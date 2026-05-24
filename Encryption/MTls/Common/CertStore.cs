using System.Security.Cryptography.X509Certificates;

namespace Common;

public class CertStore
{
    public async Task SaveRootCaCertAsync(X509Certificate2 cert, string relativePath)
    {
        var bytes = cert.Export(X509ContentType.Pfx, "1234");
        var path = Path.Combine(relativePath, "root-ca.pfx");

        await File.WriteAllBytesAsync(path, bytes);
    }

    public X509Certificate2 LoadRootCaCert(string relativePath)
    {
        var path = Path.Combine(relativePath, "root-ca.pfx");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Root CA certificate not found in the expected location.");
        }

        var cert = new X509Certificate2(path, "1234");
        return cert;
    }
}