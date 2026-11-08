using System;
using System.IO;
using System.Threading;

namespace DelphiModernizer.Engine.Agents.FakeClaude;

/// <summary>Arguments: the NDJSON file to replay, the file to record the remaining arguments in, then the runner's own.</summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        File.WriteAllLines(args[1], args[2..]);

        foreach (string line in File.ReadLines(args[0]))
        {
            if (line.StartsWith("#sleep ", StringComparison.Ordinal))
            {
                Thread.Sleep(int.Parse(line[7..], System.Globalization.CultureInfo.InvariantCulture));

                continue;
            }

            if (line.StartsWith("#stderr ", StringComparison.Ordinal))
            {
                Console.Error.WriteLine(line[8..]);

                continue;
            }

            Console.WriteLine(line);
        }

        return 0;
    }
}
