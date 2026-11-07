using System.Threading;
using System.Threading.Tasks;
using DelphiModernizer.Engine.Core;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>The few git operations a workspace needs, through the git command line.</summary>
internal static class Git
{
    public static async Task<bool> IsRepositoryAsync(string root, CancellationToken cancellationToken)
    {
        var result = await TryRunAsync(root, cancellationToken, "rev-parse", "--is-inside-work-tree");

        return result?.Succeeded == true;
    }

    public static async Task<string?> HeadAsync(string root, CancellationToken cancellationToken)
    {
        var result = await TryRunAsync(root, cancellationToken, "rev-parse", "HEAD");

        return result?.Succeeded == true ? result.StandardOutput.Trim() : null;
    }

    /// <summary>A git config value as git resolves it for <paramref name="root"/>; null when unset.</summary>
    public static async Task<string?> ConfigAsync(string root, string key, CancellationToken cancellationToken)
    {
        var result = await TryRunAsync(root, cancellationToken, "config", key);

        return result?.Succeeded == true ? result.StandardOutput.Trim() : null;
    }

    public static async Task<bool> InitAsync(string root, CancellationToken cancellationToken)
    {
        var result = await TryRunAsync(root, cancellationToken, "init", "--quiet");

        return result?.Succeeded == true;
    }

    public static async Task<bool> CommitAsync(string root, string message, CancellationToken cancellationToken, params string[] paths)
    {
        var add = await TryRunAsync(root, cancellationToken, ["add", "--", .. paths]);

        if (add?.Succeeded != true)
        {
            return false;
        }

        var commit = await TryRunAsync(root, cancellationToken, "commit", "--quiet", "-m", message);

        return commit?.Succeeded == true;
    }

    private static async Task<CommandResult?> TryRunAsync(string root, CancellationToken cancellationToken, params string[] arguments)
    {
        try
        {
            return await CommandRunner.RunAsync("git", arguments, root, cancellationToken);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
