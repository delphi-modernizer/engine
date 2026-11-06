using System.Linq;
using DelphiModernizer.Engine.Delphi.Analysis;
using Xunit;

namespace DelphiModernizer.Engine.Delphi.Tests;

public sealed class DependencyGraphTests
{
    [Fact]
    public void Cycles_are_found_case_insensitively_and_acyclic_units_are_not()
    {
        var graph = Graph(("A", ["b"]), ("B", ["C"]), ("C", ["a"]), ("D", ["A"]));

        var cycles = graph
            .Cycles()
            .Select(c => c.ToArray());

        Assert.Equal([["A", "B", "C"]], cycles);
    }

    [Fact]
    public void A_unit_that_uses_itself_is_a_cycle()
    {
        var graph = Graph(("A", ["A"]));

        Assert.Single(graph.Cycles());
    }

    [Fact]
    public void A_very_long_chain_does_not_overflow_the_stack()
    {
        var graph = new DependencyGraph();
        const int length = 200_000;

        for (var i = 0; i < length; i++)
        {
            graph.AddNode($"U{i}");
        }

        for (var i = 0; i < length - 1; i++)
        {
            graph.AddUses($"U{i}", [$"U{i + 1}"]);
        }

        graph.AddUses($"U{length - 1}", ["U0"]);

        Assert.Equal(length, Assert.Single(graph.Cycles()).Length);
    }

    [Fact]
    public void Fan_in_counts_distinct_users()
    {
        var graph = Graph(("A", ["C"]), ("B", ["C", "C"]), ("C", []));

        Assert.Equal(2, graph.FanIn()["C"]);
        Assert.Equal(1, graph.FanOut("B"));
    }

    private static DependencyGraph Graph(params (string Name, string[] Uses)[] units)
    {
        var graph = new DependencyGraph();

        foreach (var (name, _) in units)
        {
            graph.AddNode(name);
        }

        foreach (var (name, uses) in units)
        {
            graph.AddUses(name, uses);
        }

        return graph;
    }
}
