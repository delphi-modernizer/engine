using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using DelphiModernizer.Engine.Delphi.Inventory;

namespace DelphiModernizer.Engine.Delphi.Parsing;

/// <summary>Reads Delphi project files: MSBuild .dproj and .groupproj, .bdsproj, and the .dof and .cfg option files.</summary>
internal static class ProjectFileParser
{
    /// <remarks>Security decision: legacy XML is untrusted, so DTDs and external entities are refused.</remarks>
    private static readonly XmlReaderSettings _xmlSettings = new() {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
    };

    public static ProjectInfo Parse(string path, string text)
    {
        string extension = Path
            .GetExtension(path)
            .ToLowerInvariant();

        return extension switch {
            ".dproj" => ParseDproj(path, text),
            ".groupproj" => ParseGroup(path, text),
            ".bdsproj" => ParseBdsproj(path, text),
            _ => ParseOptions(path, text),
        };
    }

    public static ProjectInfo FromSource(string path, ParsedUnit unit)
    {
        var references = unit.Kind == "package" ? unit.Contains : unit.Uses;

        return new ProjectInfo(
            path,
            unit.Kind,
            unit.Name,
            null,
            null,
            null,
            [],
            [],
            [
                .. references.Select(r => new ProjectReference(r.Name, r.Path)),
                .. unit.Requires.Select(r => new ProjectReference(r, null)),
            ]);
    }

    /// <summary>Maps a .dproj ProjectVersion to the band of Delphi releases that write it.</summary>
    /// <remarks>Coarse by design: several releases share a major value. docs/SCANNER.md lists the bands; confirm with the team.</remarks>
    public static string? DelphiVersionOf(string? projectVersion)
    {
        if (!decimal.TryParse(projectVersion, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal version))
        {
            return null;
        }

        return Math.Floor(version) switch {
            < 12 => "Delphi 2007",
            12 => "Delphi 2009 to XE",
            >= 13 and < 18 => "Delphi XE2 to XE8",
            18 => "Delphi 10 Seattle to 10.3 Rio",
            19 => "Delphi 10.4 Sydney to 11 Alexandria",
            _ => "Delphi 12 Athens or later",
        };
    }

    private static ProjectInfo ParseDproj(string path, string text)
    {
        var document = Load(text);
        string? projectVersion = Values(document, "ProjectVersion").FirstOrDefault();
        string? framework = Values(document, "FrameworkType").FirstOrDefault();
        string? mainSource = Values(document, "MainSource").FirstOrDefault();
        var defines = Values(document, "DCC_Define")
            .SelectMany(d => d.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(d => !d.StartsWith("$(", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
        var platforms = Elements(document, "Platform")
            .Where(e => string.Equals(e.Value.Trim(), "True", StringComparison.OrdinalIgnoreCase))
            .Select(e => (string?)e.Attribute("value"))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
        var references = Elements(document, "DCCReference")
            .Select(e => (string?)e.Attribute("Include"))
            .OfType<string>()
            .Select(include => new ProjectReference(Path.GetFileNameWithoutExtension(include.Replace('\\', '/')), include));

        if (mainSource is not null)
        {
            references = references.Prepend(new ProjectReference(Path.GetFileNameWithoutExtension(mainSource), mainSource));
        }

        return new ProjectInfo(
            path,
            "project",
            Path.GetFileNameWithoutExtension(path),
            framework,
            DelphiVersionOf(projectVersion),
            projectVersion,
            [.. platforms],
            [.. defines],
            [.. references]);
    }

    private static ProjectInfo ParseGroup(string path, string text)
    {
        var references = Elements(Load(text), "Projects")
            .Select(e => (string?)e.Attribute("Include"))
            .OfType<string>()
            .Select(include => new ProjectReference(Path.GetFileNameWithoutExtension(include.Replace('\\', '/')), include));

        return new ProjectInfo(path, "group", Path.GetFileNameWithoutExtension(path), null, null, null, [], [], [.. references]);
    }

    private static ProjectInfo ParseBdsproj(string path, string text)
    {
        string? mainSource = Elements(Load(text), "Source")
            .Where(e => string.Equals((string?)e.Attribute("Name"), "MainSource", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Value.Trim())
            .FirstOrDefault();
        ImmutableArray<ProjectReference> references = mainSource is null ? [] : [new ProjectReference(Path.GetFileNameWithoutExtension(mainSource), mainSource)];

        return new ProjectInfo(path, "project", Path.GetFileNameWithoutExtension(path), "VCL", "Delphi 2005 or 2006", null, [], [], references);
    }

    private static ProjectInfo ParseOptions(string path, string text)
    {
        string? version = null;
        var defines = new List<string>();
        string section = string.Empty;

        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1];

                continue;
            }

            int separator = line.IndexOf('=');

            if (separator < 0)
            {
                AddCfgDefines(line, defines);

                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();

            if (section.Equals("FileVersion", StringComparison.OrdinalIgnoreCase) && key.Equals("Version", StringComparison.OrdinalIgnoreCase))
            {
                version = value;
            }
            else if (key.Equals("Conditionals", StringComparison.OrdinalIgnoreCase))
            {
                defines.AddRange(value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        bool isDof = path.EndsWith(".dof", StringComparison.OrdinalIgnoreCase);
        string? delphiVersion = (version, isDof) switch {
            (not null, _) => $"Delphi {version}",
            (null, true) => "Delphi 7 or earlier",
            _ => null,
        };

        return new ProjectInfo(
            path,
            "options",
            Path.GetFileNameWithoutExtension(path),
            null,
            delphiVersion,
            version,
            [],
            [.. defines
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)],
            []);
    }

    private static void AddCfgDefines(string line, List<string> defines)
    {
        if (line.StartsWith("-D", StringComparison.OrdinalIgnoreCase))
        {
            string[] values = line[2..]
                .Trim('"')
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            defines.AddRange(values);
        }
    }

    private static XDocument Load(string text)
    {
        using var reader = XmlReader.Create(new StringReader(text), _xmlSettings);

        return XDocument.Load(reader);
    }

    private static IEnumerable<XElement> Elements(XDocument document, string localName)
    {
        return document
            .Descendants()
            .Where(e => e.Name.LocalName.Equals(localName, StringComparison.Ordinal));
    }

    private static IEnumerable<string> Values(XDocument document, string localName)
    {
        return Elements(document, localName)
            .Select(e => e.Value.Trim())
            .Where(v => v.Length > 0);
    }
}
