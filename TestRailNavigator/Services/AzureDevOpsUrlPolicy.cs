using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TestRailNavigator.Services;

/// <summary>
/// Validates Azure DevOps URLs without treating work item or relation URLs as trusted configuration.
/// </summary>
public static class AzureDevOpsUrlPolicy
{
    private const string LegacyHostSuffix = ".visualstudio.com";

    /// <summary>
    /// Validates an administrator-approved HTTPS organization or collection root.
    /// Hosted roots must include one organization; legacy roots must identify one organization host.
    /// Other servers must include a collection path. Query strings and fragments are not allowed.
    /// </summary>
    public static bool TryGetBaseUri(string? value, [NotNullWhen(true)] out Uri? baseUri)
    {
        baseUri = null;
        if (!TryReadUrl(value, out var uri, out var segments) ||
            uri.OriginalString.IndexOfAny(['?', '#']) >= 0 ||
            segments.Any(segment => Is(segment, "_apis") || Is(segment, "_workitems")))
        {
            return false;
        }

        if (Is(uri.Host, "dev.azure.com"))
        {
            if (segments.Length != 1) return false;
        }
        else if (uri.Host.EndsWith(LegacyHostSuffix, StringComparison.OrdinalIgnoreCase))
        {
            var organization = uri.Host[..^LegacyHostSuffix.Length];
            if (organization.Length == 0 || organization.Contains('.') || segments.Length != 0) return false;
        }
        else if (segments.Length == 0)
        {
            return false;
        }

        baseUri = CreateBaseUri(uri, segments);
        return true;
    }

    internal static (string Project, int Id) ParseWorkItemUrl(
        Uri approvedBaseUri,
        string? url,
        string? fallbackProject = null)
    {
        if (!TryReadUrl(url, out var uri, out var segments))
        {
            throw new ArgumentException(
                "Enter a valid HTTPS Azure DevOps work item URL without user information, encoded separators, or dot segments.",
                nameof(url));
        }

        if (!Is(uri.Scheme, approvedBaseUri.Scheme) ||
            !Is(uri.Host, approvedBaseUri.Host) ||
            uri.Port != approvedBaseUri.Port)
        {
            throw new ArgumentException(
                "The Azure DevOps work item URL must match the approved HTTPS server and port.",
                nameof(url));
        }

        var baseSegments = approvedBaseUri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.UnescapeDataString)
            .ToArray();

        if (segments.Length < baseSegments.Length ||
            !segments.AsSpan(0, baseSegments.Length).SequenceEqual(baseSegments))
        {
            throw new ArgumentException(
                "The Azure DevOps work item URL must match the approved organization or collection path exactly.",
                nameof(url));
        }

        if (!TryReadWorkItemPath(segments.AsSpan(baseSegments.Length), fallbackProject, out var project, out var id))
        {
            throw new ArgumentException(
                "The Azure DevOps URL must identify a project and a positive work item ID. Organization-scoped API links require a validated parent project.",
                nameof(url));
        }

        return (project, id);
    }

    // Syntax recognition only: callers must validate against configured trust before sending credentials.
    internal static bool TryParseWorkItemUrl(
        string? url,
        out string organization,
        out string project,
        out int id)
    {
        organization = string.Empty;
        project = string.Empty;
        id = 0;

        if (!TryReadUrl(url, out var uri, out var segments)) return false;

        var itemPathLength = segments.Length >= 5 && IsApiPath(segments.AsSpan(segments.Length - 4))
            ? 5
            : 4;
        if (segments.Length < itemPathLength) return false;

        var baseLength = segments.Length - itemPathLength;
        var candidateBase = CreateBaseUri(uri, segments.Take(baseLength));
        if (!TryGetBaseUri(candidateBase.AbsoluteUri, out _) ||
            !TryReadWorkItemPath(segments.AsSpan(baseLength), null, out var parsedProject, out var parsedId))
        {
            return false;
        }

        organization = uri.Host.EndsWith(LegacyHostSuffix, StringComparison.OrdinalIgnoreCase)
            ? uri.Host[..^LegacyHostSuffix.Length]
            : segments[baseLength - 1];
        project = parsedProject;
        id = parsedId;
        return true;
    }

    private static bool TryReadWorkItemPath(
        ReadOnlySpan<string> path,
        string? fallbackProject,
        out string project,
        out int id)
    {
        project = string.Empty;
        id = 0;

        if (path.Length == 4 && Is(path[1], "_workitems") && (Is(path[2], "edit") || Is(path[2], "view")))
        {
            project = path[0];
        }
        else if (path.Length == 5 && IsApiPath(path[1..]))
        {
            project = path[0];
        }
        else if (IsApiPath(path) && !string.IsNullOrWhiteSpace(fallbackProject))
        {
            project = fallbackProject;
        }
        else
        {
            return false;
        }

        return int.TryParse(path[^1], NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
    }

    private static bool IsApiPath(ReadOnlySpan<string> path) =>
        path.Length == 4 && Is(path[0], "_apis") && Is(path[1], "wit") && Is(path[2], "workitems");

    private static bool Is(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static Uri CreateBaseUri(Uri uri, IEnumerable<string> segments) =>
        new($"{uri.GetLeftPart(UriPartial.Authority)}/{string.Join("/", segments.Select(Uri.EscapeDataString))}");

    private static bool TryReadUrl(
        string? value,
        [NotNullWhen(true)] out Uri? uri,
        out string[] segments)
    {
        uri = null;
        segments = [];
        if (string.IsNullOrWhiteSpace(value)) return false;

        var text = value.Trim();
        if (!text.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            text.Any(char.IsControl) || text.Contains('\\') ||
            !Uri.TryCreate(text, UriKind.Absolute, out var parsed) ||
            parsed.UserInfo.Length != 0 || parsed.Host.Length == 0 ||
            parsed.HostNameType == UriHostNameType.Unknown || parsed.Host.EndsWith('.') || parsed.Port <= 0)
        {
            return false;
        }

        var authorityEnd = text.IndexOfAny(['/', '?', '#'], "https://".Length);
        if (authorityEnd < 0) authorityEnd = text.Length;
        var authority = text["https://".Length..authorityEnd];
        if (authority.Any(character => character is '@' or '%' || char.IsWhiteSpace(character))) return false;

        var pathEnd = text.IndexOfAny(['?', '#'], authorityEnd);
        if (pathEnd < 0) pathEnd = text.Length;
        var rawPath = text[authorityEnd..pathEnd];

        // Uri normalizes dot segments and backslashes. Inspect the original path before trusting that normalization.
        if (rawPath.Length > 1)
        {
            var path = rawPath[1..];
            if (path.EndsWith('/')) path = path[..^1];
            var rawSegments = path.Split('/');
            var decodedSegments = new string[rawSegments.Length];
            for (var index = 0; index < rawSegments.Length; index++)
            {
                if (!TryReadSegment(rawSegments[index], out decodedSegments[index])) return false;
            }

            segments = decodedSegments;
        }

        uri = parsed;
        return true;
    }

    private static bool TryReadSegment(string value, out string decoded)
    {
        decoded = string.Empty;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%') continue;
            if (index + 2 >= value.Length || !Uri.IsHexDigit(value[index + 1]) || !Uri.IsHexDigit(value[index + 2]))
            {
                return false;
            }

            index += 2;
        }

        decoded = Uri.UnescapeDataString(value);
        return decoded.Length != 0 && decoded == decoded.Trim() && decoded is not "." and not ".." &&
            !decoded.Any(character => character is '/' or '\\' or '%' or '?' or '#' or ';' || char.IsControl(character));
    }
}
