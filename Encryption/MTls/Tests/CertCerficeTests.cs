using Common;

namespace Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestGeneratedCert()
    {
        var certService = new CertsService();
        var cert = certService.GenerateRootCaCert();

        var dummytext = "dummytext";
        var dummytext2 = "dummytext2";
        var signature = certService.SignWithPrivate(dummytext, cert);
        
        Assert.IsTrue(certService.CheckSignature(dummytext, signature, cert));
        Assert.IsFalse(certService.CheckSignature(dummytext2, signature, cert));
    }
}