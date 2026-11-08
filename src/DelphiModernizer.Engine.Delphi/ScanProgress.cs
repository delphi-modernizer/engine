namespace DelphiModernizer.Engine.Delphi;

/// <summary>How far a scan has got: files read of the files found in the legacy tree.</summary>
public sealed record ScanProgress(int FilesDone, int FilesTotal);
