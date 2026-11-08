using System.Threading;
using System.Threading.Tasks;

namespace DelphiModernizer.Engine.Workspaces;

/// <summary>The person git would record as the author of a commit made in a folder.</summary>
public sealed record GitIdentity(string Name, string? Email)
{
    /// <summary>The identity git has for <paramref name="directory"/>; null when user.name is unset or git is missing.</summary>
    public static async Task<GitIdentity?> ReadAsync(string directory, CancellationToken cancellationToken)
    {
        string? name = await Git.ConfigAsync(directory, "user.name", cancellationToken);

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string? email = await Git.ConfigAsync(directory, "user.email", cancellationToken);

        return new GitIdentity(name, string.IsNullOrWhiteSpace(email) ? null : email);
    }

    /// <summary>The identity as approvals and decisions record it: <c>Name &lt;email&gt;</c>.</summary>
    public override string ToString()
    {
        return Email is null ? Name : $"{Name} <{Email}>";
    }
}
