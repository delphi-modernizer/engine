using System.Linq;
using Xunit;

namespace DelphiModernizer.Engine.Traceability.Tests;

public sealed class TraceCheckerTests
{
    private const string Rules = "02-functional-requirements/business-rules.md";
    private const string Frs = "02-functional-requirements/D03-functional-requirements.md";
    private const string Tests = "10-testing-quality/D14-testing-strategy.md";
    private const string Questions = "00-project-charter/open-questions.md";

    [Fact]
    public void A_fresh_workspace_from_the_kit_has_no_findings()
    {
        using var workspace = new KitWorkspace();

        var report = workspace.Check(strict: true);

        Assert.Empty(report.Issues);
        Assert.All(report.Deliverables, d => Assert.Equal("template", d.Status));
    }

    [Fact]
    public void A_business_rule_needs_evidence_that_exists()
    {
        using var workspace = new KitWorkspace()
            .Append(Rules, "### BR-001 — Gold discount\n\nNo link here.")
            .Append(Rules, "### BR-002 — Silver discount\n\nSee legacy/Sales/Pricing.pas#L3-L40.")
            .Append(Rules, "### BR-003 — Bronze discount\n\nSee [code](../../legacy/Sales/Pricing.pas#L2-L4).");

        var report = workspace.Check();

        Assert.Contains(report.Issues, i => i.Rule == "missing-evidence" && i.RecordId == "BR-001");
        Assert.Contains(report.Issues, i => i.Rule == "broken-evidence" && i.Message.Contains("10 lines"));
        Assert.DoesNotContain(report.Issues, i => i.RecordId == "BR-003");
    }

    [Fact]
    public void A_requirement_traces_to_a_rule_and_needs_a_test_strictly()
    {
        using var workspace = new KitWorkspace()
            .Append(Rules, "### BR-001 — Gold discount\n\nlegacy/Sales/Pricing.pas#L2")
            .Append(Frs, "### FR-001 — Apply discounts\n\nPreserves BR-001.")
            .Append(Frs, "### FR-002 — Untraced\n\nNothing behind it.");

        var lenient = workspace.Check();
        var strict = workspace.Check(strict: true);

        Assert.Contains(lenient.Issues, i => i.Rule == "untraced-requirement" && i.RecordId == "FR-002");
        Assert.Equal("warning", lenient.Issues.Single(i => i.Rule == "uncovered-requirement" && i.RecordId == "FR-001").Severity);
        Assert.Equal("error", strict.Issues.Single(i => i.Rule == "uncovered-requirement" && i.RecordId == "FR-001").Severity);
    }

    [Fact]
    public void A_test_that_references_a_requirement_covers_it_and_fills_the_matrix()
    {
        using var workspace = new KitWorkspace()
            .Append(Rules, "### BR-001 — Gold discount\n\nlegacy/Sales/Pricing.pas#L2")
            .Append(Frs, "### FR-001 — Apply discounts\n\nPreserves BR-001.")
            .Append(Tests, "### TC-001 — Gold customer\n\nCovers FR-001.");

        var report = workspace.Check(strict: true);
        var row = Assert.Single(report.Matrix);

        Assert.DoesNotContain(report.Issues, i => i.RecordId == "FR-001");
        Assert.Equal(["BR-001"], row.BusinessRules);
        Assert.Equal(["TC-001"], row.Tests);
    }

    [Fact]
    public void Duplicates_misplaced_definitions_dangling_and_malformed_ids_are_errors()
    {
        using var workspace = new KitWorkspace()
            .Append(Rules, "### BR-001 — One\n\nlegacy/Sales/Pricing.pas#L1\n\n### BR-001 — Again\n\nlegacy/Sales/Pricing.pas#L1")
            .Append(Tests, "### FR-009 — In the wrong file\n\nlegacy/Sales/Pricing.pas#L1")
            .Append(Frs, "See BR-404 and FR-12.");

        var report = workspace.Check();

        Assert.Equal(2, report.Issues.Count(i => i.Rule == "duplicate-definition"));
        Assert.Contains(report.Issues, i => i.Rule == "definition-outside-home" && i.RecordId == "FR-009");
        Assert.Contains(report.Issues, i => i.Rule == "dangling-reference" && i.RecordId == "BR-404");
        Assert.Contains(report.Issues, i => i.Rule == "malformed-id" && i.RecordId == "FR-12");
    }

    [Fact]
    public void Ids_in_code_and_comments_are_not_read()
    {
        using var workspace = new KitWorkspace()
            .Append(Frs, "Write `FR-404` like this. <!-- BR-404 -->\n\n```\nFR-405\n```");

        Assert.Empty(workspace.Check().Issues);
    }

    [Fact]
    public void Register_rows_are_checked_by_their_columns()
    {
        using var workspace = new KitWorkspace()
            .AppendRow(Questions, "| Q-001 | Which database? | Shapes D10 | DBA | Open | |")
            .Replace(
                "12-risk-management/D16-risk-register.md",
                "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |\n",
                "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |\n| RISK-001 | Data loss | High | High | Critical | DB | | | PA | Open |\n");

        var report = workspace.Check();

        Assert.Equal("warning", report.Issues.Single(i => i.Rule == "open-question").Severity);
        Assert.Contains(report.Issues, i => i.Rule == "unmitigated-risk" && i.RecordId == "RISK-001");
    }

    [Fact]
    public void An_adr_needs_an_allowed_status()
    {
        using var workspace = new KitWorkspace();
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(workspace.Root, "docs", "05-target-architecture", "adr", "ADR-0001-runtime.md"),
            "---\npart-of: D09\ntitle: Runtime\nowner: WS03\nstatus: draft\n---\n\n# ADR-0001 — Runtime\n\n**Status:** Pending\n");

        var issue = Assert.Single(workspace.Check().Issues);

        Assert.Equal("invalid-adr-status", issue.Rule);
    }

    [Fact]
    public void Front_matter_must_match_the_kit()
    {
        using var workspace = new KitWorkspace()
            .Replace(Frs, "owner: WS02", "owner: Bob");
        System.IO.File.Delete(System.IO.Path.Combine(workspace.Root, "docs", "13-project-roadmap", "D18-estimates.md"));

        var report = workspace.Check();

        Assert.Contains(report.Issues, i => i.Rule == "invalid-front-matter" && i.Message.Contains("Bob"));
        Assert.Contains(report.Issues, i => i.Rule == "missing-deliverable" && i.RecordId == "D18");
    }
}
