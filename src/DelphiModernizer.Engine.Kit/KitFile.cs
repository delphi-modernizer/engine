namespace DelphiModernizer.Engine.Kit;

/// <summary>A file of the kit, by its path relative to the folder that holds it.</summary>
public sealed record KitFile(string Path, byte[] Content);
