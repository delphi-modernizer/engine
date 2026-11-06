unit Payment;

interface

procedure ChargeOrder(const AOrderId: Integer);

implementation

uses
  System.SysUtils, IdHTTP, OrderService;

procedure ChargeOrder(const AOrderId: Integer);
var
  Http: TIdHTTP;
  Sql: string;
begin
  Sql := Format('update ORDERS set PAID = 1 where ID = %s', [IntToStr(AOrderId)]);
  Http := TIdHTTP.Create(nil);
  try
    Http.Get('https://payments.example/charge?order=' + IntToStr(AOrderId));
  finally
    Http.Free;
  end;
end;

end.
