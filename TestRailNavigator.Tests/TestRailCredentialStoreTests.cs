using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Unit coverage for the in-memory per-user TestRail credential store.</summary>
public class TestRailCredentialStoreTests
{
    /// <summary>A freshly stored session can be retrieved by its returned token.</summary>
    [Fact]
    public void Store_ReturnsATokenThatRetrievesTheSameCredentials()
    {
        var store = new TestRailCredentialStore();

        var token = store.Store("alice@example.test", "secret-password");
        var credentials = store.Get(token);

        Assert.NotNull(credentials);
        Assert.Equal("alice@example.test", credentials.Value.Username);
        Assert.Equal("secret-password", credentials.Value.Secret);
        Assert.True(store.Contains(token));
    }

    /// <summary>Separate sessions never collide on the same token.</summary>
    [Fact]
    public void Store_IssuesUniqueTokensForEachSession()
    {
        var store = new TestRailCredentialStore();

        var first = store.Store("alice@example.test", "alice-secret");
        var second = store.Store("bob@example.test", "bob-secret");

        Assert.NotEqual(first, second);
        Assert.Equal("alice@example.test", store.Get(first)?.Username);
        Assert.Equal("bob@example.test", store.Get(second)?.Username);
    }

    /// <summary>Unknown or missing tokens never resolve to credentials.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    public void Get_ReturnsNullForUnknownTokens(string? token)
    {
        var store = new TestRailCredentialStore();
        store.Store("alice@example.test", "alice-secret");

        Assert.Null(store.Get(token));
        Assert.False(store.Contains(token));
    }

    /// <summary>Removing a session discards only that session's credentials.</summary>
    [Fact]
    public void Remove_DiscardsOnlyTheGivenSession()
    {
        var store = new TestRailCredentialStore();
        var removed = store.Store("alice@example.test", "alice-secret");
        var kept = store.Store("bob@example.test", "bob-secret");

        store.Remove(removed);

        Assert.False(store.Contains(removed));
        Assert.True(store.Contains(kept));
    }

    /// <summary>Clearing the store (simulating an app restart) discards every session at once.</summary>
    [Fact]
    public void Clear_DiscardsEverySession()
    {
        var store = new TestRailCredentialStore();
        var first = store.Store("alice@example.test", "alice-secret");
        var second = store.Store("bob@example.test", "bob-secret");

        store.Clear();

        Assert.False(store.Contains(first));
        Assert.False(store.Contains(second));
    }
}
