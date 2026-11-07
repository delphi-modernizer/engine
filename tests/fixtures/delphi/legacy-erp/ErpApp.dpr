program ErpApp;

uses
  Forms,
  MainForm in 'MainForm.pas' {FrmMain},
  CustomerForm in 'Sales\CustomerForm.pas' {FrmCustomer},
  Pricing in 'Sales\Pricing.pas',
  Utils in 'Common\Utils.pas',
  DataModule in 'Data\DataModule.pas' {DmMain: TDataModule},
  Invoice in 'Reports\Invoice.pas' {FrmInvoice};

{$R *.res}

begin
  Application.Initialize;
  Application.CreateForm(TDmMain, DmMain);
  Application.CreateForm(TFrmMain, FrmMain);
  Application.Run;
end.
