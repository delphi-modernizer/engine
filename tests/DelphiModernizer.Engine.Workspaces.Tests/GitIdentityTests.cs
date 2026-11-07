using System.IO;
using System.Threading.Tasks;
using DelphiModernizer.Engine.Core;
using Xunit;

namespace DelphiModernizer.Engine.Workspaces.Tests;

public sealed class GitIdentityTests
{
    [Fact]
    public async Task The_identity_is_what_git_resolves_for_the_folder()
    {
        var folder = Directory.CreateTempSubdirectory("dmz-identity-").FullName;
        var cancellationToken = TestContext.Current.CancellationToken;
        await CommandRunner.RunAsync("git", ["init", "--quiet"], folder, cancellationToken);
        await CommandRunner.RunAsync("git", ["config", "user.name", "Test Person"], folder, cancellationToken);
        await CommandRunner.RunAsync("git", ["config", "user.email", "test@example.com"], folder, cancellationToken);

        var identity = await GitIdentity.ReadAsync(folder, cancellationToken);

        Assert.Equal(new GitIdentity("Test Person", "test@example.com"), identity);
        Assert.Equal("Test Person <test@example.com>", identity!.ToString());
        Directory.Delete(folder, recursive: true);
    }
}
