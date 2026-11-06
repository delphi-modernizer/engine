unit Utils;

interface

procedure LogValue(const AName: string; const AValue: Currency);
function TickCount: Cardinal; stdcall; external 'kernel32.dll' name 'GetTickCount';

implementation

uses
  SysUtils, Pricing;

const
  DbPassword = 'S3cr3t!';
  Greeting = 'Xin chào khách hàng';

var
  Buffer: ShortString;
  Scratch: string[40];
  Counter: Integer absolute Buffer;

procedure LogValue(const AName: string; const AValue: Currency);
var
  P: Pointer;
begin
  GetMem(P, 16);
  try
    Move(AValue, P^, SizeOf(AValue));
  finally
    FreeMem(P);
  end;
  asm
    nop
  end;
end;

initialization
  Buffer := Greeting;

end.
