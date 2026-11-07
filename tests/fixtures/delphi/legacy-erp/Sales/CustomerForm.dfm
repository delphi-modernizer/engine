object FrmCustomer: TFrmCustomer
  Left = 200
  Top = 120
  Caption = 'Customers'
  object GrdCustomers: TDBGrid
    Left = 8
    Top = 40
    DataSource = DsCustomers
  end
  object EdtSearch: TEdit
    Left = 8
    Top = 8
  end
  object BtnSearch: TButton
    Left = 160
    Top = 8
    Caption = 'Search'
    OnClick = BtnSearchClick
  end
  object QryCustomers: TQuery
    DatabaseName = 'ERPDB'
    SQL.Strings = (
      'select ID, NAME, CREDIT_LIMIT'
      'from CUSTOMER'
      'order by NAME')
    Left = 240
    Top = 8
  end
  object DsCustomers: TDataSource
    DataSet = QryCustomers
    Left = 272
    Top = 8
  end
end
