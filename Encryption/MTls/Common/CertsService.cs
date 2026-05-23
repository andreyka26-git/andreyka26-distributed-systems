using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Common;

public class CertsService 
{
    public X509Certificate2 GenerateRootCaCert()
    {
        // key pair
        var ecdsa = ECDsa.Create();
        
        Console.WriteLine(ecdsa.ExportPkcs8PrivateKeyPem());

        var request = new CertificateRequest("CN=AndriiCARoot", ecdsa, HashAlgorithmName.SHA256);
        var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10));
            
        return cert;
    }

    public byte[] SignWithPrivate(string textCorpus, X509Certificate2 cert)
    {
        var privateKey = cert.GetECDsaPrivateKey();
        var data = Encoding.UTF8.GetBytes(textCorpus);

        var signature = privateKey.SignData(data, HashAlgorithmName.SHA256);
        return signature;
    }

    public bool CheckSignature(string textCorpus, byte[] signature, X509Certificate2 cert)
    {
        var publicKey = cert.GetECDsaPublicKey();
        var data = Encoding.UTF8.GetBytes(textCorpus);
        
        return publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256);
    }
}