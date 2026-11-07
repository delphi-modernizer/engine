object DmMain: TDmMain
  OldCreateOrder = False
  Left = 300
  Top = 200
  object DbErp: TDatabase
    AliasName = 'ERP_PROD'
    DatabaseName = 'ERPDB'
    LoginPrompt = False
    Params.Strings = (
      'USER NAME=erp_app'
      'PASSWORD=Winter2004')
    SessionName = 'Default'
    Left = 24
    Top = 16
  end
  object TblOrders: TTable
    DatabaseName = 'ERPDB'
    TableName = 'ORDERS'
    Left = 88
    Top = 16
  end
end
