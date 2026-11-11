using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Islet.Shell.Bar;

/// <summary>R0 feasibility surface only. No AppBar reservation, chart, process list, helper, or outbound operation.</summary>
public sealed class BarFeasibilityWindow : Window
{
    private readonly TextBlock _cpu,_ram;
    private readonly Func<(BarValue Cpu,BarValue Ram)> _read;
    private readonly DispatcherTimer _samples;
    private readonly DispatcherTimer? _exit;
    private readonly string? _capture;
    private ContextMenu? _menu;
    public int MenuCreations{get;private set;}
    public int Samples{get;private set;}
    public bool FirstFrame{get;private set;}
    public bool TimersStopped{get;private set;}
    public double Scale{get;private set;}=1;
    public BarFeasibilityWindow(BarProbeOptions options,Func<(BarValue Cpu,BarValue Ram)> read)
    {
        _read=read;_capture=options.Capture;
        Title="Tidlet — R0 WPF feasibility";Width=240;Height=24;WindowStyle=WindowStyle.None;
        ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;
        AllowsTransparency=options.Transparent;Background=new SolidColorBrush(Color.FromArgb(options.Transparent?(byte)204:(byte)255,30,30,32));
        Left=Math.Max(SystemParameters.WorkArea.Left,SystemParameters.WorkArea.Left+(SystemParameters.WorkArea.Width-Width)/2);Top=SystemParameters.WorkArea.Top;
        var row=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10,0,10,0)};
        (_cpu,var cpuItem)=Item("CPU");(_ram,var ramItem)=Item("RAM");row.Children.Add(cpuItem);row.Children.Add(ramItem);Content=row;
        AutomationProperties.SetName(this,"Tidlet monitor feasibility bar");
        MouseRightButtonUp+=(_,e)=>{OpenContextMenu(false);e.Handled=true;};
        KeyDown+=(_,e)=>{if(e.Key==Key.Apps || e.Key==Key.F10 && (Keyboard.Modifiers&ModifierKeys.Shift)!=0){OpenContextMenu(true);e.Handled=true;}};
        _samples=new DispatcherTimer(DispatcherPriority.Background,Dispatcher){Interval=TimeSpan.FromSeconds(3)};
        _samples.Tick+=OnSample;
        if(options.ExitSeconds is{} seconds){_exit=new DispatcherTimer{Interval=TimeSpan.FromSeconds(seconds)};_exit.Tick+=OnExit;}
        ContentRendered+=(_,_)=>{FirstFrame=true;Scale=VisualTreeHelper.GetDpi(this).DpiScaleX;Sample();_samples.Start();_exit?.Start();};
        Closing+=(_,_)=>Capture();Closed+=(_,_)=>{_samples.Stop();_samples.Tick-=OnSample;if(_exit is not null){_exit.Stop();_exit.Tick-=OnExit;}if(_menu is not null)_menu.IsOpen=false;TimersStopped=true;};
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void OpenContextMenu(bool keyboard)
    {
        if(_menu is null)
        {
            _menu=new ContextMenu{PlacementTarget=this};
            var exit=new MenuItem{Header="Exit Tidlet"};exit.Click+=(_,_)=>Close();_menu.Items.Add(exit);MenuCreations++;
        }
        _menu.Placement=keyboard?PlacementMode.Center:PlacementMode.MousePoint;
        _menu.IsOpen=true;
    }
    private static (TextBlock Value,StackPanel Item) Item(string label)
    {
        var panel=new StackPanel{Width=64,Margin=new Thickness(0,0,10,0),VerticalAlignment=VerticalAlignment.Center};
        panel.Children.Add(new TextBlock{Text=label,FontFamily=new FontFamily("Segoe UI"),FontSize=9,Foreground=Brushes.White,Opacity=.6,LineHeight=10});
        var value=new TextBlock{Text="—",FontFamily=new FontFamily("Segoe UI"),FontSize=12,FontWeight=FontWeights.SemiBold,Foreground=Brushes.White,LineHeight=13};
        AutomationProperties.SetName(value,label+" waiting for a reading");panel.Children.Add(value);return(value,panel);
    }
    private void OnSample(object? sender,EventArgs args)=>Sample();
    private void OnExit(object? sender,EventArgs args)=>Close();
    private void Sample()
    {
        var values=_read();Samples++;Update(_cpu,"CPU",values.Cpu);Update(_ram,"RAM",values.Ram);
    }
    private static void Update(TextBlock target,string name,BarValue value)
    {if(target.Text!=value.Value)target.Text=value.Value;target.ToolTip=value.Explanation;AutomationProperties.SetName(target,name+" "+value.Value+". "+value.Explanation);}
    private void Capture()
    {
        if(_capture is null||!FirstFrame)return;
        var dpi=VisualTreeHelper.GetDpi(this);var image=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(ActualWidth*dpi.DpiScaleX)),Math.Max(1,(int)Math.Ceiling(ActualHeight*dpi.DpiScaleY)),96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);
        image.Render(this);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var stream=File.Create(_capture);encoder.Save(stream);
    }
}