using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Common;

// Overridable via env vars for docker-compose (service names instead of localhost).
string caUrl = Environment.GetEnvironmentVariable("CA_URL") ?? "http://localhost:5000";
string thisServiceCn = Environment.GetEnvironmentVariable("SERVICE_CN") ?? "client1";
string peerUrl = Environment.GetEnvironmentVariable("PEER_URL") ?? "https://localhost:5002";
int listenPort = int.Parse(Environment.GetEnvironmentVariable("LISTEN_PORT") ?? "5001");

// =====================================================================
// STAGE 1 — BOOTSTRAP + ROTATOR
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
// STAGE 2 — TRIGGER ENDPOINT (plain HTTP, just to kick off outbound calls)
// =====================================================================

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls();
// ListenAnyIP so other containers in the compose network can hit this endpoint.
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(listenPort));
var app = builder.Build();

// =====================================================================
// STAGE 3 — OUTGOING mTLS HttpClient (rotating client cert)
//
// HttpClientHandler reads ClientCertificates once at construction and never
// re-reads it. SocketsHttpHandler lets us hook a per-handshake callback AND
// cap the connection pool's lifetime — both required for client-cert rotation:
//   - LocalCertificateSelectionCallback fires whenever a NEW TLS handshake
//     happens and needs to pick a client cert.
//   - PooledConnectionLifetime forces idle pooled connections to retire on
//     a cadence, so subsequent requests open new connections and re-fire
//     the callback. Without this, a long-lived keep-alive could pin the
//     original cert forever.
// =====================================================================

// Stash the most recently observed server cert from the TLS handshake so the
// /call-client-2 endpoint can report it back. The validation callback runs per-
// handshake on the SslStream thread, so writes are racy w.r.t. concurrent reads —
// Volatile.Write/Read gives us a publish/acquire fence without a lock.
X509Certificate2? lastServerCert = null;

var handler = new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromSeconds(60),
    SslOptions = new SslClientAuthenticationOptions
    {
        // Empty placeholder collection; the callback is the source of truth.
        ClientCertificates = new X509CertificateCollection(),

        // Called by SslStream during the handshake when the server asks for a
        // client cert. Returning the rotator's current cert means every fresh
        // handshake uses whatever the latest issued leaf is.
        LocalCertificateSelectionCallback = (_, _, _, _, _) => rotator.Current,

        // Validate the SERVER's cert against our private root only — ignore the
        // OS trust store. Note: this also runs per-handshake, so it picks up any
        // server-side rotation on its own.
        RemoteCertificateValidationCallback = (object sender, X509Certificate? serverCertificate, X509Chain? sslStreamChain, SslPolicyErrors sslPolicyErrors) =>
        {
            Console.WriteLine($"[{thisServiceCn}] validating server cert: subject={serverCertificate?.Subject} sslPolicyErrors={sslPolicyErrors}");

            if (serverCertificate is null)
            {
                Console.WriteLine($"[{thisServiceCn}] server presented no certificate");
                return false;
            }

            if (serverCertificate is not X509Certificate2 serverCertificate2)
            {
                Console.WriteLine($"[{thisServiceCn}] server certificate is not an X509Certificate2 instance");
                return false;
            }

            Volatile.Write(ref lastServerCert, serverCertificate2);

            using X509Chain validationChain = new();
            validationChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            validationChain.ChainPolicy.CustomTrustStore.Add(rootCert);
            validationChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            bool chainIsValid = validationChain.Build(serverCertificate2);

            if (!chainIsValid)
            {
                Console.WriteLine($"[{thisServiceCn}] server cert chain build FAILED:");
                foreach (X509ChainStatus chainStatus in validationChain.ChainStatus)
                {
                    Console.WriteLine($"  {chainStatus.Status}: {chainStatus.StatusInformation}");
                }
            }

            return chainIsValid;
        }
    }
};

var mtlsClient = new HttpClient(handler) { BaseAddress = new Uri(peerUrl) };

// =====================================================================
// STAGE 4 — Endpoint that triggers the outbound mTLS call
// =====================================================================

app.MapGet("/call-client-2", async (ILogger<Program> logger) =>
{
    try
    {
        var response = await mtlsClient.GetAsync("/resource");
        var content = await response.Content.ReadAsStringAsync();
        var leaf = rotator.Current;
        var serverCert = Volatile.Read(ref lastServerCert);
        logger.LogInformation("client2 replied {Status} (we used leaf serial {Serial})", (int)response.StatusCode, leaf.SerialNumber);

        var serverBlock = serverCert is null
            ? "  (no server cert observed yet — connection may have been reused without re-handshake)"
            : $"  subject: {serverCert.Subject}\n" +
              $"  issuer:  {serverCert.Issuer}\n" +
              $"  serial:  {serverCert.SerialNumber}\n" +
              $"  expires: {serverCert.NotAfter:O}";

        return Results.Text(
            $"client2 → {(int)response.StatusCode}\n{content}\n" +
            $"--- our client1 leaf (what we presented) ---\n" +
            $"  subject: {leaf.Subject}\n" +
            $"  issuer:  {leaf.Issuer}\n" +
            $"  serial:  {leaf.SerialNumber}\n" +
            $"  expires: {leaf.NotAfter:O}\n" +
            $"--- client2 server cert (from TLS handshake) ---\n" +
            $"{serverBlock}\n" +
            $"--- our trust anchor (root we accept) ---\n" +
            $"  subject: {rootCert.Subject}\n");
    }
    catch (Exception ex)
    {
        var deep = ex;
        var msg = new System.Text.StringBuilder();
        while (deep is not null)
        {
            msg.AppendLine($"{deep.GetType().FullName}: {deep.Message}");
            deep = deep.InnerException;
        }
        logger.LogError(ex, "call to client2 failed");
        return Results.Text("ERROR:\n" + msg, statusCode: 500);
    }
});

app.Run();
