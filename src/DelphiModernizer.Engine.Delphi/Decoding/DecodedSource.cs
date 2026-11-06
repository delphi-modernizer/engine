namespace DelphiModernizer.Engine.Delphi.Decoding;

/// <summary>The text of a legacy file and how it was decoded.</summary>
/// <param name="Text">The decoded text.</param>
/// <param name="Encoding">The encoding that produced <paramref name="Text"/>, as a web name such as <c>utf-8</c> or <c>windows-1252</c>.</param>
/// <param name="HasByteOrderMark">Whether the file started with a byte order mark that chose the encoding.</param>
/// <param name="IsUtf8WithoutByteOrderMark">Whether the bytes, which carry no mark, also form valid UTF-8 with non-ASCII characters; the configured code page was used anyway.</param>
public sealed record DecodedSource(string Text, string Encoding, bool HasByteOrderMark, bool IsUtf8WithoutByteOrderMark);
