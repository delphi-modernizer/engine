unit Comments;

{ A brace comment with (* nested parens *) and a quote ' inside }
(* A paren comment with { braces } inside *)

interface

uses
  SysUtils {$IFDEF MSWINDOWS}, Windows{$ELSE}, Posix.Unistd{$ENDIF};

type
  TPoint3 = packed record
    X, Y, Z: Double;
  end;

  TShape = class abstract(TObject)
  public
    function Area: Double; virtual; abstract;
  end;

  TList<T> = class(TObject)
  end;

  TForward = class;

  TVariant = record
    case Kind: Integer of
      0: (I: Integer);
      1: (D: Double);
  end;

const
  Url = 'http://example.com//not-a-comment';
  Quoted = 'It''s fine';
  Chars = #13#10'tail'#$41;

implementation

procedure Helper; forward;

procedure Helper;
  procedure Local;
  begin
  end;
begin
  Local;
end;

end.
{ unterminated comment at the end
