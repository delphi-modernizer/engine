using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DelphiModernizer.Engine.Delphi.Analysis;

/// <summary>The unit uses graph of a legacy tree, restricted to units whose source is present.</summary>
/// <remarks>
/// Pascal identifiers ignore case, so every lookup is ordinal-ignore-case. Cycles come from an iterative Tarjan
/// strongly-connected-components pass, O(V + E) with no recursion limit; see docs/design/dependency-graph.md.
/// </remarks>
internal sealed class DependencyGraph
{
    private readonly Dictionary<string, HashSet<string>> _edges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _displayNames = new(StringComparer.OrdinalIgnoreCase);

    public int NodeCount => _edges.Count;

    public int EdgeCount => _edges.Values.Sum(targets => targets.Count);

    public void AddNode(string name)
    {
        if (_edges.TryAdd(name, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
        {
            _displayNames[name] = name;
        }
    }

    /// <summary>Adds the edges from <paramref name="from"/> to every name in <paramref name="uses"/> that is a node.</summary>
    public void AddUses(string from, IEnumerable<string> uses)
    {
        var targets = _edges[from];

        foreach (string used in uses.Where(u => _edges.ContainsKey(u)))
        {
            targets.Add(used);
        }
    }

    public int FanOut(string name)
    {
        return _edges.TryGetValue(name, out var targets) ? targets.Count : 0;
    }

    public IReadOnlyDictionary<string, int> FanIn()
    {
        var fanIn = _edges.Keys.ToDictionary(k => k, _ => 0, StringComparer.OrdinalIgnoreCase);

        foreach (string target in _edges.Values.SelectMany(targets => targets))
        {
            fanIn[target]++;
        }

        return fanIn;
    }

    /// <summary>Every strongly connected component with more than one unit, or a unit that uses itself.</summary>
    public ImmutableArray<ImmutableArray<string>> Cycles()
    {
        var components = new List<ImmutableArray<string>>();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lowLink = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var onStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        int nextIndex = 0;

        foreach (string root in _edges.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            if (index.ContainsKey(root))
            {
                continue;
            }

            var work = new Stack<(string Node, IEnumerator<string> Targets)>();
            Enter(root);

            while (work.Count > 0)
            {
                var (node, targets) = work.Peek();

                if (targets.MoveNext())
                {
                    string target = targets.Current;

                    if (!index.ContainsKey(target))
                    {
                        Enter(target);
                    }
                    else if (onStack.Contains(target))
                    {
                        lowLink[node] = Math.Min(lowLink[node], index[target]);
                    }

                    continue;
                }

                work.Pop();
                targets.Dispose();

                if (work.Count > 0)
                {
                    string parent = work.Peek().Node;
                    lowLink[parent] = Math.Min(lowLink[parent], lowLink[node]);
                }

                if (lowLink[node] == index[node])
                {
                    var component = PopComponent(node);

                    if (component.Length > 1 || _edges[node].Contains(node))
                    {
                        components.Add(component);
                    }
                }
            }

            void Enter(string node)
            {
                index[node] = nextIndex;
                lowLink[node] = nextIndex;
                nextIndex++;
                stack.Push(node);
                onStack.Add(node);
                var targets = _edges[node]
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .GetEnumerator();
                work.Push((node, targets));
            }
        }

        return [.. components.OrderBy(c => c[0], StringComparer.OrdinalIgnoreCase)];

        ImmutableArray<string> PopComponent(string root)
        {
            var members = new List<string>();
            string member;

            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                members.Add(_displayNames[member]);
            }
            while (!string.Equals(member, root, StringComparison.OrdinalIgnoreCase));

            return [.. members.Order(StringComparer.OrdinalIgnoreCase)];
        }
    }
}
