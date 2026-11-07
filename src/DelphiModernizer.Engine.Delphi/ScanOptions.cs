namespace DelphiModernizer.Engine.Delphi;

/// <param name="LegacyCodePage">The code page for files without a byte order mark, such as 1252 or 1258.</param>
/// <param name="MaximumFileBytes">Files larger than this are counted but not parsed.</param>
public sealed record ScanOptions(int LegacyCodePage = 1252, long MaximumFileBytes = 32 * 1024 * 1024);
