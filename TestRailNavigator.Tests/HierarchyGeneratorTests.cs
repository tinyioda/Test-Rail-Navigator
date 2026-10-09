using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Unit coverage for hierarchy generation orchestration and branching.</summary>
public class HierarchyGeneratorTests
{
    /// <summary>A leaf story creates a section, cases, a plan, and a run with the expected payloads.</summary>
    [Fact]
    public async Task GenerateAsync_CreatesLeafHierarchyFromAStory()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var handler = new RecordingHandler(async request =>
        {
            if (request.Path.EndsWith("get_milestones/12", StringComparison.Ordinal))
            {
                return JsonResponse("""{"milestones":[],"size":0,"limit":0}""");
            }

            if (request.Path.EndsWith("get_plans/12", StringComparison.Ordinal))
            {
                return JsonResponse("""{"plans":[],"size":0,"limit":0}""");
            }

            if (request.Path.EndsWith("get_sections/12&suite_id=34", StringComparison.Ordinal))
            {
                return JsonResponse("""{"sections":[],"size":0,"limit":0}""");
            }

            if (request.Path.EndsWith("add_section/12", StringComparison.Ordinal))
            {
                return JsonResponse("""{"id":501,"suite_id":34,"name":"[AB#100] Login story"}""");
            }

            if (request.Path.EndsWith("add_case/501", StringComparison.Ordinal))
            {
                var nextId = request.MatchingRequestOrdinal("add_case/501") == 1 ? 601 : 602;
                return JsonResponse($$"""{"id":{{nextId}},"title":"Case {{nextId}}"}""");
            }

            if (request.Path.EndsWith("add_plan/12", StringComparison.Ordinal))
            {
                return JsonResponse("""{"id":701,"name":"[AB#100] Login story","project_id":12}""");
            }

            if (request.Path.EndsWith("add_plan_entry/701", StringComparison.Ordinal))
            {
                return JsonResponse("""{"id":"entry-1","suite_id":34,"name":"Login story","runs":[{"id":801,"name":"Login story"}]}""");
            }

            throw new InvalidOperationException($"Unexpected request: {request.Path}");
        });
        using var client = new HttpClient(handler);
        var generator = new HierarchyGenerator(new TestRailClient(client, scope.Settings), new ConsoleLogService());
        var tracker = new FakeIssueTrackerClient();
        var item = new IssueTrackerItem
        {
            Reference = "AB#100",
            Title = "Login story",
            Kind = IssueTrackerItemKind.Story,
            RawType = "User Story",
            SourceUrl = "https://ado.example/items/100",
            DescriptionHtml = "<p>Story description</p>",
            AcceptanceCriteriaHtml = "<ul><li>User can sign in</li><li>User sees the dashboard</li></ul>"
        };

        var report = await generator.GenerateAsync(tracker, item, new HierarchyRequest
        {
            ProjectId = 12,
            SuiteId = 34,
            ParentMilestoneId = 77,
            DefaultPriorityId = 3,
            DefaultTypeId = 6
        });

        Assert.Single(report.SectionsCreated);
        Assert.Equal(2, report.CasesCreated.Count);
        Assert.Single(report.PlansCreated);
        Assert.Single(report.RunsCreated);
        Assert.True(report.HasAnyWork);
        Assert.Empty(report.Warnings);

        var addCasePayload = handler.FindRequests("add_case/501")[0].Body!.AsObject();
        Assert.Equal("AB#100", addCasePayload["refs"]?.GetValue<string>());
        Assert.Equal(3, addCasePayload["priority_id"]?.GetValue<int>());
        Assert.Equal(6, addCasePayload["type_id"]?.GetValue<int>());
        Assert.Equal("Story description", addCasePayload["custom_preconds"]?.GetValue<string>());

        var addPlanPayload = handler.FindSingleRequest("add_plan/12").Body!.AsObject();
        Assert.Equal("[AB#100] Login story", addPlanPayload["name"]?.GetValue<string>());
        Assert.Equal(77, addPlanPayload["milestone_id"]?.GetValue<int>());
        Assert.Contains("Source: https://ado.example/items/100", addPlanPayload["description"]?.GetValue<string>());

        var addPlanEntryPayload = handler.FindSingleRequest("add_plan_entry/701").Body!.AsObject();
        Assert.False(addPlanEntryPayload["include_all"]?.GetValue<bool>() ?? true);
        Assert.Equal(2, addPlanEntryPayload["case_ids"]!.AsArray().Count);
    }

    /// <summary>Feature children are processed alphabetically by title and the feature milestone honors the requested parent.</summary>
    [Fact]
    public async Task GenerateAsync_SortsFeatureChildrenAlphabetically()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var handler = new RecordingHandler(async request =>
        {
            if (request.Path.EndsWith("get_milestones/12", StringComparison.Ordinal))
            {
                return JsonResponse("""{"milestones":[],"size":0,"limit":0}""");
            }

            if (request.Path.EndsWith("get_plans/12", StringComparison.Ordinal))
            {
                return JsonResponse("""{"plans":[],"size":0,"limit":0}""");
            }

            if (request.Path.EndsWith("get_sections/12&suite_id=34", StringComparison.Ordinal))
            {
                return JsonResponse("""{"sections":[],"size":0,"limit":0}""");
            }

            if (request.Path.EndsWith("add_milestone/12", StringComparison.Ordinal))
            {
                return JsonResponse("""{"id":401,"project_id":12,"name":"[AB#200] Feature root"}""");
            }

            if (request.Path.EndsWith("add_section/12", StringComparison.Ordinal))
            {
                var sectionId = 500 + request.MatchingRequestOrdinal("add_section/12");
                return JsonResponse($$"""{"id":{{sectionId}},"suite_id":34,"name":"section-{{sectionId}}"}""");
            }

            if (request.Path.Contains("add_case/", StringComparison.Ordinal))
            {
                var caseId = 600 + request.MatchingRequestOrdinal("add_case/");
                return JsonResponse($$"""{"id":{{caseId}},"title":"Case {{caseId}}"}""");
            }

            if (request.Path.EndsWith("add_plan/12", StringComparison.Ordinal))
            {
                var planId = 700 + request.MatchingRequestOrdinal("add_plan/12");
                return JsonResponse($$"""{"id":{{planId}},"project_id":12,"name":"plan-{{planId}}"}""");
            }

            if (request.Path.Contains("add_plan_entry/", StringComparison.Ordinal))
            {
                var runId = 800 + request.MatchingRequestOrdinal("add_plan_entry/");
                return JsonResponse($$"""{"id":"entry-{{runId}}","suite_id":34,"name":"entry","runs":[{"id":{{runId}},"name":"run-{{runId}}"}]}""");
            }

            throw new InvalidOperationException($"Unexpected request: {request.Path}");
        });
        using var client = new HttpClient(handler);
        var generator = new HierarchyGenerator(new TestRailClient(client, scope.Settings), new ConsoleLogService());

        var root = new IssueTrackerItem
        {
            Reference = "AB#200",
            Title = "Feature root",
            Kind = IssueTrackerItemKind.Feature
        };
        var alphaStory = new IssueTrackerItem
        {
            Reference = "AB#202",
            Title = "alpha story",
            Kind = IssueTrackerItemKind.Story,
            AcceptanceCriteriaHtml = "<ul><li>Alpha criterion</li></ul>"
        };
        var zuluStory = new IssueTrackerItem
        {
            Reference = "AB#201",
            Title = "Zulu story",
            Kind = IssueTrackerItemKind.Story,
            AcceptanceCriteriaHtml = "<ul><li>Zulu criterion</li></ul>"
        };
        var tracker = new FakeIssueTrackerClient(new Dictionary<string, IReadOnlyList<IssueTrackerItem>>
        {
            [root.Reference] = [zuluStory, alphaStory]
        });

        var report = await generator.GenerateAsync(tracker, root, new HierarchyRequest
        {
            ProjectId = 12,
            SuiteId = 34,
            ParentMilestoneId = 5
        });

        Assert.Single(report.MilestonesCreated);
        Assert.Equal(2, report.PlansCreated.Count);
        Assert.Equal(2, report.RunsCreated.Count);

        var milestonePayload = handler.FindSingleRequest("add_milestone/12").Body!.AsObject();
        Assert.Equal(5, milestonePayload["parent_id"]?.GetValue<int>());

        var planNames = handler.FindRequests("add_plan/12")
            .Select(request => request.Body!["name"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(["[AB#202] alpha story", "[AB#201] Zulu story"], planNames);
    }

    /// <summary>Case-creation failures become warnings and suppress run creation when no cases survive.</summary>
    [Fact]
    public async Task GenerateAsync_AddsWarningsWhenCaseCreationFails()
    {
        using var scope = await TestSettingsScope.CreateAsync(ConfiguredSettings());
        using var handler = new RecordingHandler(async request =>
        {
            if (request.Path.EndsWith("get_milestones/12", StringComparison.Ordinal))
            {
                return JsonResponse("""{"milestones":[],"size":0,"limit":0}""");
            }

            if (request.Path.EndsWith("get_plans/12", StringComparison.Ordinal))
            {
                return JsonResponse("""{"plans":[],"size":0,"limit":0}""");
            }

            if (request.Path.EndsWith("get_sections/12&suite_id=34", StringComparison.Ordinal))
            {
                return JsonResponse("""{"sections":[],"size":0,"limit":0}""");
            }

            if (request.Path.EndsWith("add_section/12", StringComparison.Ordinal))
            {
                return JsonResponse("""{"id":501,"suite_id":34,"name":"[AB#300] Broken story"}""");
            }

            if (request.Path.EndsWith("add_case/501", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("""{"error":"case failure"}""", Encoding.UTF8, "application/json")
                };
            }

            if (request.Path.EndsWith("add_plan/12", StringComparison.Ordinal))
            {
                return JsonResponse("""{"id":701,"name":"[AB#300] Broken story","project_id":12}""");
            }

            throw new InvalidOperationException($"Unexpected request: {request.Path}");
        });
        using var client = new HttpClient(handler);
        var generator = new HierarchyGenerator(new TestRailClient(client, scope.Settings), new ConsoleLogService());
        var tracker = new FakeIssueTrackerClient();
        var item = new IssueTrackerItem
        {
            Reference = "AB#300",
            Title = "Broken story",
            Kind = IssueTrackerItemKind.Story,
            AcceptanceCriteriaHtml = "<ul><li>Only criterion</li></ul>"
        };

        var report = await generator.GenerateAsync(tracker, item, new HierarchyRequest
        {
            ProjectId = 12,
            SuiteId = 34
        });

        Assert.Single(report.SectionsCreated);
        Assert.Single(report.PlansCreated);
        Assert.Empty(report.RunsCreated);
        Assert.Equal(2, report.Warnings.Count);
        Assert.Contains(report.Warnings, warning => warning.Contains("Failed to create case", StringComparison.Ordinal));
        Assert.Contains(report.Warnings, warning => warning.Contains("No test cases produced", StringComparison.Ordinal));
        Assert.Empty(handler.FindRequests("add_plan_entry/"));
    }

    /// <summary>Creates a minimally configured settings object for TestRail-client unit coverage.</summary>
    private static TestRailSettings ConfiguredSettings() => new()
    {
        BaseUrl = "https://testrail.example.test",
        Username = "fixture-user",
        ApiKey = "fixture-key"
    };

    /// <summary>Creates a JSON response payload for the fake HTTP handler.</summary>
    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    /// <summary>Simple issue-tracker stub keyed by parent reference.</summary>
    private sealed class FakeIssueTrackerClient(Dictionary<string, IReadOnlyList<IssueTrackerItem>>? children = null) : IIssueTrackerClient
    {
        private readonly Dictionary<string, IReadOnlyList<IssueTrackerItem>> _children = children ?? [];

        /// <inheritdoc />
        public string DisplayName => "Fake";

        /// <inheritdoc />
        public bool CanHandle(string url) => true;

        /// <inheritdoc />
        public Task<IssueTrackerItem?> GetItemAsync(string url) => Task.FromResult<IssueTrackerItem?>(null);

        /// <inheritdoc />
        public Task<IReadOnlyList<IssueTrackerItem>> GetChildrenAsync(IssueTrackerItem item)
        {
            return Task.FromResult(_children.TryGetValue(item.Reference, out var children) ? children : (IReadOnlyList<IssueTrackerItem>)[]);
        }

        /// <inheritdoc />
        public Task<bool> IsConfiguredAsync() => Task.FromResult(true);
    }

    /// <summary>Captures HTTP requests and returns caller-supplied fake responses.</summary>
    private sealed class RecordingHandler(Func<ObservedRequest, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        private readonly List<ObservedRequest> _requests = [];

        /// <summary>Gets every intercepted request in arrival order.</summary>
        public IReadOnlyList<ObservedRequest> Requests => _requests;

        /// <summary>Returns every request whose path contains the supplied fragment.</summary>
        public IReadOnlyList<ObservedRequest> FindRequests(string pathFragment)
        {
            return _requests.Where(request => request.Path.Contains(pathFragment, StringComparison.Ordinal)).ToArray();
        }

        /// <summary>Returns the only request whose path contains the supplied fragment.</summary>
        public ObservedRequest FindSingleRequest(string pathFragment)
        {
            return Assert.Single(FindRequests(pathFragment));
        }

        /// <inheritdoc />
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
            var observed = new ObservedRequest(request.RequestUri!.AbsoluteUri, body, _requests.Select(existing => existing.Path).ToArray());
            _requests.Add(observed);
            return await responder(observed);
        }
    }

    /// <summary>Captured request metadata used by assertions and fake-response routing.</summary>
    private sealed class ObservedRequest(string path, JsonNode? body, IReadOnlyList<string> priorPaths)
    {
        /// <summary>Gets the absolute request URI string.</summary>
        public string Path { get; } = path;

        /// <summary>Gets the parsed JSON body, if the request had one.</summary>
        public JsonNode? Body { get; } = body;

        /// <summary>Gets the arrival-order count for requests whose path contains the supplied fragment.</summary>
        public int MatchingRequestOrdinal(string pathFragment)
        {
            return priorPaths.Count(existing => existing.Contains(pathFragment, StringComparison.Ordinal)) + 1;
        }
    }
}
