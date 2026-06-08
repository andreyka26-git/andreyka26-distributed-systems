using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Common;
using Microsoft.AspNetCore.Server.Kestrel.Https;

// Overridable via env vars for docker-compose (service names instead of localhost).
string caUrl = Environment.GetEnvironmentVariable("CA_URL") ?? "http://localhost:5000";
string thisServiceCn = Environment.GetEnvironmentVariable("SERVICE_CN") ?? "client2";
string allowedCallerCn = Environment.GetEnvironmentVariable("ALLOWED_CALLER_CN") ?? "client1";
int listenPort = int.Parse(Environment.GetEnvironmentVariable("LISTEN_PORT") ?? "5002");

// =====================================================================
// STAGE 1 — BOOTSTRAP
// Start the rotator: issues the first leaf synchronously, then renews it
// in the background at ~2/3 of validity.
// =====================================================================

using var bootstrapHttp = new HttpClient();
var requestor = new CertRequestor(bootstrapHttp);

// In docker-compose the CA container may still be starting when we boot.
// `depends_on` only orders container start, not service readiness, so we retry
// a bounded number of times until the CA answers.
await WaitForCaAsync(bootstrapHttp, caUrl, thisServiceCn);

await using var rotator = new RotatingLeafCert(requestor, caUrl, $"CN={thisServiceCn}");
await rotator.StartAsync();
Console.WriteLine($"[{thisServiceCn}] initial leaf: subject={rotator.Current.Subject} expiry={rotator.Current.NotAfter:O}");

// Trust anchor (root public cert) is fetched once. Root rotation is out of scope.
var rootPem = await bootstrapHttp.GetStringAsync($"{caUrl}/root-public-cert");
var rootCert = X509Certificate2.CreateFromPem(rootPem);
Console.WriteLine($"[{thisServiceCn}] trust anchor: {rootCert.Subject}");

static async Task WaitForCaAsync(HttpClient http, string caUrl, string thisServiceCn)
{
    const int maxAttempts = 30;
    var delay = TimeSpan.FromSeconds(2);

    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            using var probe = await http.GetAsync($"{caUrl}/root-public-cert");
            if (probe.IsSuccessStatusCode)
            {
                Console.WriteLine($"[{thisServiceCn}] CA reachable at {caUrl} (attempt {attempt})");
                return;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TaskCanceledException)
        {
            Console.WriteLine($"[{thisServiceCn}] CA not ready ({ex.GetType().Name}); retry {attempt}/{maxAttempts} in {delay.TotalSeconds}s");
        }

        await Task.Delay(delay);
    }

    throw new InvalidOperationException($"CA at {caUrl} never became reachable after {maxAttempts} attempts");
}

// =====================================================================
// STAGE 2 — KESTREL: HTTPS + mTLS with PER-HANDSHAKE cert selection
// =====================================================================

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls();

builder.WebHost.ConfigureKestrel(o =>
{
    // ListenAnyIP so other containers in the compose network can reach us.
    o.ListenAnyIP(listenPort, listen =>
    {
        listen.UseHttps(https =>
        {
            // Per-handshake callback — NOT a fixed ServerCertificate. Each new TLS
            // handshake reads the rotator's current value; rotation takes effect on
            // the very next incoming connection. Existing connections keep using the
            // cert they handshook with, which is correct: a TLS session is bound to
            // the keys derived during its handshake, not to cert validity.
            https.ServerCertificateSelector = (_, _) => rotator.Current;

            // Make the handshake demand a client cert — without this, no mTLS.
            https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;

            // Accept any cert at the TLS layer; chain + identity checks are in the endpoint.
            https.ClientCertificateValidation = (_, _, _) => true;
        });
    });
});

var app = builder.Build();

// =====================================================================
// STAGE 3 — Endpoint with explicit chain + identity validation
// =====================================================================

app.MapGet("/resource", async (HttpContext ctx) =>
{
    var clientCert = await ctx.Connection.GetClientCertificateAsync();
    if (clientCert is null)
    {
        Console.WriteLine($"[{thisServiceCn}] no client cert presented");
        return Results.StatusCode(401);
    }

    Console.WriteLine($"[{thisServiceCn}] incoming caller subject={clientCert.Subject} issuer={clientCert.Issuer} expiry={clientCert.NotAfter:O}");

    using var chain = new X509Chain();
    chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
    chain.ChainPolicy.CustomTrustStore.Add(rootCert);
    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

    if (!chain.Build(clientCert))
    {
        Console.WriteLine($"[{thisServiceCn}] chain build FAILED:");
        foreach (var s in chain.ChainStatus)
        {
            Console.WriteLine($"  {s.Status}: {s.StatusInformation}");
        }
        return Results.StatusCode(401);
    }

    var cn = clientCert.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
    if (cn != allowedCallerCn)
    {
        Console.WriteLine($"[{thisServiceCn}] caller CN '{cn}' is not authorized");
        return Results.StatusCode(403);
    }

    var serving = rotator.Current;
    return Results.Text(
        $"200 OK — hello {cn}\n" +
        $"  caller leaf serial:  {clientCert.SerialNumber} (expires {clientCert.NotAfter:O})\n" +
        $"  server leaf serial:  {serving.SerialNumber} (expires {serving.NotAfter:O})\n");
});

app.Run();
