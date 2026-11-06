using System.Linq;
using DelphiModernizer.Engine.Delphi.Parsing;
using Xunit;

namespace DelphiModernizer.Engine.Delphi.Tests;

public sealed class FormParserTests
{
    private const string Form = """
        object FrmMain: TFrmMain
          Caption = 'Main '#8212' window'
          Columns = <
            item
              FieldName = 'X'
            end>
          Picture.Data = {
            0A544A504547}
          object Qry: TQuery
            SQL.Strings = (
              'select *'
              'from T')
          end
          inherited Btn: TButton
            OnClick = BtnClick
          end
        end
        """;

    [Fact]
    public void Components_nest_with_their_depth()
    {
        var form = FormParser.Parse(Form);

        Assert.Equal("FrmMain", form.RootName);
        Assert.Equal([("FrmMain", 0), ("Qry", 1), ("Btn", 1)], form.Components.Select(c => (c.Name, c.Depth)));
    }

    [Fact]
    public void String_lists_join_with_line_feeds()
    {
        var sql = Assert.Single(FormParser.Parse(Form).Sql);

        Assert.Equal("select *\nfrom T", sql.Value);
        Assert.Equal("Qry", sql.Component);
    }

    [Fact]
    public void Event_bindings_name_the_handler()
    {
        var binding = Assert.Single(FormParser.Parse(Form).Events);

        Assert.Equal(("Btn", "OnClick", "BtnClick"), (binding.Component, binding.Event, binding.Handler));
    }
}
