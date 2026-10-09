# TestRail Navigator

A modern, streamlined web interface for [TestRail](https://www.testrail.com/) test management — built because the default one deserves better.

TestRail Navigator connects to your TestRail instance via its REST API and provides a cleaner way to browse projects, milestones, test plans, runs, and results.

![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)
![Razor Pages](https://img.shields.io/badge/UI-Razor%20Pages-blue)
![License](https://img.shields.io/badge/license-MIT-green)

---

## Features

- **Project Dashboard** — Card-based project overview with active/completed status
- **Milestone Management** — Create, edit, and organize milestones within projects
- **Test Plan & Run Browsing** — Navigate the full hierarchy: Plans → Runs → Tests
- **Per-User Sign-In** — Users sign in with their own TestRail username/password, validated live against TestRail
- **TestRail Permissions** — Resolves each signed-in user's own TestRail role (Read-only → Tester → Designer → Lead → Admin) and gates UI actions accordingly
- **In-App Setup** — Configure your TestRail connection directly from the browser (no config files required)
- **Console Log** — Development-mode console window for debugging API calls
- **Create Projects** — Spin up new TestRail projects without leaving the app

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

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A TestRail instance with API access enabled
- A TestRail API key (generate at **My Settings → API Keys** in TestRail)

### Run Locally

```bash
git clone https://github.com/tinyioda/Test-Rail-Navigator.git
cd Test-Rail-Navigator/TestRailNavigator
dotnet run --environment Development
```

Navigate to the address printed by `dotnet run`. The first page you'll see is **Setup** — configure the TestRail connection (Base URL, a service-account username, and API key) so the app knows which TestRail instance to validate sign-ins against. Then sign in with **your own** TestRail username and password; TestRail Navigator validates them live against TestRail and uses your TestRail role/permissions inside the app. HTTP is supported for local Development only; use HTTPS for deployed instances.

## Configuration

TestRail Navigator authenticates **each user individually**: everyone signs in with their own TestRail username and password, which are validated live against the TestRail instance on every sign-in. There is no separate app-level administrator account or password to provision.

### Sign-In

Sign-in is only available once the TestRail connection is configured on `/Setup` (see below) — there is no TestRail instance to validate credentials against otherwise. Enter your TestRail username and password on the **Login** page; TestRail Navigator calls TestRail's `get_current_user` endpoint to confirm they're valid and to resolve your TestRail role. Your credentials are kept only in an in-memory, server-side session for the duration of your sign-in — they are never written to disk. Sessions expire after 30 minutes of inactivity, are cleared on **Sign out**, and are invalidated if the app restarts. Failed sign-in attempts are limited per client IP.

Use a shared, protected Data Protection key store if running multiple replicas.

### In-App Connection Setup

`/Setup` is reachable without signing in — there has to be somewhere to configure which TestRail instance to validate sign-ins against. Enter:
- **Base URL** — Your TestRail instance URL (e.g., `https://yourcompany.testrail.io`)
- **Username** — A TestRail account email (used as the fallback/service-account identity for unauthenticated contexts)
- **API Key** — That account's TestRail API key

These three values are written to the `TestRail` section of `appsettings.json` at runtime. All remaining Setup fields (write mode, console) continue to persist to `testrail-settings.json` (gitignored by default).

**`/Setup` is only reachable until the connection is configured.** Before `BaseUrl`/`Username`/`ApiKey` are set, every other page redirects there, and sign-in is unavailable. Once set, `/Setup` itself redirects to the dashboard for everyone — to change the connection afterward, edit the `TestRail` section of `appsettings.json` directly (or clear it to reopen Setup).

### IIS Deployment

Use the included `deploy.ps1` script to publish and deploy to an IIS site:

```powershell
.\deploy.ps1
```

This publishes the app in Release mode, takes the site offline, mirrors the output to `C:\inetpub\TestRailNavigator` (preserving `testrail-settings.json`, the SQLite database, and `logs\`), recycles the app pool, and performs a warm-up request. Configure the IIS site/app pool, then visit `/Setup` after deployment to configure the TestRail connection. Health probes should target the anonymous `/healthz` endpoint.

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
│   │   ├── AdminAuthenticationService.cs # Live, per-user TestRail authentication
│   │   ├── MarkdownRenderer.cs    # Sanitized Markdown rendering
│   │   ├── TestRailClient.cs       # TestRail REST API client
│   │   ├── SettingsService.cs      # Connection settings persistence
│   │   ├── PermissionService.cs    # Role-based permission resolver
│   │   ├── ConsoleLogService.cs    # In-app development console
│   │   └── TestRailPermissions.cs  # Permission model & role mapping
│   ├── Pages/
│   │   ├── Login.cshtml            # Per-user TestRail sign-in
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
│   └── STEERING.md                 # Internal design & coding conventions
├── TestRailNavigator.Tests/        # Isolated security regression coverage
└── TestRailNavigator.slnx          # Solution file
```

## Permissions

After signing in, TestRail Navigator detects **your own** TestRail role and adjusts the UI accordingly — these are TestRail's own permissions, not a separate role system for Navigator visitors:

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

The security regression suite uses isolated temporary settings, in-process hosting, and fake HTTP handlers. It does not use real TestRail credentials.

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
- [x] Per-user TestRail authentication / authorization layer
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
| Unreleased | All data pages and handlers require sign-in, including read-only access. | Each user signs in with their own TestRail username/password once the TestRail connection is configured on `/Setup`. |
| Unreleased | Production authentication cookies require HTTPS. | Configure TLS and correctly forward the request scheme through any trusted reverse proxy. |
| Unreleased | Markdown generic attributes and unapproved advanced extensions are no longer interpreted. | Use supported headings, lists, tables, links, images, fenced code, strikethrough, and task lists instead of custom attributes. |
| Unreleased | Data pages are no longer suitable for anonymous health probes. | Point monitoring at `/healthz`. |
