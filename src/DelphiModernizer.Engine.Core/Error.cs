namespace DelphiModernizer.Engine.Core;

/// <summary>A machine-readable error code paired with a human-readable message.</summary>
/// <param name="Code">A stable, dotted identifier such as <c>Workspace.ManifestMissing</c>.</param>
/// <param name="Message">A description suitable for logs and developers.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>The absence of an error, used by successful results.</summary>
    public static readonly Error None = new(string.Empty, string.Empty);
}
