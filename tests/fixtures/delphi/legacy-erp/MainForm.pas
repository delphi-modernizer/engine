unit MainForm;

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs, Menus;

type
  TFrmMain = class(TForm)
    MainMenu: TMainMenu;
    MnuCustomers: TMenuItem;
    procedure MnuCustomersClick(Sender: TObject);
  end;

var
  FrmMain: TFrmMain;

implementation

uses
  CustomerForm;

{$R *.dfm}

procedure TFrmMain.MnuCustomersClick(Sender: TObject);
begin
  FrmCustomer := TFrmCustomer.Create(Self);
  FrmCustomer.ShowModal;
end;

end.
