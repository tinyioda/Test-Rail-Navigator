using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Unit coverage for acceptance-criteria parsing strategies.</summary>
public class AcceptanceCriteriaParserTests
{
    /// <summary>Empty input falls back to one untitled case based on the supplied work-item title.</summary>
    [Fact]
    public void ParseWithDiagnostics_ReturnsFallbackForEmptyInput()
    {
        var result = AcceptanceCriteriaParser.ParseWithDiagnostics(null, "Fallback Story");

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("Fallback Story", candidate.Title);
        Assert.Null(candidate.Steps);
        Assert.Null(candidate.Expected);
        Assert.Equal("Empty (fallback)", result.Diagnostics.DetectedFormat);
        Assert.Equal(1, result.Diagnostics.ItemCount);
    }

    /// <summary>AC-prefixed criteria keep their prefix and split Expected Results into the dedicated field.</summary>
    [Fact]
    public void ParseWithDiagnostics_ParsesAcPrefixedBodiesAndExpectedResults()
    {
        const string input = """
            Acceptance Criteria:
            AC1 - Successful sign in
            When the user submits valid credentials
            Expected Results: Dashboard opens

            AC2 - Locked account
            Given the account is locked
            When the user attempts to sign in
            Then: Access is denied
            """;

        var result = AcceptanceCriteriaParser.ParseWithDiagnostics(input, "Ignored");

        Assert.Equal("AC-prefixed items", result.Diagnostics.DetectedFormat);
        Assert.Equal(2, result.Candidates.Count);

        var first = result.Candidates[0];
        Assert.Equal("AC1 - Successful sign in", first.Title);
        Assert.Equal("When the user submits valid credentials", first.Steps);
        Assert.Equal("Dashboard opens", first.Expected);

        var second = result.Candidates[1];
        Assert.Equal("AC2 - Locked account", second.Title);
        Assert.Equal("Given the account is locked\nWhen the user attempts to sign in", second.Steps);
        Assert.Equal("Access is denied", second.Expected);
    }

    /// <summary>Full Gherkin scenarios become individual case candidates with steps and expected results.</summary>
    [Fact]
    public void ParseWithDiagnostics_ParsesGherkinScenarios()
    {
        const string input = """
            Scenario: Valid login
            Given the user is on the login page
            When the user submits valid credentials
            Then the dashboard is displayed

            Scenario: Invalid login
            Given the user is on the login page
            When the user submits an invalid password
            Then an error message is shown
            """;

        var result = AcceptanceCriteriaParser.ParseWithDiagnostics(input, "Fallback");

        Assert.Equal("Gherkin (Given/When/Then)", result.Diagnostics.DetectedFormat);
        Assert.Equal(2, result.Candidates.Count);

        var first = result.Candidates[0];
        Assert.Equal("When the user submits valid credentials", first.Title);
        Assert.Equal("Given the user is on the login page\n\nWhen the user submits valid credentials", first.Steps);
        Assert.Equal("Then the dashboard is displayed", first.Expected);

        var second = result.Candidates[1];
        Assert.Equal("When the user submits an invalid password", second.Title);
        Assert.Equal("Then an error message is shown", second.Expected);
    }

    /// <summary>Scenario headers without Gherkin bodies still produce titled candidates.</summary>
    [Fact]
    public void ParseWithDiagnostics_ParsesScenarioHeadersWithoutBodies()
    {
        const string input = """
            Scenario: Valid login
            Scenario: Invalid password
            Example: Locked account
            """;

        var result = AcceptanceCriteriaParser.ParseWithDiagnostics(input, "Fallback");

        Assert.Equal("Scenario headers", result.Diagnostics.DetectedFormat);
        Assert.Equal(
            ["Valid login", "Invalid password", "Locked account"],
            result.Candidates.Select(candidate => candidate.Title).ToArray());
        Assert.All(result.Candidates, candidate =>
        {
            Assert.Null(candidate.Steps);
            Assert.Null(candidate.Expected);
        });
    }

    /// <summary>Tabular criteria map title, steps, and expected-result columns by header name.</summary>
    [Fact]
    public void ParseWithDiagnostics_ParsesTabularCriteria()
    {
        const string input = """
            <table>
              <tr><th>Criterion</th><th>When</th><th>Expected Result</th></tr>
              <tr><td>Valid login</td><td>Submit correct credentials</td><td>Dashboard opens</td></tr>
              <tr><td>Invalid login</td><td>Submit incorrect password</td><td>Error appears</td></tr>
            </table>
            """;

        var result = AcceptanceCriteriaParser.ParseWithDiagnostics(input, "Fallback");

        Assert.Equal("Tabular", result.Diagnostics.DetectedFormat);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal("Valid login", result.Candidates[0].Title);
        Assert.Equal("Submit correct credentials", result.Candidates[0].Steps);
        Assert.Equal("Dashboard opens", result.Candidates[0].Expected);
        Assert.Equal("Invalid login", result.Candidates[1].Title);
    }

    /// <summary>Two or more numbered items are treated as a numbered-list criteria block.</summary>
    [Fact]
    public void ParseWithDiagnostics_ParsesNumberedLists()
    {
        const string input = """
            1. User can sign in
            2. User sees the dashboard
            """;

        var result = AcceptanceCriteriaParser.ParseWithDiagnostics(input, "Fallback");

        Assert.Equal("Numbered list", result.Diagnostics.DetectedFormat);
        Assert.Equal(["User can sign in", "User sees the dashboard"], result.Candidates.Select(candidate => candidate.Title));
    }

    /// <summary>Requirement sentences are extracted when no stronger structure is present.</summary>
    [Fact]
    public void ParseWithDiagnostics_ParsesRequirementSentences()
    {
        const string input = "The system shall record an audit event. The user must see a confirmation message. Additional context follows.";

        var result = AcceptanceCriteriaParser.ParseWithDiagnostics(input, "Fallback");

        Assert.Equal("Requirement sentences", result.Diagnostics.DetectedFormat);
        Assert.Equal(
            [
                "The system shall record an audit event.",
                "The user must see a confirmation message."
            ],
            result.Candidates.Select(candidate => candidate.Title).ToArray());
    }

    /// <summary>Insufficient structure falls through to paragraph parsing instead of losing content.</summary>
    [Fact]
    public void ParseWithDiagnostics_FallsBackToParagraphsWhenStructuredFormatsDoNotApply()
    {
        const string input = """
            1. Only one list item

            Additional details live in a second paragraph.
            """;

        var result = AcceptanceCriteriaParser.ParseWithDiagnostics(input, "Fallback");

        Assert.Equal("Paragraphs", result.Diagnostics.DetectedFormat);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal("1. Only one list item", result.Candidates[0].Title);
        Assert.Equal("Additional details live in a second paragraph.", result.Candidates[1].Title);
    }
}
