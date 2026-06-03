namespace CircleK.QualityAudit.Client.Http;

public sealed class CsrfTokenHandler : DelegatingHandler
{
    private readonly CsrfTokenStore _store;

    public CsrfTokenHandler(CsrfTokenStore store)
    {
        _store = store;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get &&
            request.Method != HttpMethod.Head &&
            request.Method != HttpMethod.Options &&
            request.Method != HttpMethod.Trace)
        {
            if (!string.IsNullOrWhiteSpace(_store.Token))
            {
                request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", _store.Token);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
