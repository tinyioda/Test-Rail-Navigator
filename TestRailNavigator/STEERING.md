# TestRailNavigator - Project Steering Document

## Overview

The default interface of TestRail is terrible and we're going to design a new one.

I don't get lucky, I make my own luck.

TestRailNavigator is a .NET 10 Razor Pages web application that provides a modern, streamlined interface for browsing TestRail test management data. It connects to the TestRail API to display projects, test runs, and test results with a cleaner, more intuitive user experience.

## Entity Hierarchy

```
Project
 └── Milestone(s)
      ├── Test Plan(s)
      │    └── Test Run(s)
      │         └── Test(s)
      └── Test Run(s)  (standalone, not in a plan)
           └── Test(s)
```

A **Project** has one or many **Milestones**. Each Milestone can contain **Test Plans** and/or standalone **Test Runs**. A Test Plan groups one or many Test Runs. Each Test Run contains **Tests**.

## Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 10 |
| UI | Razor Pages (ASP.NET Core) |
| HTTP Client | `HttpClient` via `IHttpClientFactory` |
| Serialization | `System.Text.Json` |
| Styling | Bootstrap (via default template) |

## Project Structure

```
TestRailNavigator/
├── Models/                         # DTOs for TestRail API responses (one class per file)
├── Services/
│   ├── TestRailClient.cs           # TestRail API client service
│   ├── SettingsService.cs          # Persists connection settings
│   └── ConsoleLogService.cs        # In-app console logging
├── Pages/
│   ├── Index.cshtml(.cs)           # Projects list
│   ├── Project.cshtml(.cs)         # Project detail (milestones panel → plans & runs)
│   ├── Milestones.cshtml(.cs)      # Milestone CRUD management
│   ├── PlanDetail.cshtml(.cs)      # Test plan detail (entries → runs → tests)
│   ├── Tests.cshtml(.cs)           # Tests in a standalone run
│   ├── Setup.cshtml(.cs)           # Connection configuration
│   └── Shared/
│       ├── _Layout.cshtml          # Layout with navbar & footer
│       └── _ConsoleWindow.cshtml   # Console log partial
├── Program.cs                      # App configuration & DI
└── STEERING.md                     # This document
```

## Configuration

### Application Authentication

All data pages and handlers require the single application's administrator identity, using
ASP.NET Core cookie authentication and a fallback administrator policy. Setup is administrator-only.
Login, Error, static assets, and the minimal `/healthz` endpoint are the only anonymous surfaces.

Use existing `SetupUsername` / `SetupPassword` values in `testrail-settings.json`, or provision
`TestRail:SetupUsername` / `TestRail:SetupPassword` through .NET configuration (environment variables
use double underscores). Missing credentials must fail closed, never enable anonymous Setup.
TestRail's shared integration role is not a substitute for authenticating the Navigator caller.

### Azure DevOps Trust Boundary

`AzureDevOpsBaseUrl` is an administrator-approved HTTPS organization or collection root.
Both it and `AzureDevOpsPat` are required. Validate the origin, port, and organization/collection
before attaching the PAT to any request, including child links. Do not follow redirects or infer
a trusted destination from a user-supplied work-item URL.

### TestRail Settings

Add the following to `appsettings.json` or use User Secrets for sensitive data:

```json
{
  "TestRail": {
    "BaseUrl": "https://yourcompany.testrail.io",
    "Username": "your-email@example.com",
    "ApiKey": "your-api-key"
  }
}
```

**Using User Secrets (recommended for development):**
```bash
dotnet user-secrets set "TestRail:BaseUrl" "https://yourcompany.testrail.io"
dotnet user-secrets set "TestRail:Username" "your-email@example.com"
dotnet user-secrets set "TestRail:ApiKey" "your-api-key"
```

## Coding Conventions

### General
- Use C# 14 features where appropriate
- Enable nullable reference types (`<Nullable>enable</Nullable>`)
- Use implicit usings (`<ImplicitUsings>enable</ImplicitUsings>`)
- Use file-scoped namespaces
- **Always use XML comments on all classes, properties, and methods**
- **Every class should be in its own file**
- **Do not ask to build the app after every change - the developer will build when ready**
- **When making a change to one page, apply the same change to all pages in the project**

### Naming
- **Classes/Records**: PascalCase (`TestRailClient`, `ProjectsResponse`)
- **Methods**: PascalCase with `Async` suffix for async methods (`GetProjectsAsync`)
- **Properties**: PascalCase (`StatusName`, `IsCompleted`)
- **Private fields**: camelCase with underscore prefix (`_httpClient`)
- **JSON mapping**: Use `[JsonPropertyName]` to match TestRail's snake_case

### Razor Pages
- Page models inherit from `PageModel`
- Use `OnGetAsync` / `OnPostAsync` naming convention
- Store page data in public properties (not ViewData when possible)
- Handle errors gracefully with `ErrorMessage` property pattern

### Services
- Register services in `Program.cs` using typed `HttpClient`
- Configuration via `IConfiguration` injection
- Throw `InvalidOperationException` for missing required configuration
- Render external Markdown only through `MarkdownRenderer`, which explicitly enables supported formatting and sanitizes the generated HTML. Do not enable generic attributes or use unsanitized output with `Html.Raw`.

## TestRail API Reference

### Base URL Format
```
{BaseUrl}/index.php?/api/v2/{endpoint}
```

### Authentication
- HTTP Basic Auth
- Username: TestRail email
- Password: API key (generate in TestRail > My Settings > API Keys)

### Endpoints Used

| Endpoint | Method | Description |
|----------|--------|-------------|
| `get_projects` | GET | List all projects |
| `get_project/{id}` | GET | Get single project |
| `get_runs/{project_id}` | GET | List runs in project |
| `get_run/{id}` | GET | Get single run |
| `get_tests/{run_id}` | GET | List tests in run |
| `get_results/{test_id}` | GET | Get results for test |

### Status IDs
| ID | Status |
|----|--------|
| 1 | Passed |
| 2 | Blocked |
| 3 | Untested |
| 4 | Retest |
| 5 | Failed |

## Navigation Flow

```
Index (Projects)
 └── Project (Milestones panel + Plans & Runs)
      ├── PlanDetail (Plan entries with inline runs & tests)
      └── Tests (Tests in a standalone run)
```

## Future Enhancements

- [ ] Add pagination for large datasets
- [ ] Add search/filter functionality
- [ ] Add test result details page
- [ ] Add caching for API responses
- [x] Add single-admin authentication/authorization
- [ ] Add ability to update test results

## Dependencies

| Package | Purpose |
|---------|---------|
| `Microsoft.VisualStudio.Azure.Containers.Tools.Targets` | Docker support |

## Running the Application

```bash
cd TestRailNavigator
dotnet run
```

Navigate to `https://localhost:{port}` to view the application.

## Docker Support

The project includes Docker configuration. Build and run with:

```bash
docker build -t testrailnavigator .
docker run -p 8080:80 testrailnavigator
```
