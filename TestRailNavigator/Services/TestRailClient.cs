using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TestRailNavigator.Models;

namespace TestRailNavigator.Services;

/// <summary>
/// Client for interacting with the TestRail API.
/// </summary>
public class TestRailClient
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly SemaphoreSlim _configLock = new(1, 1);
    private string _apiBase = string.Empty;
    private bool _isConfigured;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestRailClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client instance.</param>
    /// <param name="settingsService">The settings service.</param>
    public TestRailClient(HttpClient httpClient, SettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Ensures the client is configured with valid settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when settings are not configured.</exception>
    private async Task EnsureConfiguredAsync()
    {
        if (_isConfigured)
        {
            return;
        }

        await _configLock.WaitAsync();
        try
        {
            if (_isConfigured)
            {
                return;
            }

            var settings = await _settingsService.GetSettingsAsync()
                ?? throw new InvalidOperationException("TestRail settings not configured");

            _apiBase = $"{settings.BaseUrl.TrimEnd('/')}/index.php?/api/v2/";

            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{settings.Username}:{settings.ApiKey}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            _isConfigured = true;
        }
        finally
        {
            _configLock.Release();
        }
    }

    /// <summary>
    /// Ensures the HTTP response was successful, extracting the TestRail error message on failure.
    /// </summary>
    /// <param name="response">The HTTP response to validate.</param>
    /// <exception cref="HttpRequestException">Thrown with the TestRail error message when the response indicates failure.</exception>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        string? errorMessage = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                errorMessage = err.GetString();
            }
        }
        catch
        {
            // Response body was not valid JSON
        }

        throw new HttpRequestException(
            errorMessage ?? $"TestRail API returned {(int)response.StatusCode} {response.ReasonPhrase}",
            null,
            response.StatusCode);
    }

    /// <summary>
    /// Reads all pages of a paginated TestRail API response.
    /// </summary>
    /// <typeparam name="T">The item type in the collection.</typeparam>
    /// <param name="url">The initial API URL (without offset).</param>
    /// <param name="collectionKey">The JSON property name containing the array (e.g. "projects", "runs").</param>
    /// <returns>A list of all items across all pages.</returns>
    private async Task<List<T>> GetAllPaginatedAsync<T>(string url, string collectionKey)
    {
        var allItems = new List<T>();
        var offset = 0;

        while (true)
        {
            var pagedUrl = offset == 0 ? url : $"{url}&offset={offset}";
            var response = await _httpClient.GetAsync(pagedUrl);
            await EnsureSuccessAsync(response);

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            if (doc.RootElement.TryGetProperty(collectionKey, out var itemsElement))
            {
                var page = itemsElement.Deserialize<List<T>>(_jsonOptions) ?? [];
                allItems.AddRange(page);
            }

            var size = doc.RootElement.TryGetProperty("size", out var s) ? s.GetInt32() : 0;
            var limit = doc.RootElement.TryGetProperty("limit", out var l) ? l.GetInt32() : 250;

            // Stop when: the page is smaller than the limit (last page),
            // or limit/size is zero (all results returned at once).
            if (limit == 0 || size == 0 || size < limit)
            {
                break;
            }

            offset += limit;
        }

        return allItems;
    }

    /// <summary>
    /// Gets the currently authenticated TestRail user.
    /// </summary>
    /// <returns>The current user, or null if the request fails.</returns>
    public async Task<TestRailUser?> GetCurrentUserAsync()
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.GetAsync($"{_apiBase}get_current_user");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestRailUser>(_jsonOptions);
    }

    /// <summary>
    /// Gets all projects from TestRail.
    /// </summary>
    /// <returns>A list of projects.</returns>
    public async Task<List<Project>> GetProjectsAsync()
    {
        await EnsureConfiguredAsync();
        return await GetAllPaginatedAsync<Project>($"{_apiBase}get_projects", "projects");
    }

    /// <summary>
    /// Gets a specific project by identifier.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>The project, or null if not found.</returns>
    public async Task<Project?> GetProjectAsync(int projectId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.GetAsync($"{_apiBase}get_project/{projectId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Project>(_jsonOptions);
    }

    /// <summary>
    /// Gets all test runs for a specific project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>A list of test runs.</returns>
    public async Task<List<TestRun>> GetRunsAsync(int projectId)
    {
        await EnsureConfiguredAsync();
        return await GetAllPaginatedAsync<TestRun>($"{_apiBase}get_runs/{projectId}", "runs");
    }

    /// <summary>
    /// Gets all test runs for a specific project filtered by milestone.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="milestoneId">The milestone identifier to filter by.</param>
    /// <returns>A list of test runs associated with the milestone.</returns>
    public async Task<List<TestRun>> GetRunsForMilestoneAsync(int projectId, int milestoneId)
    {
        await EnsureConfiguredAsync();
        return await GetAllPaginatedAsync<TestRun>($"{_apiBase}get_runs/{projectId}&milestone_id={milestoneId}", "runs");
    }

    /// <summary>
    /// Gets a specific test run by identifier.
    /// </summary>
    /// <param name="runId">The test run identifier.</param>
    /// <returns>The test run, or null if not found.</returns>
    public async Task<TestRun?> GetRunAsync(int runId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.GetAsync($"{_apiBase}get_run/{runId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestRun>(_jsonOptions);
    }

    /// <summary>
    /// Gets a specific test by identifier.
    /// </summary>
    /// <param name="testId">The test identifier.</param>
    /// <returns>The test, or null if not found.</returns>
    public async Task<Test?> GetTestAsync(int testId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.GetAsync($"{_apiBase}get_test/{testId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Test>(_jsonOptions);
    }

    /// <summary>
    /// Gets all tests for a specific test run.
    /// </summary>
    /// <param name="runId">The test run identifier.</param>
    /// <returns>A list of tests.</returns>
    public async Task<List<Test>> GetTestsAsync(int runId)
    {
        await EnsureConfiguredAsync();
        return await GetAllPaginatedAsync<Test>($"{_apiBase}get_tests/{runId}", "tests");
    }

    /// <summary>
    /// Gets all results for a specific test.
    /// </summary>
    /// <param name="testId">The test identifier.</param>
    /// <returns>A list of test results.</returns>
    public async Task<List<TestResult>> GetResultsAsync(int testId)
    {
        await EnsureConfiguredAsync();
        return await GetAllPaginatedAsync<TestResult>($"{_apiBase}get_results/{testId}", "results");
    }

    /// <summary>
    /// Adds a test result for a specific case within a run.
    /// </summary>
    /// <param name="runId">The test run identifier.</param>
    /// <param name="caseId">The test case identifier.</param>
    /// <param name="request">The result to add.</param>
    /// <returns>The created test result.</returns>
    public async Task<TestResult?> AddResultForCaseAsync(int runId, int caseId, AddResultRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}add_result_for_case/{runId}/{caseId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestResult>(_jsonOptions);
    }

    /// <summary>
    /// Gets all milestones for a specific project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>A list of milestones.</returns>
    public async Task<List<Milestone>> GetMilestonesAsync(int projectId)
    {
        await EnsureConfiguredAsync();
        return await GetAllPaginatedAsync<Milestone>($"{_apiBase}get_milestones/{projectId}", "milestones");
    }

    /// <summary>
    /// Gets a specific milestone by identifier.
    /// </summary>
    /// <param name="milestoneId">The milestone identifier.</param>
    /// <returns>The milestone, or null if not found.</returns>
    public async Task<Milestone?> GetMilestoneAsync(int milestoneId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.GetAsync($"{_apiBase}get_milestone/{milestoneId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Milestone>(_jsonOptions);
    }

    /// <summary>
    /// Creates a new milestone in a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="request">The milestone creation request.</param>
    /// <returns>The created milestone.</returns>
    public async Task<Milestone?> AddMilestoneAsync(int projectId, MilestoneRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}add_milestone/{projectId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Milestone>(_jsonOptions);
    }

    /// <summary>
    /// Updates an existing milestone.
    /// </summary>
    /// <param name="milestoneId">The milestone identifier.</param>
    /// <param name="request">The milestone update request.</param>
    /// <returns>The updated milestone.</returns>
    public async Task<Milestone?> UpdateMilestoneAsync(int milestoneId, MilestoneRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}update_milestone/{milestoneId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Milestone>(_jsonOptions);
    }

    /// <summary>
    /// Deletes a milestone.
    /// </summary>
    /// <param name="milestoneId">The milestone identifier.</param>
    public async Task DeleteMilestoneAsync(int milestoneId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync<object>($"{_apiBase}delete_milestone/{milestoneId}", new { }, _jsonOptions);
        await EnsureSuccessAsync(response);
    }

    /// <summary>
    /// Creates a new project in TestRail.
    /// </summary>
    /// <param name="request">The project creation request.</param>
    /// <returns>The created project.</returns>
    public async Task<Project?> CreateProjectAsync(CreateProjectRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}add_project", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Project>(_jsonOptions);
    }

    /// <summary>
    /// Gets all test suites for a specific project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>A list of suites.</returns>
    /// <remarks>
    /// The get_suites endpoint may return a plain JSON array (older API) or
    /// a paginated object wrapper (newer API). This method handles both formats.
    /// </remarks>
    public async Task<List<Suite>> GetSuitesAsync(int projectId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.GetAsync($"{_apiBase}get_suites/{projectId}");
        await EnsureSuccessAsync(response);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        // Older API versions return a plain JSON array
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<Suite>>(json, _jsonOptions) ?? [];
        }

        // Newer API returns a paginated object wrapper
        if (doc.RootElement.TryGetProperty("suites", out var suitesElement))
        {
            return suitesElement.Deserialize<List<Suite>>(_jsonOptions) ?? [];
        }

        return [];
    }

    /// <summary>
    /// Gets a specific test suite by identifier.
    /// </summary>
    /// <param name="suiteId">The suite identifier.</param>
    /// <returns>The suite, or null if not found.</returns>
    public async Task<Suite?> GetSuiteAsync(int suiteId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.GetAsync($"{_apiBase}get_suite/{suiteId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Suite>(_jsonOptions);
    }

    /// <summary>
    /// Gets all test plans for a specific project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <returns>A list of test plans.</returns>
    public async Task<List<TestPlan>> GetPlansAsync(int projectId)
    {
        await EnsureConfiguredAsync();
        return await GetAllPaginatedAsync<TestPlan>($"{_apiBase}get_plans/{projectId}", "plans");
    }

    /// <summary>
    /// Gets a specific test plan by identifier, including its entries and runs.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <returns>The test plan with entries, or null if not found.</returns>
    public async Task<TestPlan?> GetPlanAsync(int planId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.GetAsync($"{_apiBase}get_plan/{planId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestPlan>(_jsonOptions);
    }

    /// <summary>
    /// Gets all test plans for a specific project filtered by milestone.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="milestoneId">The milestone identifier to filter by.</param>
    /// <returns>A list of test plans associated with the milestone.</returns>
    public async Task<List<TestPlan>> GetPlansForMilestoneAsync(int projectId, int milestoneId)
    {
        await EnsureConfiguredAsync();
        return await GetAllPaginatedAsync<TestPlan>($"{_apiBase}get_plans/{projectId}&milestone_id={milestoneId}", "plans");
    }

    /// <summary>
    /// Creates a new test plan in a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="request">The test plan creation request.</param>
    /// <returns>The created test plan.</returns>
    public async Task<TestPlan?> AddPlanAsync(int projectId, TestPlanRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}add_plan/{projectId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestPlan>(_jsonOptions);
    }

    /// <summary>
    /// Updates an existing test plan.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <param name="request">The test plan update request.</param>
    /// <returns>The updated test plan.</returns>
    public async Task<TestPlan?> UpdatePlanAsync(int planId, TestPlanRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}update_plan/{planId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestPlan>(_jsonOptions);
    }

    /// <summary>
    /// Deletes a test plan.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    public async Task DeletePlanAsync(int planId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync<object>($"{_apiBase}delete_plan/{planId}", new { }, _jsonOptions);
        await EnsureSuccessAsync(response);
    }

    /// <summary>
    /// Adds a new entry (suite + runs) to an existing test plan.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <param name="request">The plan entry creation request.</param>
    /// <returns>The created plan entry.</returns>
    public async Task<TestPlanEntry?> AddPlanEntryAsync(int planId, TestPlanEntryRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}add_plan_entry/{planId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestPlanEntry>(_jsonOptions);
    }

    /// <summary>
    /// Gets all test cases for a specific project and suite.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="suiteId">The suite identifier. Required for multi-suite projects.</param>
    /// <returns>A list of test cases.</returns>
    public async Task<List<TestCase>> GetCasesAsync(int projectId, int? suiteId = null)
    {
        await EnsureConfiguredAsync();
        var url = $"{_apiBase}get_cases/{projectId}";
        if (suiteId.HasValue)
        {
            url += $"&suite_id={suiteId.Value}";
        }

        return await GetAllPaginatedAsync<TestCase>(url, "cases");
    }

    /// <summary>
    /// Gets all sections for a specific project and suite.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="suiteId">The suite identifier.</param>
    /// <returns>A list of sections.</returns>
    public async Task<List<Section>> GetSectionsAsync(int projectId, int suiteId)
    {
        await EnsureConfiguredAsync();
        var url = $"{_apiBase}get_sections/{projectId}&suite_id={suiteId}";
        return await GetAllPaginatedAsync<Section>(url, "sections");
    }

    /// <summary>
    /// Creates a new test case in the specified section.
    /// </summary>
    /// <param name="sectionId">The section identifier to create the case in.</param>
    /// <param name="request">The test case creation request.</param>
    /// <returns>The created test case.</returns>
    public async Task<TestCase?> AddCaseAsync(int sectionId, AddTestCaseRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}add_case/{sectionId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestCase>(_jsonOptions);
    }

    /// <summary>
    /// Gets a specific test case by identifier.
    /// </summary>
    /// <param name="caseId">The test case identifier.</param>
    /// <returns>The test case, or null if not found.</returns>
    public async Task<TestCase?> GetCaseAsync(int caseId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.GetAsync($"{_apiBase}get_case/{caseId}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestCase>(_jsonOptions);
    }

    /// <summary>
    /// Updates an existing test case.
    /// </summary>
    /// <param name="caseId">The test case identifier.</param>
    /// <param name="request">The update request containing the fields to change.</param>
    /// <returns>The updated test case.</returns>
    public async Task<TestCase?> UpdateCaseAsync(int caseId, UpdateTestCaseRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}update_case/{caseId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestCase>(_jsonOptions);
    }

    /// <summary>
    /// Creates a new standalone test run in a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="request">The create run request.</param>
    /// <returns>The created test run.</returns>
    public async Task<TestRun?> AddRunAsync(int projectId, CreateRunRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}add_run/{projectId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestRun>(_jsonOptions);
    }

    /// <summary>
    /// Updates a standalone test run's case selection.
    /// </summary>
    /// <param name="runId">The test run identifier.</param>
    /// <param name="request">The update request containing case identifiers.</param>
    /// <returns>The updated test run.</returns>
    public async Task<TestRun?> UpdateRunAsync(int runId, UpdateRunRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}update_run/{runId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestRun>(_jsonOptions);
    }

    /// <summary>
    /// Closes (completes) a test run. This action cannot be undone.
    /// </summary>
    /// <param name="runId">The test run identifier.</param>
    /// <returns>The closed test run.</returns>
    public async Task<TestRun?> CloseRunAsync(int runId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}close_run/{runId}", new { }, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestRun>(_jsonOptions);
    }

    /// <summary>
    /// Deletes an existing test run. This action cannot be undone.
    /// </summary>
    /// <param name="runId">The test run identifier.</param>
    public async Task DeleteRunAsync(int runId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}delete_run/{runId}", new { }, _jsonOptions);
        await EnsureSuccessAsync(response);
    }

    /// <summary>
    /// Updates a plan entry's case selection. Used for runs that belong to a test plan.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <param name="entryId">The plan entry identifier.</param>
    /// <param name="request">The update request containing case identifiers.</param>
    /// <returns>The updated plan entry.</returns>
    public async Task<TestPlanEntry?> UpdatePlanEntryAsync(int planId, string entryId, UpdateRunRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}update_plan_entry/{planId}/{entryId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestPlanEntry>(_jsonOptions);
    }

    /// <summary>
    /// Renames a plan entry (test run) within a test plan.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <param name="entryId">The plan entry identifier.</param>
    /// <param name="newName">The new name for the entry.</param>
    /// <returns>The updated plan entry.</returns>
    public async Task<TestPlanEntry?> RenamePlanEntryAsync(int planId, string entryId, string newName)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}update_plan_entry/{planId}/{entryId}", new { name = newName }, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestPlanEntry>(_jsonOptions);
    }

    /// <summary>
    /// Deletes a plan entry (test run) from a test plan.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <param name="entryId">The plan entry identifier.</param>
    public async Task DeletePlanEntryAsync(int planId, string entryId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync<object>($"{_apiBase}delete_plan_entry/{planId}/{entryId}", new { }, _jsonOptions);
        await EnsureSuccessAsync(response);
    }

    /// <summary>
    /// Closes (completes) a test plan. This action cannot be undone.
    /// </summary>
    /// <param name="planId">The test plan identifier.</param>
    /// <returns>The closed test plan.</returns>
    public async Task<TestPlan?> ClosePlanAsync(int planId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}close_plan/{planId}", new { }, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TestPlan>(_jsonOptions);
    }

    /// <summary>
    /// Updates an existing project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="request">The project update request.</param>
    /// <returns>The updated project.</returns>
    public async Task<Project?> UpdateProjectAsync(int projectId, CreateProjectRequest request)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync($"{_apiBase}update_project/{projectId}", request, _jsonOptions);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Project>(_jsonOptions);
    }

    /// <summary>
    /// Deletes a project. This action cannot be undone.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    public async Task DeleteProjectAsync(int projectId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync<object>($"{_apiBase}delete_project/{projectId}", new { }, _jsonOptions);
        await EnsureSuccessAsync(response);
    }

    /// <summary>
    /// Deletes an existing test case. This action cannot be undone.
    /// </summary>
    /// <param name="caseId">The test case identifier.</param>
    public async Task DeleteCaseAsync(int caseId)
    {
        await EnsureConfiguredAsync();
        var response = await _httpClient.PostAsJsonAsync<object>($"{_apiBase}delete_case/{caseId}", new { }, _jsonOptions);
        await EnsureSuccessAsync(response);
    }
}
