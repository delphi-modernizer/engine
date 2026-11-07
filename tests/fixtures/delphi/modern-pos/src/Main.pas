unit Main;

interface

uses
  System.SysUtils, System.Classes, Vcl.Forms, Vcl.Controls, Vcl.StdCtrls, Data.DB,
  FireDAC.Comp.Client, FireDAC.Stan.Def, cxGrid, cxGridDBTableView;

type
  TFrmPos = class(TForm)
    FdConnection: TFDConnection;
    QryProducts: TFDQuery;
    GrdProducts: TcxGrid;
    BtnPay: TButton;
    procedure BtnPayClick(Sender: TObject);
  end;

var
  FrmPos: TFrmPos;

implementation

uses
  OrderService, Payment;

{$R *.dfm}

resourcestring
  SThanks = 'Cảm ơn quý khách';

procedure TFrmPos.BtnPayClick(Sender: TObject);
begin
  TOrderWorker.Create(QryProducts.FieldByName('ID').AsInteger).Start;
end;

end.
