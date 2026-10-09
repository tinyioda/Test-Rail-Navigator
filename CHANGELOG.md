# Changelog

All notable changes to this project are documented here using
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) conventions.

## [Unreleased]

### Security

- Require application-wide authentication: every signed-in user validates their own TestRail username/password live against the configured TestRail connection, and their own TestRail role/permissions govern what they can do in the app. Missing credentials, or an unconfigured TestRail connection, fail closed.
- Add protected authentication cookies, server-side session invalidation (on logout or app restart), a 30-minute inactivity timeout, antiforgery-protected logout, and sign-in rate limiting.
- Restrict Azure DevOps PAT-bearing requests to an approved HTTPS organization or collection, including child links. Reject untrusted destinations and redirects before forwarding credentials.
- Replace blanket advanced Markdown extensions with explicitly supported formatting and sanitize generated HTML before rendering.

### Added

- An isolated security regression suite for authentication, Azure DevOps URL boundaries, and Markdown rendering.
- An anonymous `/healthz` endpoint that reports application health without querying integrations.
- A configurable `DataProtection:KeysPath` for persistent authentication key storage.
- A `TestRail` section in `appsettings.json` storing the connection `BaseUrl`/`Username`/`ApiKey`, writable at runtime from the Setup page.
- `AzureDevOps` and `Jira` sections in `appsettings.json` storing their (optional) credentials, writable at runtime from the Setup page, mirroring the `TestRail` section.
- Unit test coverage for `AcceptanceCriteriaParser`, `HierarchyGenerator`, `SectionTreeBuilder`, `PermissionService`, `TestRailPermissions`, `SettingsService`, `MarkdownRenderer`, `TestRailCredentialStore`, and per-user `TestRailClient` credential resolution to satisfy the new Test-Driven Development process requirement.

### Changed

- Replace the single shared admin login with per-user authentication: users sign in with their own TestRail username/password, validated live against TestRail, instead of a shared administrator credential.
- Add approved Azure DevOps base URL configuration to Setup.
- Surface hierarchy child-fetch failures instead of silently dropping failed work items.
- Document `/healthz` for deployment and upgrade requirements.
- Mandate Test-Driven Development (red-green-refactor) for all future code changes; see `STEERING.md`.
- Label Azure DevOps and Jira as optional on the Setup page; features that depend on them remain disabled until configured.

### Removed

- Azure DevOps and Jira integrations entirely: the `/GenerateCases`, `/GenerateHierarchy`, and `/CreatePlanFromStory` pages, the `AzureDevOpsService`/`IIssueTrackerClient`/`HierarchyGenerator`/`AcceptanceCriteriaParser` services, the `AzureDevOpsBaseUrl`/`AzureDevOpsPat`/`JiraBaseUrl`/`JiraEmail`/`JiraApiToken` settings, the `AzureDevOps`/`Jira` sections of `appsettings.json`, the Setup page's Azure DevOps/Jira form sections, and all related tests. Use third-party MCPs or AI skills for work-item/issue-tracker integration instead.
- The AI case-enrichment feature ("Enrich with AI" on Generate Cases, the OpenAI-compatible endpoint configuration, and all related code/tests). Use third-party MCPs or AI skills for this instead.
- The shared `SetupUsername`/`SetupPassword` administrator credential. There is no separate app-level login anymore; each user signs in with their own TestRail account.

### Breaking

- All data pages and handlers now require sign-in, including read-only access. Each user signs in with their own TestRail username/password; TestRail must be reachable and configured before anyone can sign in.
- Production authentication cookies require HTTPS. Configure TLS and a correctly configured reverse proxy when applicable.
- Azure DevOps generation now requires `AzureDevOpsBaseUrl` in addition to the PAT. Use the canonical HTTPS organization or collection URL, matching the origin, port, and case-sensitive organization/collection path of work-item links. HTTP and redirects are rejected.
- Markdown generic attributes and unapproved advanced extensions are no longer interpreted. Use the supported standard formatting instead of custom HTML attributes.
- Health monitors must use `/healthz` instead of authenticated data pages.
- `/Setup` is only reachable before the TestRail connection is configured; once `TestRail:BaseUrl`/`Username`/`ApiKey` are set, it redirects to the dashboard for every user, including the administrator.
