unit OrderService;

{$Q-}
{$R-}

interface

uses
  System.Classes, System.Win.ComObj, System.Variants;

type
  TOrderWorker = class(TThread)
  private
    FOrderId: Integer;
  protected
    procedure Execute; override;
  public
    constructor Create(const AOrderId: Integer);
  end;

implementation

uses
  Payment;

constructor TOrderWorker.Create(const AOrderId: Integer);
begin
  inherited Create(True);
  FreeOnTerminate := True;
  FOrderId := AOrderId;
end;

procedure TOrderWorker.Execute;
var
  Excel: Variant;
begin
  Excel := CreateOleObject('Excel.Application');
  Excel.Visible := False;
  ChargeOrder(FOrderId);
end;

end.
