namespace TestRailNavigator.Services;

/// <summary>
/// Request-scoped holder for the signed-in user's own TestRail credentials, when available.
/// Populated either while validating a login attempt or, for subsequent requests, from that
/// user's server-side session (see <see cref="TestRailCredentialStore"/>). <see cref="TestRailClient"/>
/// prefers these over the configured service account whenever they are present.
/// </summary>
public sealed class CurrentTestRailCredentials
{
    /// <summary>Gets or sets the signed-in user's TestRail username (email), if any.</summary>
    public string? Username { get; set; }

    /// <summary>Gets or sets the signed-in user's TestRail password or personal API key, if any.</summary>
    public string? Secret { get; set; }
}
