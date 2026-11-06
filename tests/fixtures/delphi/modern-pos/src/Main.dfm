object FrmPos: TFrmPos
  Left = 0
  Top = 0
  Caption = 'Point of sale'
  object GrdProducts: TcxGrid
    Left = 8
    Top = 8
  end
  object BtnPay: TButton
    Left = 8
    Top = 320
    Caption = 'Pay'
    OnClick = BtnPayClick
  end
  object FdConnection: TFDConnection
    Params.Strings = (
      'Database=pos'
      'User_Name=pos_user'
      'Password=hunter2'
      'Server=db.internal'
      'DriverID=MSSQL')
    LoginPrompt = False
    Left = 120
    Top = 8
  end
  object QryProducts: TFDQuery
    Connection = FdConnection
    SQL.Strings = (
      'select ID, NAME, PRICE'
      'from PRODUCT'
      'where ACTIVE = 1')
    Left = 160
    Top = 8
  end
end
