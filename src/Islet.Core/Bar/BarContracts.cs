using System.Text.Json;
using System.Text.Json.Serialization;
namespace Islet.Core.Bar;
public enum BarMode { Overlay, Reserve }
public enum BarEdge { Top, Bottom }
public enum BarAlignment { Left, Center, Right }
public enum MonitorSelection { Primary, All, Selected }
public enum BarModule { Cpu, Ram, Network, Battery, Gpu, Disk, Sensors, FrameRate }
public enum BarTheme { Dark, Light, System }
public enum BarItemStyle { Auto, TwoLine, Speed, Battery, Ring, Bar, Sparkline, IconWithText, IconOnly }

public static class BarStyles
{
    private static readonly IReadOnlyList<BarItemStyle> Meters=Array.AsReadOnly(new[]
    {
        BarItemStyle.Auto,BarItemStyle.TwoLine,BarItemStyle.Ring,BarItemStyle.Bar,BarItemStyle.Sparkline,BarItemStyle.IconWithText,BarItemStyle.IconOnly
    });
    private static readonly IReadOnlyList<BarItemStyle> Battery=Array.AsReadOnly(new[]
    {
        BarItemStyle.Auto,BarItemStyle.TwoLine,BarItemStyle.Battery,BarItemStyle.Ring,BarItemStyle.Bar,BarItemStyle.IconOnly
    });
    private static readonly IReadOnlyList<BarItemStyle> Network=Array.AsReadOnly(new[]
    {
        BarItemStyle.Auto,BarItemStyle.TwoLine,BarItemStyle.Speed,BarItemStyle.IconWithText,BarItemStyle.IconOnly
    });
    private static readonly IReadOnlyList<BarItemStyle> Details=Array.AsReadOnly(new[]
    {
        BarItemStyle.Auto,BarItemStyle.TwoLine,BarItemStyle.IconWithText,BarItemStyle.IconOnly
    });
    public static IReadOnlyList<BarItemStyle> Options(BarModule module)=>module switch
    {
        BarModule.Network=>Network,BarModule.Battery=>Battery,
        BarModule.Cpu or BarModule.Gpu or BarModule.Ram=>Meters,_=>Details
    };

    public static BarItemStyle Resolve(BarModule module,BarItemStyle requested,bool withMeters=true)
    {
        if(!Options(module).Contains(requested))requested=BarItemStyle.Auto;
        var style=requested==BarItemStyle.Auto?module switch
        {
            BarModule.Cpu=>BarItemStyle.Ring,BarModule.Gpu=>BarItemStyle.Sparkline,BarModule.Ram=>BarItemStyle.Bar,
            BarModule.Network=>BarItemStyle.Speed,BarModule.Battery=>BarItemStyle.Battery,_=>BarItemStyle.TwoLine
        }:requested;
        return !withMeters&&style is BarItemStyle.Ring or BarItemStyle.Bar or BarItemStyle.Sparkline
            ?BarItemStyle.TwoLine:style;
    }
}
public sealed record ModulePreference
{
    public bool PopupEnabled{get;init;}=true;
    public int UpdateSeconds{get;init;}=1;
    public BarItemStyle Style{get;init;}=BarItemStyle.Auto;
}
public sealed record BarSettings
{
    public int Version {get;init;}=1;
    public BarMode Mode {get;init;}=BarMode.Overlay;
    public BarEdge Edge {get;init;}=BarEdge.Top;
    public bool RevealAtTop {get;init;}
    [JsonIgnore] public bool TopReveal=>Edge==BarEdge.Top&&RevealAtTop;
    public BarAlignment Alignment {get;init;}=BarAlignment.Right;
    public MonitorSelection Monitors {get;init;}=MonitorSelection.Primary;
    public string? SelectedMonitor {get;init;}
    public double Opacity {get;init;}=.8;
    public int IntervalSeconds {get;init;}=3;
    public BarModule[] Items {get;init;}=[BarModule.Cpu,BarModule.Ram,BarModule.Network,BarModule.Battery];
    public BarModule[] ItemOrder {get;init;}=[];
    public bool StripOrderCustomized {get;init;}
    [JsonIgnore] public bool GamingMode {get;init;}
    private static readonly IReadOnlyList<BarModule> DefaultGamingItems=Array.AsReadOnly(new[]{BarModule.FrameRate,BarModule.Gpu,BarModule.Cpu,BarModule.Ram,BarModule.Battery});
    private static readonly IReadOnlyList<BarModule> GamingItemsWithSensors=Array.AsReadOnly(DefaultGamingItems.Where(item=>item!=BarModule.Battery).Append(BarModule.Sensors).Append(BarModule.Battery).ToArray());
    public static IEnumerable<BarModule> ConfigurableModules=>Enum.GetValues<BarModule>().Where(x=>x!=BarModule.FrameRate);
    /// <summary>Auto-enter gaming mode when a full-screen non-shell window is detected on the bar's monitor.</summary>
    public bool GamingModeAuto{get;init;}
    /// <summary>Target FPS for gaming-mode color coding (green ≥ target, amber ≥ half, red below half). Range 30–360.</summary>
    public int FpsTarget{get;init;}=60;
    /// <summary>Percent threshold above which CPU/GPU/RAM items turn amber; ≥ 95% turns red. Range 50–100.</summary>
    public int ThresholdPercent{get;init;}=85;
    public static IReadOnlyList<BarModule> GamingModules=>GamingItemsWithSensors;
    public BarModule[]? GamingStripItems {get;init;}
    public int? GamingRefreshSeconds {get;init;}
    [JsonIgnore] public IReadOnlyList<BarModule> GamingItems=>GamingStripItems is { } items
        ?items:Items.Contains(BarModule.Sensors)?GamingItemsWithSensors:DefaultGamingItems;
    [JsonIgnore] public BarModule[] NormalItems=>StripOrderCustomized||Items.Length==0||Items[^1]==BarModule.Battery||!Items.Contains(BarModule.Battery)
        ?Items:Items.Where(item=>item!=BarModule.Battery).Append(BarModule.Battery).ToArray();
    [JsonIgnore] public IReadOnlyList<BarModule> VisibleItems=>GamingMode?GamingItems:NormalItems;
    [JsonIgnore] public int ActiveIntervalSeconds=>GamingMode?GamingRefreshSeconds??IntervalSeconds:IntervalSeconds;

    public BarSettings WithGamingItemVisibility(BarModule module,bool enabled)
    {
        if(!GamingModules.Contains(module))throw new ArgumentOutOfRangeException(nameof(module));
        var items=GamingModules.Where(item=>item==module?enabled:GamingItems.Contains(item)).ToArray();
        return items.Length==0?this:this with{GamingStripItems=items};
    }
    public bool Pause {get;init;}
    public BarTheme Theme{get;init;}=BarTheme.Dark;
    public string Accent{get;init;}="#0A84FF";
    public bool UseBits{get;init;}
    public bool Fahrenheit{get;init;}
    public bool PingEnabled{get;init;}
    public string PingTarget{get;init;}="";
    public bool WifiDetailsEnabled{get;init;}
    public Dictionary<BarModule,ModulePreference> Modules{get;init;}=[];
    public ModulePreference Preference(BarModule module)=>Modules.GetValueOrDefault(module)??DefaultPreference;
    private static readonly ModulePreference DefaultPreference=new();
    public BarSettings WithItemVisibility(BarModule module,bool enabled)
    {
        Validate();if(!ConfigurableModules.Contains(module))throw new ArgumentOutOfRangeException(nameof(module));
        var order=RememberedOrder();
        return this with{ItemOrder=order,Items=order.Where(item=>item==module?enabled:Items.Contains(item)).ToArray()};
    }
    private BarModule[] RememberedOrder()
    {
        var order=ItemOrder.Concat(Items).Concat(ConfigurableModules).Distinct();
        return (StripOrderCustomized?order:order.Where(item=>item!=BarModule.Battery).Append(BarModule.Battery)).ToArray();
    }

    public BarSettings MoveItemEarlier(BarModule module)=>MoveItem(module,-1);
    public BarSettings MoveItemLater(BarModule module)=>MoveItem(module,1);

    private BarSettings MoveItem(BarModule module,int direction)
    {
        Validate();
        if(!ConfigurableModules.Contains(module))throw new ArgumentOutOfRangeException(nameof(module));
        var visibleItems=NormalItems;
        var visible=Array.IndexOf(visibleItems,module);
        var neighbor=visible+direction;
        if(visible<0||neighbor<0||neighbor>=visibleItems.Length)return this;
        var order=RememberedOrder();
        var index=Array.IndexOf(order,module);
        var other=Array.IndexOf(order,visibleItems[neighbor]);
        (order[index],order[other])=(order[other],order[index]);
        return this with{StripOrderCustomized=true,ItemOrder=order,Items=order.Where(Items.Contains).ToArray()};
    }
    public BarSettings Validate()
    {
        if(Version!=1 || !Enum.IsDefined(Mode)||!Enum.IsDefined(Edge)||!Enum.IsDefined(Alignment)||!Enum.IsDefined(Monitors)
            || !double.IsFinite(Opacity)||Opacity is <.2 or >1 || IntervalSeconds is not (1 or 2 or 3 or 5)
            || Items is null||Items.Length>7||Items.Any(x=>!ConfigurableModules.Contains(x))||Items.Distinct().Count()!=Items.Length
            || ItemOrder is null||ItemOrder.Length>7||ItemOrder.Any(x=>!ConfigurableModules.Contains(x))||ItemOrder.Distinct().Count()!=ItemOrder.Length)
            throw new InvalidDataException("Bar settings contain an unsupported value.");
        if(!Enum.IsDefined(Theme)||Accent is null||Accent.Length!=7||Accent[0]!='#'||Accent.Skip(1).Any(c=>!Uri.IsHexDigit(c))
            ||PingTarget is null||PingTarget.Length>253||PingTarget.Any(c=>char.IsWhiteSpace(c)||c is '/' or '\\')||Modules is null||Modules.Count>7
            ||Modules.Any(p=>!ConfigurableModules.Contains(p.Key)||p.Value is null||!Enum.IsDefined(p.Value.Style)||p.Value.UpdateSeconds is not(1 or 2 or 3 or 5))
        ||GamingRefreshSeconds is not (null or 1 or 2 or 3 or 5)
        ||(GamingStripItems is { } gaming && (gaming.Length==0||gaming.Length>GamingModules.Count
            ||gaming.Any(item=>!GamingModules.Contains(item))||gaming.Distinct().Count()!=gaming.Length))
        ||FpsTarget is <30 or >360||ThresholdPercent is <50 or >100)
            throw new InvalidDataException("Theme or module settings contain an unsupported value.");
        return this;
    }
}
public static class BarSettingsFile
{
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true,Converters={new JsonStringEnumConverter()}};
    public static BarSettings Load(string path,out string? notice)
    {
        notice=null;if(!File.Exists(path))return new();
        try{return (JsonSerializer.Deserialize<BarSettings>(File.ReadAllText(path),Json)??throw new InvalidDataException()).Validate();}
        catch(Exception ex) when(ex is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {notice="Bar settings could not be read; safe defaults are in use. Original file was preserved.";return new();}
    }
    public static void Save(string path,BarSettings settings)
    {
        settings.Validate();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(settings,Json));File.Move(temp,path,true);
    }
    public static string Export(BarSettings settings)=>JsonSerializer.Serialize(settings.Validate(),Json);
    public static BarSettings Import(string json)
    {
        if(json.Length>262144)throw new InvalidDataException("Settings import exceeds 256 KB.");
        return (JsonSerializer.Deserialize<BarSettings>(json,Json)??throw new InvalidDataException("Empty settings import.")).Validate();
    }
}
public readonly record struct BarRect(int Left,int Top,int Right,int Bottom)
{public int Width=>Right-Left;public int Height=>Bottom-Top;}
public sealed record BarDisplay(string Id,bool Primary,BarRect Bounds,BarRect Work);
public readonly record struct BarPlacement(BarRect Pixels,double Scale,BarMode ActualMode,string? Notice);
public sealed record BarSnapshot(double? Cpu,double? Ram,double? Battery,string CpuReason,string RamReason,string NetworkReason,string BatteryReason,
    double? Download,double? Upload,bool Loading=false,bool CpuLoading=false,bool NetworkLoading=false,double? Gpu=null,string GpuReason="GPU readings are not enabled.",bool GpuLoading=false,
    bool CpuRefreshing=false,bool NetworkRefreshing=false,bool GpuRefreshing=false,FrameReading Frames=default,
    double? SensorTemp=null,string SensorReason="Sensors are not enabled.",bool SensorLoading=false);
public readonly record struct BarItemLayout(BarModule Module,double X,double Width);
public static class BarLayout
{
    public const double GamingIndicatorWidth=78;
    public static double HeightDip(BarEdge edge)=>edge==BarEdge.Bottom?34:24;
    public static double HeightDip(BarSettings settings)=>settings.TopReveal?34:HeightDip(settings.Edge);
    private static double Size(BarSettings settings,BarModule module,bool withMeters)=>BarStyles.Resolve(module,settings.Preference(module).Style,withMeters) switch
    {
        BarItemStyle.Ring or BarItemStyle.Bar or BarItemStyle.Sparkline=>84,
        BarItemStyle.Speed=>112,
        BarItemStyle.Battery=>74,
        BarItemStyle.IconOnly=>32,
        BarItemStyle.IconWithText when module==BarModule.Network=>105,
        BarItemStyle.IconWithText=>84,
        _ when module is BarModule.Network or BarModule.FrameRate=>88,
        _=>54
    };
    public static BarRect BottomOverlay(BarDisplay display,BarSettings settings,double scale)=>CompactOverlay(display,settings,scale,BarEdge.Bottom);
    public static BarRect CompactOverlay(BarDisplay display,BarSettings settings,double scale,BarEdge edge)
    {
        var b=display.Bounds;var w=display.Work;
        var left=Math.Max(b.Left,w.Left);var right=Math.Min(b.Right,w.Right);var top=Math.Max(b.Top,w.Top);var bottom=Math.Min(b.Bottom,w.Bottom);
        if(right<=left||bottom<=top){left=b.Left;right=b.Right;top=b.Top;bottom=b.Bottom;}
        var inset=Math.Min((int)Math.Ceiling(8*scale),Math.Max(0,(right-left-1)/2));
        var gap=Math.Min((int)Math.Ceiling(4*scale),Math.Max(0,bottom-top-1));
        var height=Math.Min((int)Math.Ceiling(HeightDip(BarEdge.Bottom)*scale),bottom-top-gap);
        var requested=24+(settings.GamingMode?GamingIndicatorWidth+10:0)+settings.VisibleItems.Sum(module=>Size(settings,module,true))+Math.Max(0,settings.VisibleItems.Count-1)*10;
        var width=Math.Min(right-left-inset*2,(int)Math.Ceiling(requested*scale));
        var x=settings.Alignment switch{BarAlignment.Left=>left+inset,BarAlignment.Right=>right-inset-width,_=>left+(right-left-width)/2};
        var y=edge==BarEdge.Bottom?bottom-gap-height:top+gap;
        return new(x,y,x+width,y+height);
    }
    public static BarItemLayout[] Items(BarSettings settings,double width,bool batteryAvailable)=>Items(settings,width,batteryAvailable,out _);
    public static BarItemLayout[] Items(BarSettings settings,double width,bool batteryAvailable,out double? gamingIndicatorX)
    {
        var modules=settings.VisibleItems.Where(x=>x!=BarModule.Battery||batteryAvailable).ToArray();
        var spacing=Math.Max(0,modules.Length-1)*10;
        var gamingLead=settings.GamingMode?GamingIndicatorWidth+(modules.Length>0?10:0):0;
        var withMeters=modules.Sum(module=>Size(settings,module,true))+spacing+gamingLead+24<=width+.01;
        var total=modules.Sum(module=>Size(settings,module,withMeters))+spacing+gamingLead;
        var start=settings.Alignment switch{BarAlignment.Left=>12,BarAlignment.Right=>Math.Max(12,width-total-12),_=>Math.Max(12,(width-total)/2)};
        gamingIndicatorX=settings.GamingMode?start:null;
        start+=gamingLead;
        var result=new BarItemLayout[modules.Length];for(var i=0;i<result.Length;i++){var itemWidth=Size(settings,modules[i],withMeters);result[i]=new(modules[i],start,itemWidth);start+=itemWidth+10;}return result;
    }
    public static BarModule? Hit(BarItemLayout[] items,double x)=>items.Cast<BarItemLayout?>().FirstOrDefault(i=>x>=i!.Value.X&&x<i.Value.X+i.Value.Width)?.Module;
    public static BarRect Popup(BarRect item,BarRect work,int width,int height,BarEdge edge)
    {
        width=Math.Min(width,work.Width);height=Math.Min(height,work.Height);
        int x=Math.Clamp(item.Left+(item.Width-width)/2,work.Left,work.Right-width);
        int y=Math.Clamp(edge==BarEdge.Top?item.Bottom+6:item.Top-height-6,work.Top,work.Bottom-height);
        return new(x,y,x+width,y+height);
    }
}
public interface IBarDesktop
{
    IReadOnlyList<BarDisplay> Displays();
    BarPoint? CursorPosition()=>null;
    IBarSurface Attach(nint handle,BarDisplay display,BarSettings settings,bool tray);
}
public interface IBarSurface:IDisposable
{
    BarPlacement Placement{get;}
    bool ShouldHide{get;}
    bool TrayPresent{get;}
    bool SlowPolling=>false;
    /// <summary>True when the foreground window covers this bar's monitor and belongs to a non-shell process. Used for auto gaming-mode detection; never blocks the bar.</summary>
    bool IsFullScreenForeground=>false;
    void SetHidden(bool hidden);
    void Reposition();
    int Menu(string? module,bool paused);
    void HandleMessage(int message,nint wParam,nint lParam);
    event Action? EnvironmentChanged;
    event Action? TrayMenuRequested;
}

