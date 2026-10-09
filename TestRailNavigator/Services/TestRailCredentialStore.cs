using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TestRailNavigator.Services;

/// <summary>
/// Holds signed-in users' own TestRail credentials in memory for the lifetime of their session,
/// keyed by an opaque session token carried only in their (encrypted) authentication cookie.
/// Credentials are never persisted to disk and are cleared on sign-out, on a failed revalidation,
/// or implicitly when the app restarts (which empties this store, forcing everyone to sign in again).
/// </summary>
public sealed class TestRailCredentialStore
{
    private readonly ConcurrentDictionary<string, (string Username, string Secret)> _sessions = new();

    /// <summary>Stores credentials for a new session and returns its opaque session token.</summary>
    public string Store(string username, string secret)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _sessions[token] = (username, secret);
        return token;
    }

    /// <summary>Gets the credentials for the given session token, or null if the session no longer exists.</summary>
    public (string Username, string Secret)? Get(string? token) =>
        token is not null && _sessions.TryGetValue(token, out var credentials) ? credentials : null;

    /// <summary>Determines whether the given session token still has stored credentials.</summary>
    public bool Contains(string? token) => token is not null && _sessions.ContainsKey(token);

    /// <summary>Removes the session's stored credentials (sign-out).</summary>
    public void Remove(string? token)
    {
        if (token is not null)
        {
            _sessions.TryRemove(token, out _);
        }
    }

    /// <summary>Removes every stored session, revoking all already-issued cookies at once. Useful to
    /// simulate (or deliberately force) the same effect an app restart has on in-memory sessions.</summary>
    public void Clear() => _sessions.Clear();
}
