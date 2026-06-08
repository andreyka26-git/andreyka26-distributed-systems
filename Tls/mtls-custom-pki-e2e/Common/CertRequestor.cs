using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Common;

public class CertRequestor
{
    private readonly HttpClient _httpClient;

    public CertRequestor(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<X509Certificate2> RequestLeafCertificateAsync(string caUrl, string subjectName)
    {
        // 1) Generate a fresh ECDSA keypair locally. The PRIVATE key stays here forever —
        //    it never leaves this process. That's the whole point of having a CA: we prove
        //    we hold the private key, the CA only ever sees the public half.
        var key = ECDsa.Create();

        // 2) Build a Certificate Signing Request (PKCS#10):
        //    - our chosen subject (e.g. "CN=client1")
        //    - our PUBLIC key
        //    - a signature over both, made with our private key (proof of possession)
        var csrBuilder = new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256);
        var csrDer = csrBuilder.CreateSigningRequest();

        // 3) POST the CSR bytes to the CA's signing endpoint.
        using var content = new ByteArrayContent(csrDer);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pkcs10");
        var response = await _httpClient.PostAsync($"{caUrl}/api/certificates/request", content);
        response.EnsureSuccessStatusCode();

        // 4) The CA returns the signed leaf cert as raw X.509 DER. Public bytes only.
        var leafDer = await response.Content.ReadAsByteArrayAsync();
        var publicLeaf = new X509Certificate2(leafDer);

        // 5) Bolt our private key onto the signed public cert. Now it's usable for TLS
        //    — both as a server cert (we can decrypt handshake messages addressed to us)
        //    and as a client cert (we can sign the CertificateVerify handshake message).
        var leafWithKey = publicLeaf.CopyWithPrivateKey(key);

        // 6) Windows quirk: CopyWithPrivateKey produces a cert with an "ephemeral" key
        //    that Schannel (the Windows TLS stack Kestrel uses) refuses to use as a
        //    server certificate AND refuses to send as a client certificate.
        //    Exporting to PFX and re-importing creates a CNG-backed key Schannel can consume.
        //    PersistKeySet|Exportable produces a key that survives long enough to be used
        //    by Schannel and can be re-exported if needed. Harmless on Linux/macOS.
        var pfxBytes = leafWithKey.Export(X509ContentType.Pfx);
        return new X509Certificate2(
            pfxBytes,
            (string?)null,
            X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
    }
}
