using System;
using System.Text;

namespace DelphiModernizer.Engine.Delphi.Decoding;

/// <summary>Decodes legacy source bytes: a byte order mark decides when present, otherwise the configured code page.</summary>
/// <remarks>
/// Delphi before 2009 saved sources in the ANSI code page, and later versions save UTF-8 with a mark. Bytes without a
/// mark are never guessed to be UTF-8; the result flags them so a person can choose the code page. See
/// docs/design/encodings.md.
/// </remarks>
public sealed class SourceDecoder(int legacyCodePage)
{
    private static readonly UTF8Encoding _strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly byte[] _utf8Mark = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] _utf16LittleEndianMark = [0xFF, 0xFE];
    private static readonly byte[] _utf16BigEndianMark = [0xFE, 0xFF];

    private readonly Encoding _legacyEncoding = Encoding.GetEncoding(legacyCodePage);

    static SourceDecoder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public DecodedSource Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(_utf8Mark))
        {
            return new DecodedSource(Encoding.UTF8.GetString(bytes[3..]), "utf-8", true, false);
        }

        if (bytes.StartsWith(_utf16LittleEndianMark))
        {
            return new DecodedSource(Encoding.Unicode.GetString(bytes[2..]), "utf-16", true, false);
        }

        if (bytes.StartsWith(_utf16BigEndianMark))
        {
            return new DecodedSource(Encoding.BigEndianUnicode.GetString(bytes[2..]), "utf-16BE", true, false);
        }

        if (!bytes.ContainsAnyInRange((byte)0x80, (byte)0xFF))
        {
            return new DecodedSource(Encoding.ASCII.GetString(bytes), "us-ascii", false, false);
        }

        return new DecodedSource(_legacyEncoding.GetString(bytes), _legacyEncoding.WebName, false, IsValidUtf8(bytes));
    }

    private static bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            _strictUtf8.GetCharCount(bytes);

            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
