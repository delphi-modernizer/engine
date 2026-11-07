object FrmMain: TFrmMain
  Left = 192
  Top = 114
  Caption = 'ERP '#8212' main window'
  Menu = MainMenu
  object MainMenu: TMainMenu
    Left = 24
    Top = 16
    object MnuCustomers: TMenuItem
      Caption = '&Customers'
      OnClick = MnuCustomersClick
    end
  end
end
