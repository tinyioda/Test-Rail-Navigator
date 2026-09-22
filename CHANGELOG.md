# Changelog

All notable changes to this project are documented here using
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) conventions.

## [Unreleased]

### Security

- Require application-wide single-admin authentication using existing Setup credentials or deployment-provided configuration. Missing credentials fail closed.
- Add protected authentication cookies, credential-change invalidation, a 30-minute inactivity timeout, antiforgery-protected logout, and sign-in rate limiting.
- Restrict Azure DevOps PAT-bearing requests to an approved HTTPS organization or collection, including child links. Reject untrusted destinations and redirects before forwarding credentials.
- Replace blanket advanced Markdown extensions with explicitly supported formatting and sanitize generated HTML before rendering.

### Added

- An isolated security regression suite for authentication, Azure DevOps URL boundaries, and Markdown rendering.
- An anonymous `/healthz` endpoint that reports application health without querying integrations.
- A configurable `DataProtection:KeysPath` for persistent authentication key storage.

### Changed

- Reuse the existing Setup credentials for the entire application rather than protecting only Setup.
- Add approved Azure DevOps base URL configuration to Setup and Helm.
- Surface hierarchy child-fetch failures instead of silently dropping failed work items.
- Point Helm health probes at `/healthz` and document deployment and upgrade requirements.

### Breaking

- All data pages and handlers now require administrator sign-in, including read-only access. Provision both `SetupUsername` and `SetupPassword`, or their `TestRail:` configuration overrides, before upgrading.
- Production authentication cookies require HTTPS. Configure TLS and a correctly configured reverse proxy when applicable.
- Azure DevOps generation now requires `AzureDevOpsBaseUrl` in addition to the PAT. Use the canonical HTTPS organization or collection URL, matching the origin, port, and case-sensitive organization/collection path of work-item links. HTTP and redirects are rejected.
- Markdown generic attributes and unapproved advanced extensions are no longer interpreted. Use the supported standard formatting instead of custom HTML attributes.
- Health monitors must use `/healthz` instead of authenticated data pages.
