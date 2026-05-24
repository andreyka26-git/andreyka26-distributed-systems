using System.Security.Cryptography.X509Certificates;
using Common;

// =====================================================================
// Root CA bootstrap server.
//
// Runs over PLAIN HTTP on purpose: clients have no trust anchor yet
// (they haven't downloaded the root cert), so there's nothing to verify
// a TLS connection against. In production you'd pin the CA's cert
// out-of-band; in this POC we just use HTTP.
// =====================================================================

// Where the root CA PFX (with private key) is persisted. Override with env var when
// running in Docker so it lands on a mounted volume (e.g. /data) and survives restarts.
var rootCertStorePath = Environment.GetEnvironmentVariable("ROOT_CA_STORAGE_PATH") ?? "./";

// Port the CA listens on. Same env-var pattern lets compose override without code changes.
var listenPort = int.Parse(Environment.GetEnvironmentVariable("ROOT_CA_PORT") ?? "5000");

Directory.CreateDirectory(rootCertStorePath);

var store = new CertStore();
var issuer = new CertIssuer();

// Every startup mints a brand-new root with a unique CN. The point is to make trust-anchor
// rotation visible: clients that fetched the OLD root at their startup will no longer be
// able to validate leaves issued by this NEW root, breaking mTLS — which is exactly the
// failure mode we want to be able to demonstrate.
var existingPfxPath = Path.Combine(rootCertStorePath, "root-ca.pfx");
if (File.Exists(existingPfxPath))
{
    File.Delete(existingPfxPath);
    Console.WriteLine($"[RootCA] Dropped existing PFX at {existingPfxPath}");
}

var rootSubject = $"CN=AndriiCARoot-{Guid.NewGuid():N}";
var rootCaCert = issuer.IssueRootCaCert(rootSubject);
await store.SaveRootCaCertAsync(rootCaCert, rootCertStorePath);
Console.WriteLine($"[RootCA] Created new root CA: {rootCaCert.Subject}");

var builder = WebApplication.CreateBuilder(args);

// Ignore any URLs from launchSettings / ASPNETCORE_URLS — bind explicitly here.
builder.WebHost.UseUrls();
builder.WebHost.ConfigureKestrel(o =>
{
    // HTTP only — no UseHttps call.
    // ListenAnyIP binds on 0.0.0.0 so other containers in the compose network can reach us
    // (ListenLocalhost would bind to the loopback inside the container only).
    o.ListenAnyIP(listenPort);
});

var app = builder.Build();

// Hand out the root CA's PUBLIC certificate as PEM.
// Clients fetch this once at startup and use it as their trust anchor.
// Only the public half — the private key never leaves this process.
app.MapGet("/root-public-cert", () =>
{
    var pem = rootCaCert.ExportCertificatePem();
    return Results.Text(pem, "application/x-pem-file");
});

// Sign a CSR (PKCS#10 DER bytes) and return the resulting leaf cert (X.509 DER bytes).
// The CSR contains the client's subject + public key + proof-of-possession signature.
// We never see the client's private key.
app.MapPost("/api/certificates/request", async (HttpContext ctx) =>
{
    using var ms = new MemoryStream();
    await ctx.Request.Body.CopyToAsync(ms);
    var csrDer = ms.ToArray();

    var leafCert = issuer.IssueLeafCert(csrDer, rootCaCert);
    Console.WriteLine($"[RootCA] Issued leaf cert: subject={leafCert.Subject} serial={leafCert.SerialNumber}");

    // RawData is the raw X.509 DER of the public cert. No private key here — there can't be:
    // we don't have the client's private key, only their public one.
    return Results.Bytes(leafCert.RawData, "application/pkix-cert");
});

app.Run();
