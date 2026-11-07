using System.Linq;
using DelphiModernizer.Engine.Delphi.Lexing;
using DelphiModernizer.Engine.Delphi.Parsing;
using Xunit;

namespace DelphiModernizer.Engine.Delphi.Tests;

public sealed class UnitParserTests
{
    [Fact]
    public void Interface_and_implementation_uses_are_kept_apart()
    {
        var unit = Parse("unit A; interface uses B, C.D; implementation uses E; end.");

        Assert.Equal("A", unit.Name);
        Assert.Equal(["B", "C.D"], unit.Uses.Select(u => u.Name));
        Assert.Equal(["E"], unit.ImplementationUses.Select(u => u.Name));
    }

    [Fact]
    public void A_program_records_the_paths_of_its_units()
    {
        var unit = Parse("program P; uses Forms, Main in 'src\\Main.pas' {FrmMain}; begin end.");

        Assert.Equal("program", unit.Kind);
        Assert.Equal("src\\Main.pas", unit.Uses.Single(u => u.Name == "Main").Path);
    }

    [Fact]
    public void Types_record_kind_and_ancestors_and_skip_forward_declarations()
    {
        var unit = Parse("""
            unit A; interface
            type
              TFwd = class;
              TBase = class abstract(TObject, IInterface) end;
              TRec = packed record X: Integer; end;
              TRef = class of TBase;
              IThing = interface(IInterface) ['{00000000-0000-0000-0000-000000000000}'] end;
            implementation end.
            """);

        Assert.Equal(["TBase", "TRec", "IThing"], unit.Types.Select(t => t.Name));
        Assert.Equal(["TObject", "IInterface"], unit.Types[0].Ancestors);
        Assert.Equal("interface", unit.Types[2].Kind);
    }

    [Fact]
    public void Routines_with_bodies_are_counted_and_forward_or_external_ones_are_not()
    {
        var unit = Parse("""
            unit A; interface procedure Shown; implementation
            procedure Later; forward;
            function Tick: Cardinal; stdcall; external 'kernel32.dll' name 'GetTickCount';
            procedure Shown; procedure Local; begin end; begin Local; end;
            procedure Later; begin case 1 of 1: ; end; end;
            end.
            """);

        Assert.Equal(3, unit.Routines);
    }

    [Fact]
    public void Unit_level_variables_are_counted_but_routine_locals_are_not()
    {
        var unit = Parse("""
            unit A; interface var X, Y: Integer; implementation
            var Z: record A: Integer; end;
            procedure P; var Local: Integer; begin end;
            end.
            """);

        Assert.Equal(3, unit.GlobalVariables);
    }

    [Fact]
    public void A_variant_record_case_does_not_need_its_own_end()
    {
        var unit = Parse("""
            unit A; interface type TV = record case K: Integer of 0: (I: Integer); 1: (D: Double); end;
            var After: Integer;
            implementation end.
            """);

        Assert.Equal(1, unit.GlobalVariables);
    }

    [Fact]
    public void Conditional_directives_inside_uses_are_flagged()
    {
        var unit = Parse("unit A; interface uses SysUtils {$IFDEF MSWINDOWS}, Windows{$ENDIF}; implementation end.");

        Assert.True(unit.HasConditionalUses);
        Assert.Equal(["SysUtils", "Windows"], unit.Uses.Select(u => u.Name));
    }

    private static ParsedUnit Parse(string source)
    {
        return UnitParser.Parse(PascalLexer.Tokenize(source).Tokens);
    }
}
