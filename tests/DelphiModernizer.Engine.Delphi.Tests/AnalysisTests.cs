using System.Linq;
using System.Text;
using DelphiModernizer.Engine.Delphi.Analysis;
using DelphiModernizer.Engine.Delphi.Decoding;
using DelphiModernizer.Engine.Delphi.Lexing;
using Xunit;

namespace DelphiModernizer.Engine.Delphi.Tests;

public sealed class AnalysisTests
{
    [Theory]
    [InlineData("Params=Password=hunter2;Server=x", "Params=Password=***;Server=x")]
    [InlineData("pwd = 'abc'", "pwd = ***")]
    [InlineData("create user u identified by Secret1", "create user u identified by ***")]
    [InlineData("Server=x;User=y", "Server=x;User=y")]
    public void Secrets_are_redacted(string text, string expected)
    {
        Assert.Equal(expected, SecretRedactor.Redact(text));
    }

    [Theory]
    [InlineData("FPassword := 'Winter2004';", true)]
    [InlineData("const DbPwd = 'S3cr3t';", true)]
    [InlineData("Edit1.PasswordChar := '*';", false)]
    [InlineData("aTokenType := 'ident';", false)]
    [InlineData("S := 'CREATE LOGIN %s WITH PASSWORD = %s';", false)]
    [InlineData("Args.Add('--password=\"***\"');", false)]
    public void Hard_coded_credentials_are_found_without_flagging_placeholders(string source, bool expected)
    {
        var findings = Analyze(source);

        Assert.Equal(expected, findings.Markers.Any(m => m.Kind == "hard-coded-credential"));
    }

    [Theory]
    [InlineData("select a from t", true, "select")]
    [InlineData("  update T set A = 1", true, "update")]
    [InlineData("EXEC dbo.Charge 1", true, "execute")]
    [InlineData("Please select a file from the list", false, "other")]
    public void Sql_is_recognised_and_classified(string text, bool isSql, string kind)
    {
        Assert.Equal(isSql, SqlClassifier.LooksLikeSql(text) && SqlClassifier.Kind(text) != "other");
        Assert.Equal(kind, isSql ? SqlClassifier.Kind(text) : "other");
    }

    [Fact]
    public void Concatenated_sql_is_flagged_with_placeholders()
    {
        var findings = Analyze("Q.SQL.Text := 'select * from T where Name = ''' + Edit1.Text + '''';");

        var statement = Assert.Single(findings.Sql);
        Assert.True(statement.BuiltByConcatenation);
        Assert.Equal("select * from T where Name = '?'", statement.Text);
        Assert.Contains(findings.Markers, m => m.Kind == "sql-concatenation");
    }

    [Fact]
    public void Markers_are_counted_per_kind_and_anchored_at_the_first_occurrence()
    {
        var findings = Analyze("begin\nwith A do;\nwith B do;\nApplication.ProcessMessages;\nend");

        var with = findings.Markers.Single(m => m.Kind == "with-statement");
        Assert.Equal((2, 2), (with.Line, with.Count));
        Assert.Contains(findings.Markers, m => m.Kind == "process-messages");
    }

    [Fact]
    public void A_method_named_like_a_risky_routine_is_not_a_marker()
    {
        var findings = Analyze("List.Move(1, 2);");

        Assert.DoesNotContain(findings.Markers, m => m.Kind == "raw-memory");
    }

    [Fact]
    public void Decoding_uses_the_byte_order_mark_before_the_code_page()
    {
        var decoder = new SourceDecoder(1252);
        byte[] withMark = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("Cảm ơn")];

        var decoded = decoder.Decode(withMark);

        Assert.Equal(("Cảm ơn", "utf-8", true), (decoded.Text, decoded.Encoding, decoded.HasByteOrderMark));
    }

    [Fact]
    public void Utf8_without_a_mark_is_decoded_with_the_code_page_and_flagged()
    {
        var decoded = new SourceDecoder(1252).Decode(Encoding.UTF8.GetBytes("café"));

        Assert.Equal("windows-1252", decoded.Encoding);
        Assert.True(decoded.IsUtf8WithoutByteOrderMark);
    }

    private static CodeFindings Analyze(string source)
    {
        return CodeAnalyzer.Analyze("A.pas", PascalLexer.Tokenize(source).Tokens, []);
    }
}
