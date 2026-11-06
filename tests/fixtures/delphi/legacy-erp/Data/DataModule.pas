unit DataModule;

interface

uses
  SysUtils, Classes, DB, DBTables;

type
  TDmMain = class(TDataModule)
    DbErp: TDatabase;
    TblOrders: TTable;
  end;

var
  DmMain: TDmMain;

implementation

{$R *.dfm}

end.
