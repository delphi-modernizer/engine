program Pos;

uses
  Vcl.Forms,
  Main in 'src\Main.pas' {FrmPos},
  OrderService in 'src\OrderService.pas',
  Payment in 'src\Payment.pas';

{$R *.res}

begin
  Application.Initialize;
  Application.CreateForm(TFrmPos, FrmPos);
  Application.Run;
end.
