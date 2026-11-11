using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace Islet.Shell.Bar;

/// <summary>Bounded R0 WPF visual-host experiment; no Application, Window, charts or work-area reservation.</summary>
public sealed class BarVisualHost : IDisposable
{
    private readonly HwndSource _source;
    private readonly MetricVisual _root = new();
    private readonly DispatcherTimer _samples;
    private readonly DispatcherTimer _exit;
    private readonly Func<(BarValue Cpu, BarValue Ram)> _read;
    private readonly BarProbeOptions _options;
    private readonly Action<nint,double> _position;
    private bool _closed;
    private Action? _closeMenu;
    public int Samples { get; private set; }
    public int MenuCreations { get; private set; }
    public bool FirstFrame { get; private set; }
    public bool TimersStopped { get; private set; }
    public double Scale { get; private set; }
    public string AccessibleName => _root.AccessibleName;
    public bool AccessibilityPeerVerified { get; private set; }

    public BarVisualHost(BarProbeOptions options, Func<(BarValue Cpu,BarValue Ram)> read, Action<nint,double> position)
    {
        _options=options; _read=read; _position=position;
        var parameters=new HwndSourceParameters("Tidlet R0 WPF visual host")
        {
            Width=240, Height=24, PositionX=100, PositionY=0,
            WindowStyle=unchecked((int)0x80000000), ExtendedWindowStyle=0x00000080,
            UsesPerPixelOpacity=false
        };
        _source=new HwndSource(parameters);
        _source.AddHook(OnMessage);
        _source.RootVisual=_root;
        _source.ContentRendered+=OnRendered;
        _root.MouseRightButtonUp+=(_,e)=>{OpenMenu(false);e.Handled=true;};
        _root.MouseLeftButtonDown+=(_,_)=>_root.Focus();
        _root.KeyDown+=(_,e)=>
        {
            if(e.Key==Key.Escape){Dispose();e.Handled=true;}
            else if(e.Key==Key.Apps || e.Key==Key.F10 && (Keyboard.Modifiers&ModifierKeys.Shift)!=0){OpenMenu(true);e.Handled=true;}
        };
        _samples=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(3)};
        _samples.Tick+=OnSample;
        _exit=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromSeconds(options.ExitSeconds!.Value)};
        _exit.Tick+=OnExit;
        Scale=_source.CompositionTarget.TransformToDevice.M11;
        _root.Measure(new Size(240,24)); _root.Arrange(new Rect(0,0,240,24));
        Sample();
        var peer=UIElementAutomationPeer.CreatePeerForElement(_root);
        AccessibilityPeerVerified=peer?.GetName()==AccessibleName;
        try { _position(_source.Handle,Scale); _samples.Start();_exit.Start(); }
        catch { Dispose();throw; }
    }
    public void Run()=>Dispatcher.Run();
    private void OnRendered(object? sender,EventArgs args)=>FirstFrame=true;
    private void OnSample(object? sender,EventArgs args)=>Sample();
    private void OnExit(object? sender,EventArgs args)=>Dispose();
    private void Sample(){var values=_read();Samples++;_root.Update(values.Cpu,values.Ram,Scale);}
    private nint OnMessage(nint hwnd,int message,nint wParam,nint lParam,ref bool handled)
    {
        if(message==0x0010){Dispose();handled=true;}
        if(message==0x0007)_root.Focus(); // WM_SETFOCUS: explicit user activation can receive keyboard input.
        // WPF handles DPI transforms; reposition after its own processing rather than scaling RootVisual.
        if(message==0x02E0)_root.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
        {
            if(_closed)return;Scale=_source.CompositionTarget.TransformToDevice.M11;_position(hwnd,Scale);Sample();
        }));
        return 0;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void OpenMenu(bool keyboard)
    {
        MenuCreations++;
        _closeMenu?.Invoke();
        _closeMenu=VisualHostMenu.Open(_root,keyboard,Dispose);
    }
    public void Dispose()
    {
        if(_closed)return;_closed=true;
        _samples.Stop();_samples.Tick-=OnSample;_exit.Stop();_exit.Tick-=OnExit;TimersStopped=true;
        try
        {
            if(_options.Capture is{} path && FirstFrame)
            {
                var image=new RenderTargetBitmap((int)Math.Ceiling(240*Scale),(int)Math.Ceiling(24*Scale),96*Scale,96*Scale,PixelFormats.Pbgra32);
                image.Render(_root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream=File.Create(path);encoder.Save(stream);
            }
        }
        finally
        {
            _closeMenu?.Invoke();_source.ContentRendered-=OnRendered;_source.RemoveHook(OnMessage);
            _source.RootVisual=null;_source.Dispose();_root.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        }
    }

    private sealed class MetricVisual : UIElement
    {
        private readonly DrawingVisual _drawing=new();
        private static readonly Typeface LabelFace=new("Segoe UI");
        private static readonly Typeface ValueFace=new(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal);
        public string AccessibleName { get; private set; }="Tidlet monitor waiting for CPU and RAM";
        public MetricVisual(){Focusable=true;AddVisualChild(_drawing);}
        protected override int VisualChildrenCount=>1;
        protected override Visual GetVisualChild(int index)=>index==0?_drawing:throw new ArgumentOutOfRangeException(nameof(index));
        protected override AutomationPeer OnCreateAutomationPeer()=>new MetricPeer(this);
        public void Update(BarValue cpu,BarValue ram,double scale)
        {
            AccessibleName=$"CPU {cpu.Value}. {cpu.Explanation} RAM {ram.Value}. {ram.Explanation}";
            using var draw=_drawing.RenderOpen();
            draw.DrawRectangle(new SolidColorBrush(Color.FromRgb(30,30,32)),null,new Rect(0,0,240,24));
            Item(draw,"CPU",cpu.Value,10,scale);Item(draw,"RAM",ram.Value,84,scale);
        }
        private static void Item(DrawingContext draw,string label,string value,double x,double scale)
        {
            var dim=new SolidColorBrush(Color.FromArgb(153,255,255,255));
            draw.DrawText(new FormattedText(label,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,LabelFace,9,dim,scale),new Point(x,0));
            draw.DrawText(new FormattedText(value,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,ValueFace,12,Brushes.White,scale),new Point(x,9));
        }
        private sealed class MetricPeer(MetricVisual owner) : UIElementAutomationPeer(owner)
        {
            protected override string GetClassNameCore()=>"TidletMetricBar";
            protected override string GetNameCore()=>owner.AccessibleName;
            protected override AutomationControlType GetAutomationControlTypeCore()=>AutomationControlType.Text;
        }
    }
}
