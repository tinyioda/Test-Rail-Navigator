namespace TestRailNavigator.Tests;

/// <summary>Records and rejects unexpected network requests from in-process authentication coverage.</summary>
internal sealed class RejectExternalRequestsHandler(Action onRequest) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        onRequest();
        throw new InvalidOperationException("Unexpected outbound integration request during a security regression.");
    }
}
