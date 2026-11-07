using System.CommandLine;
using System.Threading.Tasks;
using DelphiModernizer.Cli.Commands;

namespace DelphiModernizer.Cli;

/// <summary>The dmz command tree.</summary>
internal static class DmzApp
{
    public static Task<int> RunAsync(CliContext context, string[] args)
    {
        var root = new RootCommand("Delphi Modernizer: governed modernization workspaces for legacy Delphi systems.")
        {
            InitCommand.Create(context),
            ScanCommand.Create(context),
            TraceCommand.Create(context),
            StatusCommand.Create(context),
            ApproveCommand.Create(context, "approve"),
            ApproveCommand.Create(context, "revoke"),
            StackCommand.Create(context),
            GuardCommand.Create(context),
            RecipesCommand.Create(context),
            RunCommand.Create(context),
            VersionCommand.Create(context),
        };
        var configuration = new InvocationConfiguration {
            Output = context.Output,
            Error = context.Error,
        };

        return root
            .Parse(args)
            .InvokeAsync(configuration);
    }
}
