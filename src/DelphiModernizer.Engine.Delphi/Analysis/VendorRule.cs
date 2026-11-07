using System.Text.RegularExpressions;

namespace DelphiModernizer.Engine.Delphi.Analysis;

/// <summary>A third-party library recognised by its unit names and, when it ships components, its class names.</summary>
internal sealed record VendorRule(string Name, string Category, Regex Units, Regex? Classes);
