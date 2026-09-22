using System.Net;
using System.Text;
using System.Text.Json;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

public class AzureDevOpsSecurityTests
{
    private const string HostedBase = "https://dev.azure.com/approved-test-org";
    private const string LegacyBase = "https://approved-test-org.visualstudio.com";
    private const string OnPremBase = "https://ado.example.test/tfs/Collection";
    private const string DummyPat = "dummy-ado-pat";
    private const string ApiQuery = "?$expand=relations&api-version=7.1";
    private const string DescriptionHtml = "<p>Description &amp; details<br>Second line</p>";
    private const string AcceptanceCriteriaHtml = "<ul><li>First criterion</li></ul>";
    private const string ReproStepsHtml = "<p>Reproduce the issue</p>";
    private static readonly string ExpectedAuthorization =
        $"Basic {Convert.ToBase64String(Encoding.ASCII.GetBytes($":{DummyPat}"))}";

    public static TheoryData<string?> InvalidBaseUrls => new()
    {
        null,
        "",
        "   ",
        "not a URL",
        "//ado.example.test/tfs/Collection",
        "http://ado.example.test/tfs/Collection",
        "ftp://ado.example.test/tfs/Collection",
        "https://dev.azure.com",
        "https://dev.azure.com/",
        HostedBase + "/Project",
        LegacyBase + "/Project",
        "https://nested.approved-test-org.visualstudio.com",
        "https://ado.example.test",
        "https://ado.example.test/",
        "https://username:password@ado.example.test/tfs/Collection",
        "https://@ado.example.test/tfs/Collection",
        OnPremBase + "?api-version=7.1",
        OnPremBase + "?",
        OnPremBase + "#fragment",
        OnPremBase + "#",
        OnPremBase + "/_apis/wit",
        OnPremBase + "/Project/_workitems/edit/1",
        "https://ado.example.test/tfs/Other/../Collection",
        "https://ado.example.test/tfs/./Collection",
        "https://ado.example.test/tfs/%2e%2e/Collection",
        "https://ado.example.test/tfs%2fCollection",
        "https://ado.example.test/tfs%5cCollection",
        "https://ado.example.test/tfs/%252e%252e/Collection",
        "https://ado.example.test/tfs%252fCollection",
        "https://ado.example.test/tfs//Collection",
        "https://ado.example.test\\tfs\\Collection",
        "https://ado.example.test/tfs/Collection%00",
        "https://ado.example.test/tfs/Collection%",
        "https://ado.example.test/tfs/Collection%GG",
        "https://ado.example.test/tfs/Collection;ignored",
        "https://ado.example.test:0/tfs/Collection",
        "https://ado.example.test:65536/tfs/Collection",
        "https://ado.example.test./tfs/Collection",
        "https://ado.example.test/tfs/Collec\ntion"
    };

    public static TheoryData<string, string> DisallowedDestinations => new()
    {
        { HostedBase, "https://attacker.invalid/approved-test-org/Project/_workitems/edit/42" },
        { HostedBase, "https://dev.azure.com.attacker.invalid/approved-test-org/Project/_workitems/edit/42" },
        { HostedBase, "https://dev-azure.invalid/approved-test-org/Project/_workitems/edit/42" },
        { HostedBase, "https://dev.azure.com/other-test-org/Project/_workitems/edit/42" },
        { HostedBase, "https://dev.azure.com/approved-test-org-extra/Project/_workitems/edit/42" },
        { HostedBase, "https://dev.azure.com/Approved-test-org/Project/_workitems/edit/42" },
        { HostedBase, "https://dev.azure.com/other-test-org/_apis/wit/workItems/42" },
        { HostedBase, "https://dev.azure.com/other-test-org/Project/_apis/wit/workItems/42" },
        { HostedBase, "https://dev.azure.com/other-test-org/../approved-test-org/Project/_workitems/edit/42" },
        { HostedBase, "https://dev.azure.com/approved-test-org%2fProject/_workitems/edit/42" },
        { HostedBase, "https://dev.azure.com:8443/approved-test-org/Project/_workitems/edit/42" },
        { HostedBase, "http://dev.azure.com/approved-test-org/Project/_workitems/edit/42" },
        { LegacyBase, "https://other-test-org.visualstudio.com/Project/_workitems/edit/42" },
        { LegacyBase, "https://approved-test-org.visualstudio.com.attacker.invalid/Project/_workitems/edit/42" },
        { LegacyBase, "https://approved-test-org.visualstudio.com:8443/Project/_apis/wit/workItems/42" },
        { OnPremBase, "https://attacker.invalid/tfs/Collection/Project/_workitems/edit/42" },
        { OnPremBase, "https://ado.example.test.attacker.invalid/tfs/Collection/Project/_workitems/edit/42" },
        { OnPremBase, "https://ado.example.test./tfs/Collection/Project/_workitems/edit/42" },
        { OnPremBase, "http://ado.example.test/tfs/Collection/Project/_workitems/edit/42" },
        { OnPremBase, "https://username:password@ado.example.test/tfs/Collection/Project/_workitems/edit/42" },
        { OnPremBase, "https://@ado.example.test/tfs/Collection/Project/_workitems/edit/42" },
        { OnPremBase, "https://ado.example.test@attacker.invalid/tfs/Collection/Project/_workitems/edit/42" },
        { OnPremBase, "https://ado.example.test:8443/tfs/Collection/Project/_workitems/edit/42" },
        { "https://ado.example.test:8443/tfs/Collection", OnPremBase + "/Project/_workitems/edit/42" },
        { OnPremBase, "https://ado.example.test/tfs/OtherCollection/Project/_workitems/edit/42" },
        { OnPremBase, "https://ado.example.test/tfs/OtherCollection/_apis/wit/workItems/42" },
        { OnPremBase, "https://ado.example.test/tfs/OtherCollection/Project/_apis/wit/workItems/42" },
        { OnPremBase, "https://ado.example.test/tfs/CollectionExtra/Project/_workitems/edit/42" },
        { OnPremBase, "https://ado.example.test/_apis/wit/workItems/42" },
        { OnPremBase, "https://ado.example.test/tfs/_apis/wit/workItems/42" },
        { OnPremBase, "https://ado.example.test/tfs/Other/../Collection/Project/_workitems/edit/42" },
        { OnPremBase, "https://ado.example.test/tfs/%2e%2e/tfs/Collection/Project/_workitems/edit/42" },
        { OnPremBase, "https://ado.example.test/tfs%2fCollection/Project/_workitems/edit/42" },
        { OnPremBase, "https://ado.example.test/tfs/Collection%5cProject/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project/../Project/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project/%2E%2E/Project/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project/.%2e/Project/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project/./_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/./_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/%252e%252e/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project%2fOther/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project%5cOther/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project%252fOther/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project%3fOther/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project%23Other/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project%c0%afOther/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project%00/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project%/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project%GG/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project;ignored/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "//Project/_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project\\_workitems/edit/42" },
        { OnPremBase, OnPremBase + "/Project/_workitems/edit/0" },
        { OnPremBase, OnPremBase + "/Project/_workitems/edit/-42" },
        { OnPremBase, OnPremBase + "/Project/_workitems/edit/+42" },
        { OnPremBase, OnPremBase + "/Project/_workitems/edit/2147483648" },
        { OnPremBase, OnPremBase + "/Project/_workitems/edit/42suffix" },
        { OnPremBase, OnPremBase + "/Project/_workitems/edit/42/extra" },
        { OnPremBase, OnPremBase + "/Project/_apis/wit/workItems/42suffix" },
        { OnPremBase, "/tfs/Collection/Project/_workitems/edit/42" },
        { OnPremBase, "" }
    };

    [Theory]
    [InlineData(HostedBase, HostedBase)]
    [InlineData(HostedBase + "/", HostedBase)]
    [InlineData("https://DEV.AZURE.COM:443/approved-test-org", HostedBase)]
    [InlineData("https://dev.azure.com:8443/approved-test-org", "https://dev.azure.com:8443/approved-test-org")]
    [InlineData(LegacyBase, LegacyBase + "/")]
    [InlineData(LegacyBase + "/", LegacyBase + "/")]
    [InlineData("https://approved-test-org.visualstudio.com:8443", "https://approved-test-org.visualstudio.com:8443/")]
    [InlineData(OnPremBase, OnPremBase)]
    [InlineData(OnPremBase + "/", OnPremBase)]
    [InlineData("https://ado.example.test:8443/tfs/Collection", "https://ado.example.test:8443/tfs/Collection")]
    [InlineData("https://ado.example.test/Collection", "https://ado.example.test/Collection")]
    [InlineData("https://ado.example.test/tfs/Collection%20Name", "https://ado.example.test/tfs/Collection%20Name")]
    public void ApprovedBaseUrlsAreCanonicalized(string value, string expected)
    {
        Assert.True(AzureDevOpsUrlPolicy.TryGetBaseUri(value, out var uri));
        Assert.NotNull(uri);
        Assert.Equal(expected, uri.AbsoluteUri);
    }

    [Theory]
    [MemberData(nameof(InvalidBaseUrls))]
    public void InvalidBaseUrlsAreRejected(string? value)
    {
        Assert.False(AzureDevOpsUrlPolicy.TryGetBaseUri(value, out var uri));
        Assert.Null(uri);
    }

    [Theory]
    [MemberData(nameof(InvalidBaseUrls))]
    public async Task InvalidConfigurationFailsClosedWithoutRequests(string? baseUrl)
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(baseUrl ?? string.Empty));
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);

        Assert.False(await service.IsConfiguredAsync());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetItemAsync(OnPremBase + "/Project/_workitems/edit/42"));

        Assert.Contains("base URL", error.Message);
        AssertSafeError(error);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingPatFailsClosedWithoutRequests(string pat)
    {
        var settings = Settings(OnPremBase);
        settings.AzureDevOpsPat = pat;
        using var scope = await TestSettingsScope.CreateAsync(settings);
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);

        Assert.False(await service.IsConfiguredAsync());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetItemAsync(OnPremBase + "/Project/_workitems/edit/42"));

        Assert.Contains("PAT is not configured", error.Message);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HostedBase, HostedBase + "/Project/_workitems/edit/42", HostedBase + "/Project/_apis/wit/workitems/42")]
    [InlineData(HostedBase, HostedBase + "/Team%20Project/_workitems/view/00042?fullScreen=true#details", HostedBase + "/Team%20Project/_apis/wit/workitems/42")]
    [InlineData(HostedBase, HostedBase + "/Project/_apis/wit/workItems/42?api-version=6.0", HostedBase + "/Project/_apis/wit/workitems/42")]
    [InlineData(HostedBase, "https://DEV.AZURE.COM:443/approved-test-org/Project/_WORKITEMS/EDIT/42", HostedBase + "/Project/_apis/wit/workitems/42")]
    [InlineData("https://dev.azure.com:8443/approved-test-org", "https://dev.azure.com:8443/approved-test-org/Project/_workitems/edit/42", "https://dev.azure.com:8443/approved-test-org/Project/_apis/wit/workitems/42")]
    [InlineData(LegacyBase, LegacyBase + "/Project/_workitems/edit/42", LegacyBase + "/Project/_apis/wit/workitems/42")]
    [InlineData(LegacyBase + "/", LegacyBase + "/Team%20Project/_workitems/view/42", LegacyBase + "/Team%20Project/_apis/wit/workitems/42")]
    [InlineData(LegacyBase, LegacyBase + "/Project/_apis/wit/workItems/42", LegacyBase + "/Project/_apis/wit/workitems/42")]
    [InlineData("https://approved-test-org.visualstudio.com:8443", "https://approved-test-org.visualstudio.com:8443/Project/_workitems/edit/42", "https://approved-test-org.visualstudio.com:8443/Project/_apis/wit/workitems/42")]
    [InlineData(OnPremBase, OnPremBase + "/Project/_workitems/edit/42", OnPremBase + "/Project/_apis/wit/workitems/42")]
    [InlineData(OnPremBase, OnPremBase + "/Team Project/_workitems/view/42", OnPremBase + "/Team%20Project/_apis/wit/workitems/42")]
    [InlineData(OnPremBase + "/", OnPremBase + "/Team%20%2B%20Project/_apis/wit/workItems/42", OnPremBase + "/Team%20%2B%20Project/_apis/wit/workitems/42")]
    [InlineData(OnPremBase, OnPremBase + "/Team%20%E2%9C%93/_workitems/edit/42", OnPremBase + "/Team%20%E2%9C%93/_apis/wit/workitems/42")]
    [InlineData(OnPremBase, OnPremBase + "/Project/_workitems/edit/42?redirect=https://attacker.invalid&api-version=999", OnPremBase + "/Project/_apis/wit/workitems/42")]
    [InlineData("https://ado.example.test:8443/tfs/Collection", "https://ado.example.test:8443/tfs/Collection/Project/_workitems/edit/42", "https://ado.example.test:8443/tfs/Collection/Project/_apis/wit/workitems/42")]
    [InlineData("https://ado.example.test:443/tfs/Collection", OnPremBase + "/Project/_workitems/edit/42/", OnPremBase + "/Project/_apis/wit/workitems/42")]
    [InlineData("https://ado.example.test/tfs/Collection%20Name", "https://ado.example.test/tfs/Collection%20Name/Project/_workitems/edit/42", "https://ado.example.test/tfs/Collection%20Name/Project/_apis/wit/workitems/42")]
    public async Task ApprovedWorkItemsUseOnlyTheConfiguredBaseAndEscapedProject(
        string baseUrl,
        string url,
        string expectedApiUrl)
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(baseUrl));
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);

        Assert.True(await service.IsConfiguredAsync());
        var item = await service.GetItemAsync(url);

        Assert.NotNull(item);
        Assert.Equal(42, item.NumericId);
        Assert.Equal("AB#42", item.Reference);
        Assert.Equal(expectedApiUrl.Replace("/_apis/wit/workitems/", "/_workitems/edit/"), item.SourceUrl);
        Assert.Equal(DescriptionHtml, item.DescriptionHtml);
        Assert.Equal(AcceptanceCriteriaHtml, item.AcceptanceCriteriaHtml);
        Assert.Equal(ReproStepsHtml, item.ReproStepsHtml);
        AssertRequest(Assert.Single(handler.Requests), expectedApiUrl + ApiQuery);
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Theory]
    [MemberData(nameof(DisallowedDestinations))]
    public async Task DisallowedWorkItemsNeverSendRequests(string baseUrl, string url)
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(baseUrl));
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.GetItemAsync(url));

        AssertSafeError(error);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [MemberData(nameof(DisallowedDestinations))]
    public async Task UntrustedRelationsAreRejectedBeforeTheyCanBeRewritten(string baseUrl, string relationUrl)
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(baseUrl));
        var handler = new RecordingHandler(_ => WorkItemResponse(relationUrl));
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetItemAsync(baseUrl + "/Parent%20Project/_workitems/edit/1"));

        AssertSafeError(error);
        AssertRequest(
            Assert.Single(handler.Requests),
            baseUrl + "/Parent%20Project/_apis/wit/workitems/1" + ApiQuery);
    }

    [Theory]
    [MemberData(nameof(DisallowedDestinations))]
    public async Task UntrustedChildUrlsAreRevalidatedWithoutSendingRequests(string baseUrl, string childUrl)
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(baseUrl));
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);
        var parent = new IssueTrackerItem
        {
            SourceUrl = baseUrl + "/Parent%20Project/_workitems/edit/1",
            ChildUrls = [childUrl]
        };

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.GetChildrenAsync(parent));

        AssertSafeError(error);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HostedBase, HostedBase + "/_apis/wit/workItems/2", HostedBase + "/Parent%20Project")]
    [InlineData(LegacyBase, LegacyBase + "/_apis/wit/workItems/2", LegacyBase + "/Parent%20Project")]
    [InlineData(OnPremBase, OnPremBase + "/_apis/wit/workItems/2?api-version=7.1", OnPremBase + "/Parent%20Project")]
    [InlineData(OnPremBase, OnPremBase + "/Child%20Project/_apis/wit/workItems/2", OnPremBase + "/Child%20Project")]
    [InlineData(HostedBase, HostedBase + "/Child%20Project/_apis/wit/workItems/2", HostedBase + "/Child%20Project")]
    [InlineData(LegacyBase, LegacyBase + "/Child%20Project/_apis/wit/workItems/2", LegacyBase + "/Child%20Project")]
    [InlineData("https://ado.example.test:8443/tfs/Collection", "https://ado.example.test:8443/tfs/Collection/_apis/wit/workItems/2", "https://ado.example.test:8443/tfs/Collection/Parent%20Project")]
    public async Task ApprovedRelationsPreserveTheirScopeAndInheritOnlyMissingProjects(
        string baseUrl,
        string relationUrl,
        string expectedChildProjectUrl)
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(baseUrl));
        var handler = new RecordingHandler(request => request.Uri.AbsolutePath.EndsWith("/1", StringComparison.Ordinal)
            ? WorkItemResponse(relationUrl)
            : WorkItemResponse());
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);

        var parent = await service.GetItemAsync(baseUrl + "/Parent%20Project/_workitems/edit/1");

        Assert.NotNull(parent);
        Assert.Equal(expectedChildProjectUrl + "/_workitems/edit/2", Assert.Single(parent.ChildUrls));
        var child = Assert.Single(await service.GetChildrenAsync(parent));
        Assert.Equal(expectedChildProjectUrl + "/_workitems/edit/2", child.SourceUrl);
        Assert.Collection(handler.Requests,
            request => AssertRequest(request, baseUrl + "/Parent%20Project/_apis/wit/workitems/1" + ApiQuery),
            request => AssertRequest(request, expectedChildProjectUrl + "/_apis/wit/workitems/2" + ApiQuery));
    }

    [Theory]
    [InlineData(HostedBase)]
    [InlineData(LegacyBase)]
    [InlineData(OnPremBase)]
    public async Task RawOrganizationScopedChildrenUseAValidatedParentProject(string baseUrl)
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(baseUrl));
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);
        var parent = new IssueTrackerItem
        {
            SourceUrl = baseUrl + "/Parent%20Project/_workitems/edit/1",
            ChildUrls = [baseUrl + "/_apis/wit/workItems/2"]
        };

        var child = Assert.Single(await service.GetChildrenAsync(parent));

        Assert.Equal(baseUrl + "/Parent%20Project/_workitems/edit/2", child.SourceUrl);
        AssertRequest(
            Assert.Single(handler.Requests),
            baseUrl + "/Parent%20Project/_apis/wit/workitems/2" + ApiQuery);
    }

    [Theory]
    [InlineData(HostedBase)]
    [InlineData(LegacyBase)]
    [InlineData(OnPremBase)]
    public async Task OrganizationScopedRootUrlsDoNotGuessAProject(string baseUrl)
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(baseUrl));
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetItemAsync(baseUrl + "/_apis/wit/workItems/42"));

        Assert.Contains("parent project", error.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UntrustedParentCannotSupplyAFallbackProject()
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(OnPremBase));
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);
        var parent = new IssueTrackerItem
        {
            SourceUrl = "https://ado.example.test/tfs/OtherCollection/Project/_workitems/edit/1",
            ChildUrls = [OnPremBase + "/_apis/wit/workItems/2"]
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetChildrenAsync(parent));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AllChildrenAreValidatedBeforeAnyChildRequest()
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(OnPremBase));
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);
        var parent = new IssueTrackerItem
        {
            SourceUrl = OnPremBase + "/Project/_workitems/edit/1",
            ChildUrls =
            [
                OnPremBase + "/Project/_workitems/edit/2",
                "https://attacker.invalid/tfs/Collection/Project/_workitems/edit/3"
            ]
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetChildrenAsync(parent));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ChangingApprovedBaseDoesNotReusePreviouslyValidatedChildLinks()
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(OnPremBase));
        var handler = new RecordingHandler(_ => WorkItemResponse(OnPremBase + "/_apis/wit/workItems/2"));
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);
        var parent = await service.GetItemAsync(OnPremBase + "/Project/_workitems/edit/1");
        Assert.NotNull(parent);

        await scope.Settings.SaveSettingsAsync(Settings("https://ado.example.test/tfs/OtherCollection"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetChildrenAsync(parent));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ChildHttpFailuresAreNotSwallowed()
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(OnPremBase));
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"message\":\"Access denied for this work item.\"}", Encoding.UTF8, "application/json")
        });
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);
        var parent = new IssueTrackerItem
        {
            SourceUrl = OnPremBase + "/Project/_workitems/edit/1",
            ChildUrls = [OnPremBase + "/Project/_workitems/edit/2"]
        };

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetChildrenAsync(parent));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Equal("Access denied for this work item.", error.Message);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData("/tfs/Collection/Project/_apis/wit/workitems/42")]
    [InlineData(OnPremBase + "/Project/_apis/wit/workitems/42")]
    public async Task EvenSameOriginRedirectsAreRejected(string location)
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(OnPremBase));
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        });
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetItemAsync(OnPremBase + "/Project/_workitems/edit/42"));

        Assert.Equal(HttpStatusCode.TemporaryRedirect, error.StatusCode);
        Assert.Contains("redirect", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(handler.Requests);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(301)]
    [InlineData(302)]
    [InlineData(303)]
    [InlineData(304)]
    [InlineData(305)]
    [InlineData(307)]
    [InlineData(308)]
    public async Task RedirectsAreSurfacedWithoutASecondRequest(int statusCode)
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(OnPremBase));
        var handler = new RecordingHandler(_ =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)statusCode)
            {
                Content = new StringContent("Redirect response")
            };
            response.Headers.Location = new Uri("https://attacker.invalid/collect");
            return response;
        });
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.GetItemAsync(OnPremBase + "/Project/_workitems/edit/42"));

        Assert.Equal((HttpStatusCode)statusCode, error.StatusCode);
        Assert.Contains("redirect", error.Message, StringComparison.OrdinalIgnoreCase);
        AssertSafeError(error);
        AssertRequest(Assert.Single(handler.Requests), OnPremBase + "/Project/_apis/wit/workitems/42" + ApiQuery);
    }

    [Theory]
    [InlineData(HostedBase + "/Team%20Project/_workitems/edit/42", "approved-test-org", "Team Project")]
    [InlineData(HostedBase + "/Project/_workitems/view/42?fullScreen=true", "approved-test-org", "Project")]
    [InlineData(HostedBase + "/Project/_apis/wit/workItems/42", "approved-test-org", "Project")]
    [InlineData(LegacyBase + "/Project/_workitems/edit/42", "approved-test-org", "Project")]
    [InlineData(LegacyBase + "/Project/_apis/wit/workItems/42", "approved-test-org", "Project")]
    [InlineData(OnPremBase + "/Project/_workitems/edit/42", "Collection", "Project")]
    [InlineData(OnPremBase + "/Project/_apis/wit/workItems/42", "Collection", "Project")]
    [InlineData("https://ado.example.test:8443/tfs/Collection/Project/_workitems/view/42", "Collection", "Project")]
    public void PublicParserPreservesSupportedUrlInformation(string url, string expectedOrganization, string expectedProject)
    {
        Assert.True(AzureDevOpsService.TryParseWorkItemUrl(url, out var organization, out var project, out var id));
        Assert.Equal(expectedOrganization, organization);
        Assert.Equal(expectedProject, project);
        Assert.Equal(42, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://ado.example.test/tfs/Collection/Project/_workitems/edit/42")]
    [InlineData(OnPremBase + "/Project/_workitems/edit/0")]
    [InlineData(OnPremBase + "/Project/_workitems/edit/42suffix")]
    [InlineData(OnPremBase + "/Project/_workitems/edit/42/extra")]
    [InlineData(OnPremBase + "/%2e%2e/_workitems/edit/42")]
    public void PublicParserRejectsUnsafeOrPartialMatches(string url)
    {
        Assert.False(AzureDevOpsService.TryParseWorkItemUrl(url, out var organization, out var project, out var id));
        Assert.Empty(organization);
        Assert.Empty(project);
        Assert.Equal(0, id);
    }

    [Fact]
    public async Task LegacyWorkItemHelperPreservesHtmlFields()
    {
        using var scope = await TestSettingsScope.CreateAsync(Settings(OnPremBase));
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = new AzureDevOpsService(client, scope.Settings);

        var item = await service.GetWorkItemAsync(OnPremBase + "/Project/_workitems/edit/42");

        Assert.NotNull(item);
        Assert.Equal(42, item.Id);
        Assert.Equal("Example story", item.Title);
        Assert.Equal("User Story", item.WorkItemType);
        Assert.Equal(DescriptionHtml, item.Description);
        Assert.Equal(AcceptanceCriteriaHtml, item.AcceptanceCriteria);
        Assert.Equal(ReproStepsHtml, item.ReproSteps);
        Assert.Equal(OnPremBase + "/Project/_workitems/edit/42", item.SourceUrl);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(DescriptionHtml, "Description & details\nSecond line")]
    [InlineData(AcceptanceCriteriaHtml, "- First criterion")]
    [InlineData("<div> First   paragraph </div><p>Second&nbsp;paragraph</p>", "First paragraph \nSecond\u00a0paragraph")]
    public void HtmlToPlainTextBehaviorIsUnchanged(string? html, string? expected)
    {
        Assert.Equal(expected, AzureDevOpsService.HtmlToPlainText(html));
    }

    private static TestRailSettings Settings(string baseUrl) => new()
    {
        AzureDevOpsBaseUrl = baseUrl,
        AzureDevOpsPat = DummyPat
    };

    private static void AssertSafeError(Exception error)
    {
        Assert.DoesNotContain(DummyPat, error.Message);
        Assert.DoesNotContain(ExpectedAuthorization, error.Message);
        Assert.DoesNotContain("password", error.Message);
    }

    private static void AssertRequest(RecordedRequest request, string expectedUrl)
    {
        Assert.Equal(expectedUrl, request.Uri.AbsoluteUri);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(ExpectedAuthorization, request.Authorization);
        Assert.True(request.AcceptsJson);
    }

    private static HttpResponseMessage WorkItemResponse(params string[] childUrls)
    {
        var payload = new
        {
            fields = new Dictionary<string, string>
            {
                ["System.Title"] = "Example story",
                ["System.WorkItemType"] = "User Story",
                ["System.Description"] = DescriptionHtml,
                ["Microsoft.VSTS.Common.AcceptanceCriteria"] = AcceptanceCriteriaHtml,
                ["Microsoft.VSTS.TCM.ReproSteps"] = ReproStepsHtml
            },
            relations = childUrls.Select(url => new { rel = "System.LinkTypes.Hierarchy-Forward", url })
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private sealed record RecordedRequest(Uri Uri, HttpMethod Method, string? Authorization, bool AcceptsJson);

    private sealed class RecordingHandler(Func<RecordedRequest, HttpResponseMessage>? respond = null) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = new RecordedRequest(
                request.RequestUri ?? throw new InvalidOperationException("A request URI is required."),
                request.Method,
                request.Headers.Authorization?.ToString(),
                request.Headers.Accept.Any(header => header.MediaType == "application/json"));
            Requests.Add(recorded);
            return Task.FromResult(respond is null ? WorkItemResponse() : respond(recorded));
        }
    }
}
