using System.Linq;
using DelphiModernizer.Engine.Delphi.Lexing;
using Xunit;

namespace DelphiModernizer.Engine.Delphi.Tests;

public sealed class PascalLexerTests
{
    [Fact]
    public void Comment_forms_do_not_end_each_other()
    {
        var result = PascalLexer.Tokenize("{ a (* b *) c } (* d { e } f *) x // y\nz");

        var significant = result.Tokens
            .Where(t => t.IsSignificant)
            .Select(t => t.Text);

        Assert.Equal(["x", "z"], significant);
        Assert.Equal(3, result.Tokens.Count(t => t.Kind == TokenKind.Comment));
    }

    [Fact]
    public void A_directive_is_not_a_comment()
    {
        var result = PascalLexer.Tokenize("{$IFDEF DEBUG}(*$R+*)");

        Assert.All(result.Tokens, t => Assert.Equal(TokenKind.Directive, t.Kind));
    }

    [Fact]
    public void Strings_decode_doubled_quotes_and_character_codes()
    {
        var token = Assert.Single(PascalLexer.Tokenize("'It''s'#13#10'ok'#$41").Tokens);

        Assert.Equal(TokenKind.String, token.Kind);
        Assert.Equal("It's\r\nokA", token.Text);
    }

    [Fact]
    public void Slashes_inside_a_string_are_not_a_comment()
    {
        var tokens = PascalLexer.Tokenize("Url := 'http://example.com';").Tokens;

        Assert.DoesNotContain(tokens, t => t.Kind == TokenKind.Comment);
    }

    [Fact]
    public void A_range_is_two_numbers_and_a_symbol()
    {
        var tokens = PascalLexer.Tokenize("1..10").Tokens;

        Assert.Equal(["1", "..", "10"], tokens.Select(t => t.Text));
    }

    [Fact]
    public void Lines_are_counted_across_multi_line_comments()
    {
        var result = PascalLexer.Tokenize("{ one\ntwo }\nx");

        Assert.Equal(3, result.Tokens[^1].Line);
        Assert.Equal(3, result.LineCount);
    }

    [Fact]
    public void An_unterminated_comment_is_reported_not_thrown()
    {
        var result = PascalLexer.Tokenize("x { never closed");

        Assert.Equal("unterminated comment", Assert.Single(result.Diagnostics).Message);
    }
}
