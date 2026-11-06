unit CustomerForm;

interface

uses
  SysUtils, Classes, Controls, Forms, DB, DBTables, Grids, DBGrids, StdCtrls;

type
  TFrmCustomer = class(TForm)
    QryCustomers: TQuery;
    DsCustomers: TDataSource;
    GrdCustomers: TDBGrid;
    EdtSearch: TEdit;
    BtnSearch: TButton;
    procedure BtnSearchClick(Sender: TObject);
  private
    function DiscountFor(const ACustomerId: Integer): Currency;
  end;

var
  FrmCustomer: TFrmCustomer;

implementation

uses
  Pricing;

{$R *.dfm}

procedure TFrmCustomer.BtnSearchClick(Sender: TObject);
begin
  with QryCustomers do
  begin
    Close;
    SQL.Text := 'select ID, NAME, CREDIT_LIMIT from CUSTOMER where NAME like ''' + EdtSearch.Text + '%''';
    Open;
  end;
  Application.ProcessMessages;
end;

function TFrmCustomer.DiscountFor(const ACustomerId: Integer): Currency;
begin
  Result := CalculateDiscount(QryCustomers.FieldByName('CREDIT_LIMIT').AsCurrency, ACustomerId);
end;

end.
