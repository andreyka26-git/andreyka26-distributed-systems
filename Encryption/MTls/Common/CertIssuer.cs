using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Common;

public class CertIssuer
{
    private readonly HashSet<byte[]> _serialNumbers = new(new ByteArrayEqualityComparer());

    // Self-signed root: subject == issuer, signed with its own private key.
    // This is the trust anchor — clients install the PUBLIC half of this and decide
    // "any cert that chains to here is trusted".
    public X509Certificate2 IssueRootCaCert(string subject = "CN=AndriiCARoot")
    {
        var ecdsa = ECDsa.Create();
        var request = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);

        // BasicConstraints CA=true marks this cert as eligible to sign other certs.
        // Without this, chain validation refuses to accept it as an issuer.
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        // KeyUsage tells verifiers what this cert's key is allowed to do.
        // A CA needs KeyCertSign (to sign leaf certs) and CrlSign (to sign revocation lists).
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
                critical: true));

        return request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10));
    }

    // Signs a CSR (Certificate Signing Request, PKCS#10) submitted by a client.
    // The CSR contains the client's subject, PUBLIC key, and a self-signature proving the
    // client holds the matching private key. The private key never crosses the wire.
    public X509Certificate2 IssueLeafCert(byte[] csrDer, X509Certificate2 caCert)
    {
        // Parse the CSR. CertificateRequestLoadOptions.Default means we IGNORE any extensions
        // the client put in the CSR — the CA decides what the leaf is allowed to do.
        var csr = CertificateRequest.LoadSigningRequest(
            csrDer,
            HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.Default);

        // Leaf is NOT a CA — it can't sign other certs.
        csr.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        // Leaf key is used to sign TLS handshake messages.
        csr.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        // ExtendedKeyUsage: this cert is valid for both TLS server auth and TLS client auth.
        // We need both because each client plays both roles in mTLS (server2 when called,
        // client when calling server2).
        csr.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new Oid("1.3.6.1.5.5.7.3.1"), // id-kp-serverAuth
                    new Oid("1.3.6.1.5.5.7.3.2")  // id-kp-clientAuth
                },
                critical: true));

        // Subject Alternative Name: the TLS stack on the calling side checks that the host
        // it dialed (e.g. "localhost", or the compose service name "client2") appears in
        // the server cert's SAN. Without this, even a chain-valid cert is rejected.
        // We always include "localhost" (for local dev) and the subject's CN (which in this
        // POC IS the docker-compose service name, so "CN=client2" gets SAN "client2" and
        // can be reached as https://client2:5002 from inside the network).
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");

        var commonName = ExtractCommonName(csr.SubjectName);
        if (!string.IsNullOrEmpty(commonName) && !string.Equals(commonName, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            san.AddDnsName(commonName);
        }

        csr.CertificateExtensions.Add(san.Build());

        // Sign with the CA's private key. The returned cert has the client's PUBLIC key
        // and the CA's signature over it. No private key is attached on the CA side —
        // the client will bolt its own private key back on after receiving this.
        // Short lifetime by design: the system is built around runtime rotation, so leaves
        // expire fast and expiry itself acts as the revocation mechanism (no CRL/OCSP needed).
        // RotatingLeafCert renews at ~2/3 of this window, leaving a safety margin.
        return csr.Create(
            caCert,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(5),
            GenerateUniqueSerialNumber());
    }

    // CSR subjects look like "CN=client1" or possibly "CN=client1, O=Org". X500DistinguishedName.Format
    // is the official decoder; we just pull the CN out for use as a DNS SAN.
    private static string ExtractCommonName(X500DistinguishedName subjectName)
    {
        var formatted = subjectName.Format(multiLine: false);

        foreach (var rdn in formatted.Split(','))
        {
            var trimmed = rdn.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[3..];
            }
        }

        return string.Empty;
    }

    private byte[] GenerateUniqueSerialNumber()
    {
        var serial = new byte[16];
        do
        {
            RandomNumberGenerator.Fill(serial);
            // Clear high bit so the serial is interpreted as a positive integer.
            serial[0] &= 0x7F;
        } while (!_serialNumbers.Add(serial));

        return serial;
    }

    private sealed class ByteArrayEqualityComparer : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.AsSpan().SequenceEqual(y);
        }

        public int GetHashCode(byte[] obj)
        {
            var hash = new HashCode();
            hash.AddBytes(obj);
            return hash.ToHashCode();
        }
    }
}
