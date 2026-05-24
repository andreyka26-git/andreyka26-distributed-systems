using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Common;

namespace Tests;

public class Tests
{
    private CertService _certService = null!;
    private CertIssuer _certIssuer = null!;

    [SetUp]
    public void Setup()
    {
        _certService = new CertService();
        _certIssuer = new CertIssuer();
    }

    [Test]
    public void TwoRootCerts_SameData_DifferentSignatures()
    {
        var cert1 = _certIssuer.IssueRootCaCert();
        var cert2 = _certIssuer.IssueRootCaCert();

        var dummyText = "dummytext";

        var signature1 = _certService.SignWithPrivate(dummyText, cert1);
        var signature2 = _certService.SignWithPrivate(dummyText, cert2);

        Assert.IsTrue(_certService.CheckSignature(dummyText, signature1, cert1));
        Assert.IsFalse(_certService.CheckSignature(dummyText, signature1, cert2));

        Assert.IsTrue(_certService.CheckSignature(dummyText, signature2, cert2));
        Assert.IsFalse(_certService.CheckSignature(dummyText, signature2, cert1));
    }

    [Test]
    public void RootCert_HappyPath()
    {
        var cert = _certIssuer.IssueRootCaCert();

        var dummyText = "dummytext";
        var dummyText2 = "dummytext2";
        var signature = _certService.SignWithPrivate(dummyText, cert);

        Assert.IsTrue(_certService.CheckSignature(dummyText, signature, cert));
        Assert.IsFalse(_certService.CheckSignature(dummyText2, signature, cert));
    }

    [Test]
    public void TwoLeafCerts_SameData_DifferentSignatures()
    {
        var caCert = _certIssuer.IssueRootCaCert();
        var leaf1 = IssueLeafViaCsr(_certIssuer, caCert, "CN=leaf1");
        var leaf2 = IssueLeafViaCsr(_certIssuer, caCert, "CN=leaf2");

        var dummyText = "dummytext";

        var signature1 = _certService.SignWithPrivate(dummyText, leaf1);
        var signature2 = _certService.SignWithPrivate(dummyText, leaf2);

        Assert.IsTrue(_certService.CheckSignature(dummyText, signature1, leaf1));
        Assert.IsFalse(_certService.CheckSignature(dummyText, signature1, leaf2));

        Assert.IsTrue(_certService.CheckSignature(dummyText, signature2, leaf2));
        Assert.IsFalse(_certService.CheckSignature(dummyText, signature2, leaf1));
    }

    [Test]
    public void LeafCert_HappyPath()
    {
        var caCert = _certIssuer.IssueRootCaCert();
        var leaf = IssueLeafViaCsr(_certIssuer, caCert);

        var dummyText = "dummytext";
        var dummyText2 = "dummytext2";
        var signature = _certService.SignWithPrivate(dummyText, leaf);

        Assert.IsTrue(_certService.CheckSignature(dummyText, signature, leaf));
        Assert.IsFalse(_certService.CheckSignature(dummyText2, signature, leaf));
    }

    [Test]
    public void LeafCert_SignedByCa_NotByOtherCa()
    {
        var caCert = _certIssuer.IssueRootCaCert();
        var otherCa = _certIssuer.IssueRootCaCert();
        var leaf = IssueLeafViaCsr(_certIssuer, caCert);

        Assert.AreEqual(caCert.SubjectName.Name, leaf.IssuerName.Name);
        Assert.AreNotEqual(otherCa.SubjectName.Name, leaf.SubjectName.Name);
    }

    [Test]
    public void Chain_LeafTrustedByItsCa_NotByForeignCa()
    {
        var caCert = _certIssuer.IssueRootCaCert();
        var foreignCa = _certIssuer.IssueRootCaCert();
        var leaf = IssueLeafViaCsr(_certIssuer, caCert);

        Assert.IsTrue(BuildChain(leaf, caCert), "leaf must chain to its issuing CA");
        Assert.IsFalse(BuildChain(leaf, foreignCa), "leaf must not chain to an unrelated CA");
    }

    // Mirrors what CertRequestor does in-process: generate key, build CSR, sign,
    // attach private key back. Useful in tests so the leaf cert can sign things.
    private static X509Certificate2 IssueLeafViaCsr(CertIssuer issuer, X509Certificate2 caCert, string subject = "CN=Leaf")
    {
        var key = ECDsa.Create();
        var csr = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        var csrDer = csr.CreateSigningRequest();
        var publicLeaf = issuer.IssueLeafCert(csrDer, caCert);
        return publicLeaf.CopyWithPrivateKey(key);
    }

    private static bool BuildChain(X509Certificate2 leaf, X509Certificate2 trustedRoot)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(trustedRoot);
        return chain.Build(leaf);
    }
}
