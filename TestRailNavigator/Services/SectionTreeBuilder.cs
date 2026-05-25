using TestRailNavigator.Models;

namespace TestRailNavigator.Services;

/// <summary>
/// Builds a nested <see cref="SectionTreeNode"/> tree from the flat <see cref="Section"/> list
/// returned by TestRail, and resolves slash-delimited folder paths against it.
/// </summary>
/// <remarks>
/// Company-level folder structure is meaningful at JSI, so the wizard flow never falls back
/// to an arbitrary section. The resolver returns explicit success/failure detail so the UI can
/// tell the user exactly which path segment is missing rather than silently guessing.
/// </remarks>
public static class SectionTreeBuilder
{
    /// <summary>
    /// Assembles the flat <paramref name="sections"/> list into a rooted tree keyed on <see cref="Section.ParentId"/>.
    /// Children are sorted case-insensitively by name. Orphaned sections (parent id not in the list)
    /// are treated as root-level.
    /// </summary>
    public static IReadOnlyList<SectionTreeNode> Build(IEnumerable<Section> sections)
    {
        var list = sections as IList<Section> ?? sections.ToList();
        var byId = list.ToDictionary(s => s.Id, s => new SectionTreeNode(s));

        var roots = new List<SectionTreeNode>();
        foreach (var node in byId.Values)
        {
            if (node.Section.ParentId is { } parentId && byId.TryGetValue(parentId, out var parent))
            {
                parent.ChildrenInternal.Add(node);
                node.Parent = parent;
            }
            else
            {
                roots.Add(node);
            }
        }

        SortRecursive(roots);
        return roots;
    }

    private static void SortRecursive(List<SectionTreeNode> nodes)
    {
        nodes.Sort((a, b) => string.Compare(a.Section.Name, b.Section.Name, StringComparison.OrdinalIgnoreCase));
        foreach (var child in nodes)
        {
            SortRecursive(child.ChildrenInternal);
        }
    }

    /// <summary>
    /// Resolves a slash-delimited path (<c>a/b/c</c>) against the supplied tree.
    /// Matching is case-insensitive. Returns the resolved section id on full success,
    /// otherwise reports which segments were missing and (if applicable) the deepest parent
    /// that did resolve, so callers can offer a "create missing leaf" affordance.
    /// </summary>
    /// <param name="roots">The root nodes produced by <see cref="Build"/>.</param>
    /// <param name="path">The slash-delimited path. Leading/trailing slashes are tolerated.</param>
    public static SectionPathResolution ResolvePath(IReadOnlyList<SectionTreeNode> roots, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new SectionPathResolution(null, [], null, SectionPathStatus.Empty, null);
        }

        var segments = path
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (segments.Count == 0)
        {
            return new SectionPathResolution(null, [], null, SectionPathStatus.Empty, null);
        }

        IReadOnlyList<SectionTreeNode> cursor = roots;
        SectionTreeNode? deepestResolved = null;

        for (var i = 0; i < segments.Count; i++)
        {
            var matches = cursor
                .Where(n => string.Equals(n.Section.Name, segments[i], StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                var missing = segments.Skip(i).ToList();
                return new SectionPathResolution(
                    null,
                    missing,
                    deepestResolved,
                    SectionPathStatus.Missing,
                    $"Path segment '{segments[i]}' not found under '{FormatBreadcrumb(deepestResolved) ?? "root"}'.");
            }

            if (matches.Count > 1)
            {
                return new SectionPathResolution(
                    null,
                    segments.Skip(i).ToList(),
                    deepestResolved,
                    SectionPathStatus.Ambiguous,
                    $"Path segment '{segments[i]}' is ambiguous ({matches.Count} siblings share the name). Pick from the tree instead.");
            }

            deepestResolved = matches[0];
            cursor = deepestResolved.ChildrenInternal;
        }

        return new SectionPathResolution(
            deepestResolved?.Section.Id,
            [],
            deepestResolved,
            SectionPathStatus.Resolved,
            null);
    }

    /// <summary>
    /// Returns the display breadcrumb for a resolved node (e.g. <c>Regression › Plan Details</c>)
    /// or <see langword="null"/> when the node is <see langword="null"/>.
    /// </summary>
    public static string? FormatBreadcrumb(SectionTreeNode? node)
    {
        if (node is null) return null;
        var names = new List<string>();
        var cursor = node;
        while (cursor is not null)
        {
            names.Insert(0, cursor.Section.Name);
            cursor = cursor.Parent;
        }

        return string.Join(" › ", names);
    }

    /// <summary>
    /// Plans how a slash-delimited path maps onto the tree without writing anything.
    /// Returns the deepest node that already exists plus the ordered list of segments
    /// that would need to be created beneath it. Callers use this to preview a
    /// "create missing sub-sections" action before committing.
    /// </summary>
    /// <param name="roots">The root nodes produced by <see cref="Build"/>.</param>
    /// <param name="path">The slash-delimited path. Leading/trailing slashes are tolerated.</param>
    /// <returns>A <see cref="SectionPathPlan"/> describing the split between existing and new segments.</returns>
    public static SectionPathPlan PlanPath(IReadOnlyList<SectionTreeNode> roots, string? path)
    {
        var segments = SplitPath(path);
        if (segments.Count == 0)
        {
            return new SectionPathPlan([], null, [], null);
        }

        IReadOnlyList<SectionTreeNode> cursor = roots;
        SectionTreeNode? deepestExisting = null;

        for (var i = 0; i < segments.Count; i++)
        {
            var matches = cursor
                .Where(n => string.Equals(n.Section.Name, segments[i], StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count > 1)
            {
                // Ambiguous prefix — bail without suggesting a create, the user needs to resolve it.
                return new SectionPathPlan(segments, deepestExisting, [], segments[i]);
            }

            if (matches.Count == 0)
            {
                var missing = segments.Skip(i).ToList();
                return new SectionPathPlan(segments, deepestExisting, missing, null);
            }

            deepestExisting = matches[0];
            cursor = deepestExisting.ChildrenInternal;
        }

        return new SectionPathPlan(segments, deepestExisting, [], null);
    }

    /// <summary>
    /// Splits a slash/backslash-delimited path into trimmed, non-empty segments.
    /// </summary>
    public static List<string> SplitPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        return path
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>
    /// Looks up a node by section id, walking the rooted tree.
    /// </summary>
    public static SectionTreeNode? FindById(IReadOnlyList<SectionTreeNode> roots, int sectionId)
    {
        foreach (var root in roots)
        {
            var found = FindByIdInternal(root, sectionId);
            if (found is not null) return found;
        }
        return null;
    }

    private static SectionTreeNode? FindByIdInternal(SectionTreeNode node, int sectionId)
    {
        if (node.Section.Id == sectionId) return node;
        foreach (var child in node.ChildrenInternal)
        {
            var found = FindByIdInternal(child, sectionId);
            if (found is not null) return found;
        }
        return null;
    }
}

/// <summary>A single node in the section tree produced by <see cref="SectionTreeBuilder"/>.</summary>
public class SectionTreeNode
{
    internal readonly List<SectionTreeNode> ChildrenInternal = [];

    internal SectionTreeNode(Section section)
    {
        Section = section;
    }

    /// <summary>Gets the underlying TestRail section.</summary>
    public Section Section { get; }

    /// <summary>Gets the parent node, or <see langword="null"/> when this node is a root.</summary>
    public SectionTreeNode? Parent { get; internal set; }

    /// <summary>Gets the sorted, read-only child list.</summary>
    public IReadOnlyList<SectionTreeNode> Children => ChildrenInternal;

    /// <inheritdoc />
    public override string ToString() => $"{Section.Id}:{Section.Name} ({ChildrenInternal.Count} children)";
}

/// <summary>The outcome of resolving a slash-delimited path against a section tree.</summary>
/// <param name="SectionId">The resolved section identifier when fully matched, otherwise <see langword="null"/>.</param>
/// <param name="MissingSegments">The segments that did not match. Empty on full success.</param>
/// <param name="DeepestResolved">The last node that matched before resolution failed (or the fully resolved node on success).</param>
/// <param name="Status">The resolution status.</param>
/// <param name="Message">A human-readable explanation when <see cref="Status"/> is not <see cref="SectionPathStatus.Resolved"/>.</param>
public record SectionPathResolution(
    int? SectionId,
    IReadOnlyList<string> MissingSegments,
    SectionTreeNode? DeepestResolved,
    SectionPathStatus Status,
    string? Message);

/// <summary>Outcome categories for a path resolution.</summary>
public enum SectionPathStatus
{
    /// <summary>The input path was empty or whitespace.</summary>
    Empty,

    /// <summary>Every segment matched exactly one node; the full section id is available.</summary>
    Resolved,

    /// <summary>A segment had no match under the current cursor.</summary>
    Missing,

    /// <summary>A segment matched multiple siblings; the path is not uniquely identifying.</summary>
    Ambiguous
}

/// <summary>
/// A non-destructive plan for how a slash-delimited path maps onto the section tree.
/// </summary>
/// <param name="AllSegments">Every segment in the input path, in order.</param>
/// <param name="DeepestExisting">
/// The deepest node that already exists along the path, or <see langword="null"/> when no
/// prefix matched (the path would be created entirely at root).
/// </param>
/// <param name="NewSegments">The segments that do not yet exist and would need to be created, in order.</param>
/// <param name="AmbiguousSegment">
/// The first path segment that matched multiple sibling sections, when the path cannot be
/// uniquely identified; <see langword="null"/> otherwise.
/// </param>
public record SectionPathPlan(
    IReadOnlyList<string> AllSegments,
    SectionTreeNode? DeepestExisting,
    IReadOnlyList<string> NewSegments,
    string? AmbiguousSegment)
{
    /// <summary>Gets a value indicating whether the path is empty.</summary>
    public bool IsEmpty => AllSegments.Count == 0;

    /// <summary>Gets a value indicating whether the ambiguity prevents committing.</summary>
    public bool IsAmbiguous => !string.IsNullOrEmpty(AmbiguousSegment);

    /// <summary>Gets a value indicating whether every segment already exists on the tree.</summary>
    public bool FullyExists => !IsEmpty && !IsAmbiguous && NewSegments.Count == 0 && DeepestExisting is not null;

    /// <summary>Gets a value indicating whether at least one new segment would be created.</summary>
    public bool HasNewSegments => NewSegments.Count > 0 && !IsAmbiguous;
}
