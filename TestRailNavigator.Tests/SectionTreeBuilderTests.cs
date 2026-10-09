using TestRailNavigator.Models;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Unit coverage for section-tree assembly and path resolution.</summary>
public class SectionTreeBuilderTests
{
    /// <summary>Build links parent-child relationships, sorts alphabetically, and promotes orphans to roots.</summary>
    [Fact]
    public void Build_SortsNodesAndTreatsOrphansAsRoots()
    {
        List<Section> sections =
        [
            new() { Id = 3, Name = "Zulu" },
            new() { Id = 1, Name = "alpha" },
            new() { Id = 4, Name = "Orphan", ParentId = 999 },
            new() { Id = 5, Name = "Bravo child", ParentId = 3 },
            new() { Id = 6, Name = "alpha child", ParentId = 3 }
        ];

        var roots = SectionTreeBuilder.Build(sections);

        Assert.Equal(["alpha", "Orphan", "Zulu"], roots.Select(node => node.Section.Name).ToArray());
        Assert.Null(roots[0].Parent);
        Assert.Null(roots[1].Parent);
        Assert.Null(roots[2].Parent);
        Assert.Equal(["alpha child", "Bravo child"], roots[2].Children.Select(node => node.Section.Name).ToArray());
        Assert.All(roots[2].Children, child => Assert.Same(roots[2], child.Parent));
    }

    /// <summary>ResolvePath matches case-insensitively and breadcrumbs show the resolved ancestry.</summary>
    [Fact]
    public void ResolvePath_ReturnsResolvedSectionAndBreadcrumb()
    {
        var roots = BuildTree();

        var resolution = SectionTreeBuilder.ResolvePath(roots, @"root\child/grandchild");

        Assert.Equal(SectionPathStatus.Resolved, resolution.Status);
        Assert.Equal(3, resolution.SectionId);
        Assert.Equal("Root › Child › Grandchild", SectionTreeBuilder.FormatBreadcrumb(resolution.DeepestResolved));
        Assert.Empty(resolution.MissingSegments);
    }

    /// <summary>Missing path segments report the deepest resolved node and the remainder of the path.</summary>
    [Fact]
    public void ResolvePath_ReturnsMissingSegments()
    {
        var roots = BuildTree();

        var resolution = SectionTreeBuilder.ResolvePath(roots, "Root/Child/Missing/Leaf");

        Assert.Equal(SectionPathStatus.Missing, resolution.Status);
        Assert.Null(resolution.SectionId);
        Assert.Equal(["Missing", "Leaf"], resolution.MissingSegments);
        Assert.Equal("Root › Child", SectionTreeBuilder.FormatBreadcrumb(resolution.DeepestResolved));
        Assert.Contains("Path segment 'Missing' not found", resolution.Message);
    }

    /// <summary>Duplicate sibling names produce an ambiguous resolution instead of guessing.</summary>
    [Fact]
    public void ResolvePath_ReturnsAmbiguousWhenSiblingNamesDuplicate()
    {
        IReadOnlyList<SectionTreeNode> roots = SectionTreeBuilder.Build(
        [
            new Section { Id = 1, Name = "Root" },
            new Section { Id = 2, Name = "Duplicate" },
            new Section { Id = 3, Name = "duplicate" }
        ]);

        var resolution = SectionTreeBuilder.ResolvePath(roots, "Duplicate");

        Assert.Equal(SectionPathStatus.Ambiguous, resolution.Status);
        Assert.Equal(["Duplicate"], resolution.MissingSegments);
        Assert.Contains("ambiguous", resolution.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>PlanPath identifies the deepest existing node and the remaining segments to create.</summary>
    [Fact]
    public void PlanPath_ReturnsDeepestExistingNodeAndNewSegments()
    {
        var roots = BuildTree();

        var plan = SectionTreeBuilder.PlanPath(roots, "/Root/Child/New Leaf/Last Leaf/");

        Assert.False(plan.IsEmpty);
        Assert.False(plan.IsAmbiguous);
        Assert.False(plan.FullyExists);
        Assert.True(plan.HasNewSegments);
        Assert.Equal("Child", plan.DeepestExisting?.Section.Name);
        Assert.Equal(["Root", "Child", "New Leaf", "Last Leaf"], plan.AllSegments);
        Assert.Equal(["New Leaf", "Last Leaf"], plan.NewSegments);
    }

    /// <summary>SplitPath trims separators, and FindById walks the tree recursively.</summary>
    [Fact]
    public void SplitPath_AndFindById_HandleMixedSeparatorsAndRecursion()
    {
        var roots = BuildTree();

        Assert.Equal(["Root", "Child", "Grandchild"], SectionTreeBuilder.SplitPath(@"//Root\Child/Grandchild\\"));
        Assert.Empty(SectionTreeBuilder.SplitPath("   "));
        Assert.Equal("Grandchild", SectionTreeBuilder.FindById(roots, 3)?.Section.Name);
        Assert.Null(SectionTreeBuilder.FindById(roots, 999));
    }

    private static IReadOnlyList<SectionTreeNode> BuildTree()
    {
        return SectionTreeBuilder.Build(
        [
            new Section { Id = 1, Name = "Root" },
            new Section { Id = 2, Name = "Child", ParentId = 1 },
            new Section { Id = 3, Name = "Grandchild", ParentId = 2 },
            new Section { Id = 4, Name = "Sibling", ParentId = 1 }
        ]);
    }
}
