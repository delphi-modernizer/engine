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

    /// <summary>The person git would record as the author, as <c>Name &lt;email&gt;</c>; null when unset.</summary>
    public static async Task<string?> IdentityAsync(string root, CancellationToken cancellationToken)
    {
        var name = await TryRunAsync(root, cancellationToken, "config", "user.name");
        var email = await TryRunAsync(root, cancellationToken, "config", "user.email");

        if (name?.Succeeded != true || string.IsNullOrWhiteSpace(name.StandardOutput))
        {
            return null;
        }

        return email?.Succeeded == true ? $"{name.StandardOutput.Trim()} <{email.StandardOutput.Trim()}>" : name.StandardOutput.Trim();
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
