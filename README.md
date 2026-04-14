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
- **Role-Based Permissions** — Automatically resolves your TestRail role (Read-only → Tester → Designer → Lead → Admin) and gates UI actions accordingly
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
dotnet run
```

Navigate to `https://localhost:{port}` — the app will redirect you to the **Setup** page on first launch to configure your TestRail connection.

### Run with Docker

```bash
docker build -t testrail-navigator .
docker run -p 8080:8080 testrail-navigator
```

Open `http://localhost:8080` and configure your connection via the Setup page.

## Configuration

TestRail Navigator supports two configuration methods:

### Option 1: In-App Setup (Recommended)

Navigate to `/Setup` in the browser and enter:
- **Base URL** — Your TestRail instance URL (e.g., `https://yourcompany.testrail.io`)
- **Username** — Your TestRail email
- **API Key** — Your TestRail API key

Settings are persisted to `testrail-settings.json` (gitignored by default).

### Option 2: User Secrets (Development)

```bash
cd TestRailNavigator
dotnet user-secrets set "TestRail:BaseUrl" "https://yourcompany.testrail.io"
dotnet user-secrets set "TestRail:Username" "your-email@example.com"
dotnet user-secrets set "TestRail:ApiKey" "your-api-key"
```

> ⚠️ Never commit credentials. The `.gitignore` excludes `testrail-settings.json` and `launchSettings.json`.

## Project Structure

```
Test-Rail-Navigator/
├── .github/
│   └── copilot-instructions.md     # Copilot coding guidelines
├── TestRailNavigator/
│   ├── Models/                     # 26 DTOs for TestRail API responses
│   ├── Services/
│   │   ├── TestRailClient.cs       # TestRail REST API client
│   │   ├── SettingsService.cs      # Connection settings persistence
│   │   ├── PermissionService.cs    # Role-based permission resolver
│   │   ├── ConsoleLogService.cs    # In-app development console
│   │   └── TestRailPermissions.cs  # Permission model & role mapping
│   ├── Pages/
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
└── TestRailNavigator.slnx          # Solution file
```

## Permissions

TestRail Navigator automatically detects your TestRail role and adjusts the UI:

| Role | Read | Add Results | Manage Cases | Manage Runs/Plans | Admin |
|------|:----:|:-----------:|:------------:|:-----------------:|:-----:|
| Read-only | ✅ | — | — | — | — |
| Tester | ✅ | ✅ | — | — | — |
| Designer | ✅ | ✅ | ✅ | — | — |
| Lead | ✅ | ✅ | ✅ | ✅ | — |
| Admin | ✅ | ✅ | ✅ | ✅ | ✅ |

If the current user can't be resolved, the app defaults to **read-only** mode.

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
- [ ] Authentication / authorization layer
- [ ] Ability to update test results from the UI

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Follow the conventions in [`STEERING.md`](TestRailNavigator/STEERING.md)
4. Submit a pull request

**Brand colors:** Teal (`#00A3AD`) and Black (`#1A1A1A`) — use the CSS custom properties `--brand-teal` and `--brand-dark`.

## License

This project is open source. See the repository for license details.
