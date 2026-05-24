using System.Security.Cryptography.X509Certificates;

namespace Common;

// Holds the current leaf cert for a service and re-requests a fresh one from the CA
// at ~2/3 of its validity period. Reads (Current) are lock-free; in-flight TLS
// handshakes that captured the previous reference keep working — TLS sessions are
// independent of cert validity once established, so we just let the old cert die
// by GC after no one references it anymore.
public sealed class RotatingLeafCert : IAsyncDisposable
{
    private readonly CertRequestor _requestor;
    private readonly string _caUrl;
    private readonly string _subjectName;
    private readonly CancellationTokenSource _cts = new();
    private X509Certificate2 _current = null!;
    private Task? _loop;

    public RotatingLeafCert(CertRequestor requestor, string caUrl, string subjectName)
    {
        _requestor = requestor;
        _caUrl = caUrl;
        _subjectName = subjectName;
    }

    public X509Certificate2 Current => Volatile.Read(ref _current);

    // Issues the first cert synchronously so callers can wire it into Kestrel/HttpClient
    // before the rotation loop starts. Without this, the first handshake could race the
    // CA round-trip.
    public async Task StartAsync()
    {
        var initial = await _requestor.RequestLeafCertificateAsync(_caUrl, _subjectName);
        Volatile.Write(ref _current, initial);
        _loop = Task.Run(LoopAsync);
    }

    // Forces an immediate rotation outside the background loop. The next TLS handshake
    // (and any pooled connection that re-opens) will use the freshly issued leaf.
    public async Task<X509Certificate2> RotateNowAsync()
    {
        var fresh = await _requestor.RequestLeafCertificateAsync(_caUrl, _subjectName);
        Volatile.Write(ref _current, fresh);
        return fresh;
    }

    private async Task LoopAsync()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            var leaf = Current;
            var lifetime = leaf.NotAfter - leaf.NotBefore;
            var renewAt = leaf.NotBefore.AddSeconds(lifetime.TotalSeconds * 2.0 / 3.0);
            var wait = renewAt - DateTime.Now;

            try
            {
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, ct);
                }

                var fresh = await _requestor.RequestLeafCertificateAsync(_caUrl, _subjectName);
                Volatile.Write(ref _current, fresh);
                Console.WriteLine($"[{_subjectName}] rotated cert; new expiry {fresh.NotAfter:O}");
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // CA briefly unreachable — back off and retry. The previous cert is still
                // valid until NotAfter, so we have a window to recover.
                Console.WriteLine($"[{_subjectName}] rotation failed: {ex.Message}; retrying in 10s");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch
            {
                // Loop exits via OperationCanceledException.
            }
        }
        _cts.Dispose();
    }
}
