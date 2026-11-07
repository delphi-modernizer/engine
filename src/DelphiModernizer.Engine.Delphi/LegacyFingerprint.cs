using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DelphiModernizer.Engine.Delphi;

/// <summary>A SHA-256 over every file of a legacy tree, so a gate can tell whether a scan is current.</summary>
public static class LegacyFingerprint
{
    public static string Compute(string root)
    {
        var files = new List<(string Path, byte[] Hash)>();

        foreach (var relative in LegacyFiles.Enumerate(root))
        {
            files.Add((relative, SHA256.HashData(File.ReadAllBytes(Path.Combine(root, relative)))));
        }

        return Combine(files);
    }

    internal static string Combine(IEnumerable<(string Path, byte[] Hash)> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        foreach (var (path, fileHash) in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(path));
            hash.AppendData([0]);
            hash.AppendData(fileHash);
        }

        return "sha256:" + Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
