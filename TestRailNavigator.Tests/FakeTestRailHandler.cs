namespace TestRailNavigator.Tests;

/// <summary>
/// Simulates the TestRail API for in-process authentication coverage. Returns the caller-supplied
/// canned response for every request (e.g. a successful or failed live credential validation during
/// sign-in); throws when no responder is configured, matching the old hard-reject guard for every
/// scenario that must never actually reach TestRail (anonymous access, blank credentials, etc.).
/// </summary>
internal sealed class FakeTestRailHandler(
    Func<HttpRequestMessage, HttpResponseMessage?> responder, Action onRequest) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        onRequest();
        var response = responder(request);
        if (response is null)
        {
            throw new InvalidOperationException("Unexpected outbound integration request during a security regression.");
        }

        return Task.FromResult(response);
    }
}
