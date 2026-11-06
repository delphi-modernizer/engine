using System;
using System.Threading.Tasks;

namespace DelphiModernizer.Cli;

internal static class Program
{
    public static Task<int> Main(string[] args)
    {
        var context = new CliContext(Console.Out, Console.Error, Console.In, Environment.GetEnvironmentVariable, Environment.CurrentDirectory);

        return DmzApp.RunAsync(context, args);
    }
}
