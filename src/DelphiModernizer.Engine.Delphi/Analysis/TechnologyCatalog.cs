using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;

namespace DelphiModernizer.Engine.Delphi.Analysis;

/// <summary>What the scanner knows about Delphi's own units and common third-party libraries.</summary>
/// <remarks>
/// Exact names live in frozen dictionaries, built once and read on every token; prefix families are ordered rules.
/// Matches are evidence for a person to confirm, not proof. See docs/design/catalogs.md.
/// </remarks>
internal static partial class TechnologyCatalog
{
    public static readonly FrozenDictionary<string, string> DatabaseUnits = Map(
        ("BDE", ["DBTables", "BDE", "Bde.DBTables", "DBiProcs", "DBiTypes"]),
        ("ADO (dbGo)", ["ADODB", "Data.Win.ADODB", "ADOInt", "Data.Win.ADOConEd"]),
        ("dbExpress", ["SqlExpr", "Data.SqlExpr", "DBXCommon", "Data.DBXCommon", "DBXpress", "SimpleDS"]),
        ("FireDAC", ["FireDAC.Comp.Client", "FireDAC.Comp.DataSet", "FireDAC.Stan.Def", "FireDAC.Phys"]),
        ("AnyDAC", ["uADCompClient", "uADCompDataSet", "uADStanDef"]),
        ("InterBase Express", ["IBDatabase", "IBQuery", "IBTable", "IBCustomDataSet", "IBSQL", "IBStoredProc", "IBX.IBDatabase", "IBX.IBQuery"]),
        ("IBObjects", ["IB_Components", "IBODataset"]),
        ("Zeos", ["ZConnection", "ZDataset", "ZAbstractRODataset", "ZStoredProcedure"]),
        ("UniDAC", ["Uni", "UniProvider", "DBAccess"]),
        ("ODAC", ["Ora", "OraSmart"]),
        ("Direct Oracle Access", ["Oracle", "OracleData"]),
        ("DBISAM", ["dbisamtb"]),
        ("DataSnap / MIDAS", ["DBClient", "Datasnap.DBClient", "Provider", "Datasnap.Provider", "MConnect", "MidasLib"]));

    public static readonly FrozenDictionary<string, string> DatabaseClasses = Map(
        ("BDE", ["TTable", "TQuery", "TDatabase", "TSession", "TStoredProc", "TUpdateSQL", "TBatchMove"]),
        ("ADO (dbGo)", ["TADOConnection", "TADOQuery", "TADOTable", "TADOStoredProc", "TADODataSet", "TADOCommand"]),
        ("dbExpress", ["TSQLConnection", "TSQLQuery", "TSQLTable", "TSQLDataSet", "TSQLStoredProc", "TSimpleDataSet"]),
        ("FireDAC", ["TFDConnection", "TFDQuery", "TFDTable", "TFDStoredProc", "TFDCommand", "TFDTransaction"]),
        ("AnyDAC", ["TADConnection", "TADQuery", "TADTable", "TADStoredProc"]),
        ("InterBase Express", ["TIBDatabase", "TIBQuery", "TIBTable", "TIBTransaction", "TIBDataSet", "TIBStoredProc", "TIBSQL"]),
        ("IBObjects", ["TIB_Connection", "TIBOQuery", "TIBOTable", "TIBODatabase"]),
        ("Zeos", ["TZConnection", "TZQuery", "TZTable", "TZReadOnlyQuery", "TZStoredProc"]),
        ("UniDAC", ["TUniConnection", "TUniQuery", "TUniTable", "TUniStoredProc"]),
        ("ODAC", ["TOraSession", "TOraQuery", "TOraTable", "TOraStoredProc"]),
        ("Direct Oracle Access", ["TOracleSession", "TOracleDataSet", "TOracleQuery"]),
        ("DBISAM", ["TDBISAMDatabase", "TDBISAMTable", "TDBISAMQuery", "TDBISAMSession"]),
        ("DataSnap / MIDAS", ["TClientDataSet", "TDataSetProvider", "TDCOMConnection", "TSocketConnection"]));

    /// <summary>Classes that hold a database connection, so their properties carry connection settings.</summary>
    public static readonly FrozenSet<string> ConnectionClasses = new[]
    {
        "TDatabase", "TADOConnection", "TSQLConnection", "TFDConnection", "TADConnection", "TIBDatabase",
        "TIB_Connection", "TIBODatabase", "TZConnection", "TUniConnection", "TOraSession", "TOracleSession",
        "TDBISAMDatabase",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static readonly ImmutableArray<VendorRule> Vendors =
    [
        new("DevExpress", "ui", DevExpressUnits(), DevExpressClasses()),
        new("TMS Software", "ui", TmsUnits(), TmsClasses()),
        new("FastReport", "report", FastReportUnits(), FastReportClasses()),
        new("QuickReport", "report", QuickReportUnits(), QuickReportClasses()),
        new("ReportBuilder", "report", ReportBuilderUnits(), ReportBuilderClasses()),
        new("Rave Reports", "report", RaveUnits(), RaveClasses()),
        new("Indy", "network", IndyUnits(), IndyClasses()),
        new("ICS (OverByte)", "network", IcsUnits(), IcsClasses()),
        new("Synapse", "network", SynapseUnits(), null),
        new("JEDI VCL", "ui", JvclUnits(), JvclClasses()),
        new("JEDI Code Library", "other", JclUnits(), null),
        new("Raize / Konopka", "ui", RaizeUnits(), RaizeClasses()),
        new("LMD", "ui", LmdUnits(), LmdClasses()),
        new("RxLib", "ui", RxUnits(), RxClasses()),
        new("Virtual TreeView", "ui", VirtualTreesUnits(), VirtualTreesClasses()),
        new("SynEdit", "ui", SynEditUnits(), SynEditClasses()),
        new("mORMot", "other", MormotUnits(), null),
        new("RemObjects", "network", RemObjectsUnits(), RemObjectsClasses()),
        new("TeeChart", "chart", TeeChartUnits(), TeeChartClasses()),
        new("OmniThreadLibrary", "other", OmniThreadUnits(), null),
        new("Spring4D", "other", Spring4DUnits(), null),
        new("DUnit", "testing", DUnitUnits(), null),
        new("DUnitX", "testing", DUnitXUnits(), null),
        new("madExcept", "diagnostics", MadExceptUnits(), null),
        new("EurekaLog", "diagnostics", EurekaLogUnits(), null),
    ];

    private static readonly FrozenSet<string> _runtimeUnits = new[]
    {
        "System", "SysInit", "SysUtils", "Classes", "Windows", "Messages", "Graphics", "Controls", "Forms",
        "Dialogs", "StdCtrls", "ExtCtrls", "ComCtrls", "Menus", "Buttons", "Grids", "DBGrids", "DB", "DBCtrls",
        "Variants", "Math", "StrUtils", "DateUtils", "IniFiles", "Registry", "ShellAPI", "ActiveX", "ComObj",
        "OleCtrls", "OleServer", "Printers", "Clipbrd", "ImgList", "ActnList", "ToolWin", "Mask", "CheckLst",
        "FileCtrl", "Types", "TypInfo", "Contnrs", "SyncObjs", "XMLDoc", "XMLIntf", "xmldom", "msxmldom",
        "MaskUtils", "ConvUtils", "Jpeg", "pngimage", "GIFImg", "AppEvnts", "ExtDlgs", "CommCtrl", "ShlObj",
        "MMSystem", "WinSock", "Winsock2", "WinInet", "UrlMon", "Imm", "RichEdit", "ValEdit", "Tabs", "Spin",
        "Gauges", "OleAuto", "Ole2", "StdActns", "DBActns", "ExtActns", "ListActns", "XPMan", "Themes", "UxTheme",
        "Character", "RTLConsts", "Consts", "DBConsts", "Masks", "MultiMon", "PsAPI", "TlHelp32", "IOUtils",
        "RegularExpressions", "AnsiStrings", "WideStrUtils", "ZLib", "EncdDecd", "NetEncoding", "Hash", "JSON",
        "Generics.Collections", "Generics.Defaults", "Diagnostics", "TimeSpan", "Sockets", "ScktComp", "DdeMan",
        "Mapi", "ComServ", "AxCtrls", "StdVCL", "ShDocVw", "MSHTML", "Midas", "DBCommon", "DBLogDlg", "DBPWDlg",
        "CustomizeDlg", "ColorGrd", "Outline", "Calendar",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> _standardClasses = new[]
    {
        "TForm", "TFrame", "TDataModule", "TButton", "TBitBtn", "TSpeedButton", "TEdit", "TMaskEdit", "TLabel",
        "TPanel", "TMemo", "TRichEdit", "TComboBox", "TListBox", "TCheckBox", "TRadioButton", "TGroupBox",
        "TRadioGroup", "TMainMenu", "TPopupMenu", "TMenuItem", "TTimer", "TImage", "TStringGrid", "TDrawGrid",
        "TDBGrid", "TDBEdit", "TDBText", "TDBMemo", "TDBNavigator", "TDBCheckBox", "TDBComboBox", "TDBRadioGroup",
        "TDBImage", "TDBLookupComboBox", "TDBLookupListBox", "TDataSource", "TPageControl", "TTabSheet",
        "TStatusBar", "TToolBar", "TToolButton", "TImageList", "TActionList", "TAction", "TOpenDialog",
        "TSaveDialog", "TPrintDialog", "TFontDialog", "TColorDialog", "TSplitter", "TTreeView", "TListView",
        "TProgressBar", "TDateTimePicker", "TMonthCalendar", "TScrollBox", "TShape", "TBevel", "TApplicationEvents",
        "TTrackBar", "TUpDown", "TCheckListBox", "TValueListEditor", "TControlBar", "TCoolBar", "TActionManager",
        "TActionMainMenuBar", "TActionToolBar", "TTrayIcon", "TFlowPanel", "TGridPanel", "TLinkLabel",
        "TCategoryPanelGroup", "TButtonedEdit", "TStaticText", "TScrollBar", "THeaderControl", "TAnimate",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly ImmutableArray<string> _runtimeNamespaces =
    [
        "System.", "Vcl.", "Winapi.", "Data.", "FMX.", "Xml.", "Soap.", "Web.", "Datasnap.", "REST.", "Bde.",
        "FireDAC.", "IBX.", "Posix.", "Macapi.", "Androidapi.", "iOSapi.",
    ];

    public static string CategoryOfClass(string className, VendorRule? vendor)
    {
        if (DatabaseClasses.ContainsKey(className))
        {
            return "database";
        }

        if (vendor is not null)
        {
            return vendor.Category;
        }

        return _standardClasses.Contains(className) ? "standard" : "other";
    }

    public static bool IsRuntimeUnit(string unit)
    {
        return _runtimeUnits.Contains(unit)
            || _runtimeNamespaces.Any(prefix => unit.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static VendorRule? VendorOfUnit(string unit)
    {
        return Vendors.FirstOrDefault(v => v.Units.IsMatch(unit));
    }

    public static VendorRule? VendorOfClass(string className)
    {
        return Vendors.FirstOrDefault(v => v.Classes?.IsMatch(className) == true);
    }

    private static FrozenDictionary<string, string> Map(params (string Technology, string[] Names)[] groups)
    {
        return groups
            .SelectMany(g => g.Names.Select(name => KeyValuePair.Create(name, g.Technology)))
            .ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^(cx|dx)[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DevExpressUnits();

    [GeneratedRegex(@"^T(cx|dx)[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DevExpressClasses();

    [GeneratedRegex(@"^(Adv[A-Z]|TMS[A-Z]|AdvGrid|BaseGrid|AsgLinks)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TmsUnits();

    [GeneratedRegex(@"^T(Adv|TMS)[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TmsClasses();

    [GeneratedRegex(@"^(frx|fs_|frxClass)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FastReportUnits();

    [GeneratedRegex(@"^Tfrx[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FastReportClasses();

    [GeneratedRegex(@"^(QuickRpt|QRCtrls|QRPrntr|QRExport|QRExtra)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuickReportUnits();

    [GeneratedRegex(@"^(TQuickRep|TQR[A-Z]\w*)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex QuickReportClasses();

    [GeneratedRegex(@"^pp[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReportBuilderUnits();

    [GeneratedRegex(@"^Tpp[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ReportBuilderClasses();

    [GeneratedRegex(@"^Rv[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RaveUnits();

    [GeneratedRegex(@"^TRv[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RaveClasses();

    [GeneratedRegex(@"^Id[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IndyUnits();

    [GeneratedRegex(@"^TId[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IndyClasses();

    [GeneratedRegex(@"^OverbyteIcs", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IcsUnits();

    [GeneratedRegex(@"^(TWSocket|THttpCli|TSmtpCli|TFtpClient)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IcsClasses();

    [GeneratedRegex(@"^(blcksock|httpsend|synautil|synacode|smtpsend|ftpsend)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SynapseUnits();

    [GeneratedRegex(@"^Jv[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JvclUnits();

    [GeneratedRegex(@"^TJv[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JvclClasses();

    [GeneratedRegex(@"^Jcl[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JclUnits();

    [GeneratedRegex(@"^Rz[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RaizeUnits();

    [GeneratedRegex(@"^TRz[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RaizeClasses();

    [GeneratedRegex(@"^LMD", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LmdUnits();

    [GeneratedRegex(@"^TLMD", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LmdClasses();

    [GeneratedRegex(@"^Rx[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RxUnits();

    [GeneratedRegex(@"^TRx[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RxClasses();

    [GeneratedRegex(@"^VirtualTrees", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VirtualTreesUnits();

    [GeneratedRegex(@"^TVirtual(String|Draw)Tree$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VirtualTreesClasses();

    [GeneratedRegex(@"^Syn(Edit|Highlighter|Memo|Completion|Export)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SynEditUnits();

    [GeneratedRegex(@"^TSyn[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SynEditClasses();

    [GeneratedRegex(@"^(mORMot|SynCommons|SynCrypto|SynDB|SynLog|SynTable)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MormotUnits();

    [GeneratedRegex(@"^uRO[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RemObjectsUnits();

    [GeneratedRegex(@"^TRO[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RemObjectsClasses();

    [GeneratedRegex(@"^(TeEngine|TeeProcs|Chart|Series|DBChart|TeeTools|VclTee\.)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TeeChartUnits();

    [GeneratedRegex(@"^T(Chart|DBChart|\w+Series)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TeeChartClasses();

    [GeneratedRegex(@"^Otl[A-Z]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OmniThreadUnits();

    [GeneratedRegex(@"^Spring(\.|$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Spring4DUnits();

    [GeneratedRegex(@"^(TestFramework|GUITestRunner|TextTestRunner|TestExtensions)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DUnitUnits();

    [GeneratedRegex(@"^DUnitX\.", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DUnitXUnits();

    [GeneratedRegex(@"^mad(Except|LinkDisAsm|ListHardware|ListProcesses|ListModules)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MadExceptUnits();

    [GeneratedRegex(@"^(ExceptionLog|ExceptionLog7|EurekaLog|ECore|EBase|EModules)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EurekaLogUnits();
}
