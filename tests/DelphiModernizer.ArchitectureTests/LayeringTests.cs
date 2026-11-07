using System.Linq;
using System.Reflection;
using Xunit;

namespace DelphiModernizer.ArchitectureTests;

/// <summary>The engine's projects depend downward only; see docs/ARCHITECTURE.md.</summary>
public sealed class LayeringTests
{
    [Theory]
    [InlineData("DelphiModernizer.Engine.Core")]
    [InlineData("DelphiModernizer.Engine.Delphi", "DelphiModernizer.Engine.Core")]
    [InlineData("DelphiModernizer.Engine.Kit", "DelphiModernizer.Engine.Core")]
    [InlineData("DelphiModernizer.Engine.Traceability", "DelphiModernizer.Engine.Core", "DelphiModernizer.Engine.Kit")]
    [InlineData("DelphiModernizer.Engine.Agents", "DelphiModernizer.Engine.Core", "DelphiModernizer.Engine.Kit")]
    [InlineData(
        "DelphiModernizer.Engine.Workspaces",
        "DelphiModernizer.Engine.Core",
        "DelphiModernizer.Engine.Delphi",
        "DelphiModernizer.Engine.Kit",
        "DelphiModernizer.Engine.Traceability")]
    public void A_project_references_only_the_projects_below_it(string project, params string[] allowed)
    {
        var references = Assembly
            .Load(project)
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("DelphiModernizer", System.StringComparison.Ordinal) || name == "dmz")
            .Order()
            .ToArray();

        var expected = allowed
            .Order()
            .ToArray();

        Assert.Equal(expected, references);
    }
}
