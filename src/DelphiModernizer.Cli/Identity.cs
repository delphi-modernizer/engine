using System.Threading;
using System.Threading.Tasks;
using DelphiModernizer.Engine.Core;

namespace DelphiModernizer.Cli;

/// <summary>The person running dmz, as git knows them.</summary>
internal static class Identity
{
    public static async Task<string?> OfCurrentUserAsync(CliContext context, CancellationToken cancellationToken)
    {
        try
        {
            var name = await CommandRunner.RunAsync("git", ["config", "user.name"], context.CurrentDirectory, cancellationToken);
            var email = await CommandRunner.RunAsync("git", ["config", "user.email"], context.CurrentDirectory, cancellationToken);
            var trimmedName = name.StandardOutput.Trim();

            if (!name.Succeeded || trimmedName.Length == 0)
            {
                return null;
            }

            return email.Succeeded ? $"{trimmedName} <{email.StandardOutput.Trim()}>" : trimmedName;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
