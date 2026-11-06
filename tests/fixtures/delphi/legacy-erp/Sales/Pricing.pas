unit Pricing;

interface

function CalculateDiscount(const ACreditLimit: Currency; const ACustomerId: Integer): Currency;

implementation

uses
  Utils;

const
  GoldThreshold = 10000;

var
  LastDiscount: Currency;

function CalculateDiscount(const ACreditLimit: Currency; const ACustomerId: Integer): Currency;
var
  Rate: Extended;
begin
  if ACreditLimit >= GoldThreshold then
    Rate := 0.075
  else
    Rate := 0.025;
  Result := Round(ACreditLimit * Rate * 100) / 100;
  LastDiscount := Result;
  LogValue('discount', Result);
end;

end.
