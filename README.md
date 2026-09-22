# TestRail Navigator

A modern, streamlined web interface for [TestRail](https://www.testrail.com/) test management — built because the default one deserves better.

TestRail Navigator connects to your TestRail instance via its REST API and provides a cleaner way to browse projects, milestones, test plans, runs, and results.

![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)
![Razor Pages](https://img.shields.io/badge/UI-Razor%20Pages-blue)
![Docker](https://img.shields.io/badge/Docker-supported-blue)
![License](https://img.shields.io/badge/license-MIT-green)

---

## Features

- **Project Dashboard** — Card-based project overview with active/completed status
- **Milestone Management** — Create, edit, and organize milestones within projects
- **Test Plan & Run Browsing** — Navigate the full hierarchy: Plans → Runs → Tests
- **Administrator Sign-In** — Every data page and handler requires the provisioned single-admin credentials
- **TestRail Permissions** — Resolves the shared TestRail account's role (Read-only → Tester → Designer → Lead → Admin) and gates UI actions accordingly
- **In-App Setup** — Configure your TestRail connection directly from the browser (no config files required)
- **Console Log** — Development-mode console window for debugging API calls
- **Create Projects** — Spin up new TestRail projects without leaving the app
- **Docker Ready** — Ship it anywhere with the included multi-stage Dockerfile

## Entity Hierarchy

```
Project
 └── Milestone(s)
      ├── Test Plan(s)
      │    └── Test Run(s)
      │         └── Test(s)
      └── Test Run(s)  (standalone)
           └── Test(s)
```

## Tech Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 10 (ASP.NET Core) |
| UI | Razor Pages |
| Styling | Bootstrap 5 + Bootstrap Icons |
| HTTP Client | `HttpClient` via `IHttpClientFactory` |
| Serialization | `System.Text.Json` |
| Containerization | Docker (multi-stage build) |

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A TestRail instance with API access enabled
- A TestRail API key (generate at **My Settings → API Keys** in TestRail)

### Run Locally

```bash
git clone https://github.com/tinyioda/Test-Rail-Navigator.git
cd Test-Rail-Navigator/TestRailNavigator
dotnet user-secrets set "TestRail:SetupUsername" "admin"
dotnet user-secrets set "TestRail:SetupPassword" "<choose-a-strong-password>"
dotnet run --environment Development
```

Navigate to the address printed by `dotnet run`. Sign in with the administrator credentials, then open **Setup** to configure the TestRail connection. HTTP is supported for local Development only; use HTTPS for deployed instances.

### Run with Docker

```bash
docker build -f TestRailNavigator/Dockerfile -t testrail-navigator .
docker run -p 127.0.0.1:8080:8080 \
  -e TestRail__SetupUsername=admin \
  -e TestRail__SetupPassword="<choose-a-strong-password>" \
  testrail-navigator
```

For production, expose the container through a correctly configured HTTPS reverse proxy. Authentication cookies are always Secure outside Development. Provision writable data and Data Protection key storage for the non-root container user; set `DataProtection__KeysPath` to the persistent key directory. Do not expose an unconfigured instance or commit deployment credentials.

## Configuration

TestRail Navigator is a **single-admin application**, not a per-user TestRail sign-in service. Anyone holding the administrator credentials has access to the configured integrations and Setup.

### Administrator Credentials

Existing `SetupUsername` and `SetupPassword` values in `testrail-settings.json` now protect the entire application. Alternatively, provision `TestRail:SetupUsername` and `TestRail:SetupPassword` through .NET configuration: user secrets in Development, or `TestRail__SetupUsername` and `TestRail__SetupPassword` environment variables in production. Configuration values take precedence over the settings file.

Both credentials are required. If either is missing, sign-in is unavailable and all data pages remain protected; there is no anonymous Setup or account-registration endpoint. Administrator sessions expire after 30 minutes of inactivity. Failed sign-in attempts are limited per client IP. Use **Sign out** in the shared navigation to end a session.

Restart after editing credentials directly in the settings file. Previously issued cookies are rejected when the effective credentials change. Use a shared, protected Data Protection key store if running multiple replicas.

### In-App Connection Setup

After signing in, navigate to `/Setup` and enter:
- **Base URL** — Your TestRail instance URL (e.g., `https://yourcompany.testrail.io`)
- **Username** — Your TestRail email
- **API Key** — Your TestRail API key

Settings are persisted to `testrail-settings.json` (gitignored by default).

### Azure DevOps

Configure both **Azure DevOps base URL** (`AzureDevOpsBaseUrl`) and a **PAT** (`AzureDevOpsPat`) with Work Items (Read) scope. The base must be the approved HTTPS organization or collection, without a project, work-item path, query, fragment, or embedded credentials. Examples:

- `https://dev.azure.com/your-organization`
- `https://your-organization.visualstudio.com`
- `https://ado.example.com/tfs/DefaultCollection`

Work-item links and hierarchy child links must match that origin, port, and organization/collection path. Other destinations are rejected before sending credentials. HTTP and redirects are not supported; use the server's canonical HTTPS URL.

### Helm

Supply `testrail.setupUsername` and `testrail.setupPassword` through a protected values file or deployment secret workflow. Azure DevOps generation additionally requires `testrail.azureDevOpsBaseUrl` and `testrail.azureDevOpsPat`. The chart mounts integration settings read-only: update those values through deployment configuration rather than saving Setup. Health probes use the anonymous `/healthz` endpoint, which does not query the integrations.

> ⚠️ Never commit credentials. The `.gitignore` excludes `testrail-settings.json` and `launchSettings.json`.

## Project Structure

```
Test-Rail-Navigator/
├── .github/
│   └── copilot-instructions.md     # Copilot coding guidelines
├── CHANGELOG.md                    # Release and migration notes
├── TestRailNavigator/
│   ├── Models/                     # DTOs for TestRail API responses
│   ├── Services/
│   │   ├── AdminAuthenticationService.cs # Single-admin authentication
│   │   ├── AzureDevOpsUrlPolicy.cs # Approved destination validation
│   │   ├── MarkdownRenderer.cs    # Sanitized Markdown rendering
│   │   ├── TestRailClient.cs       # TestRail REST API client
│   │   ├── SettingsService.cs      # Connection settings persistence
│   │   ├── PermissionService.cs    # Role-based permission resolver
│   │   ├── ConsoleLogService.cs    # In-app development console
│   │   └── TestRailPermissions.cs  # Permission model & role mapping
│   ├── Pages/
│   │   ├── Login.cshtml            # Administrator sign-in
│   │   ├── Logout.cshtml           # Antiforgery-protected sign-out
│   │   ├── Index.cshtml            # Project dashboard
│   │   ├── Project.cshtml          # Project detail (milestones + plans/runs)
│   │   ├── Milestones.cshtml       # Milestone CRUD management
│   │   ├── PlanDetail.cshtml       # Test plan detail (runs + tests)
│   │   ├── Tests.cshtml            # Tests in a standalone run
│   │   ├── Setup.cshtml            # Connection configuration
│   │   └── Shared/
│   │       ├── _Layout.cshtml      # App shell (navbar + footer)
│   │       └── _ConsoleWindow.cshtml  # Dev console partial
│   ├── wwwroot/                    # Static assets (CSS, JS, favicon)
│   ├── Program.cs                  # DI & middleware configuration
│   ├── Dockerfile                  # Multi-stage Docker build
│   └── STEERING.md                 # Internal design & coding conventions
├── TestRailNavigator.Tests/        # Isolated security regression coverage
└── TestRailNavigator.slnx          # Solution file
```

## Permissions

After administrator authentication, TestRail Navigator detects the **shared integration account's** TestRail role and adjusts the UI. These are backend capabilities, not separate roles for individual Navigator visitors:

| Role | Read | Add Results | Manage Cases | Manage Runs/Plans | Admin |
|------|:----:|:-----------:|:------------:|:-----------------:|:-----:|
| Read-only | ✅ | — | — | — | — |
| Tester | ✅ | ✅ | — | — | — |
| Designer | ✅ | ✅ | ✅ | — | — |
| Lead | ✅ | ✅ | ✅ | ✅ | — |
| Admin | ✅ | ✅ | ✅ | ✅ | ✅ |

If the current user can't be resolved, the app defaults to **read-only** mode.

## Testing

```bash
dotnet test TestRailNavigator.slnx
```

The security regression suite uses isolated temporary settings, in-process hosting, and fake HTTP handlers. It does not use real TestRail or Azure DevOps credentials.

## TestRail API

The app uses the [TestRail API v2](https://support.testrail.com/hc/en-us/articles/7077039051284-Accessing-the-TestRail-API) with HTTP Basic authentication.

**Base URL format:** `{BaseUrl}/index.php?/api/v2/{endpoint}`

Key endpoints used:

| Endpoint | Description |
|----------|-------------|
| `get_projects` | List all projects |
| `get_project/{id}` | Get project details |
| `get_milestones/{project_id}` | List milestones |
| `get_plans/{project_id}` | List test plans |
| `get_runs/{project_id}` | List test runs |
| `get_tests/{run_id}` | List tests in a run |
| `get_results/{test_id}` | Get test results |
| `get_current_user` | Resolve permissions |

## Roadmap

- [ ] Pagination for large datasets
- [ ] Search and filter functionality
- [ ] Test result details page
- [ ] API response caching
- [x] Single-admin authentication / authorization layer
- [ ] Ability to update test results from the UI

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Follow the conventions in [`STEERING.md`](TestRailNavigator/STEERING.md)
4. Submit a pull request

**Brand colors:** Teal (`#00A3AD`) and Black (`#1A1A1A`) — use the CSS custom properties `--brand-teal` and `--brand-dark`.

## License

This project is open source. See the repository for license details.

## Breaking Changes

> Quick-reference for compatibility. See [CHANGELOG.md](./CHANGELOG.md) for full details.

| Version | Change | Migration Path |
|---------|--------|----------------|
| Unreleased | All data pages and handlers require single-admin sign-in, including read-only access. | Provision both existing Setup credentials or their `TestRail:` configuration overrides before upgrading. |
| Unreleased | Production authentication cookies require HTTPS. | Configure TLS and correctly forward the request scheme through any trusted reverse proxy. |
| Unreleased | Azure DevOps requires an approved HTTPS base URL as well as a PAT; HTTP and redirects are rejected. | Set `AzureDevOpsBaseUrl` in Setup or `testrail.azureDevOpsBaseUrl` in Helm, matching the origin, port, and case-sensitive organization/collection path of work-item links. |
| Unreleased | Markdown generic attributes and unapproved advanced extensions are no longer interpreted. | Use supported headings, lists, tables, links, images, fenced code, strikethrough, and task lists instead of custom attributes. |
| Unreleased | Data pages are no longer suitable for anonymous health probes. | Point monitoring at `/healthz`; the bundled Helm defaults already use it. |
