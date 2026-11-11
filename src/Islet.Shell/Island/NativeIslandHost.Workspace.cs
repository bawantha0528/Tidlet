using System.IO;
using Islet.Core.Island;
using Islet.Core.Presentation;
using Islet.Core.Settings;
namespace Islet.Shell.Island;

public sealed partial class NativeIslandHost
{
    private readonly IMonitorWindowDesktop? _applicationDesktop;
    private readonly ISettingsStore? _settingsStore;
    private SettingsDocument _preferences=new();
    private readonly string _deviceDescription;
    private readonly EdgeBarReveal _edgeBar=new();
    private bool _applicationActive,_modulePanel,_barAutoReveal;
    private MonitorPage _applicationPage;
    private int _moduleScroll;
    private string? _preferenceWarning;
    public bool ApplicationActive=>_applicationActive;
    public bool ModulePanel=>_modulePanel;
    public bool BarAutoReveal=>_barAutoReveal;
    public string? PreferenceWarning=>_preferenceWarning;
    private bool EdgeBarArmed=>Style==IslandStyle.Bar&&_barAutoReveal;
    private MonitorDashboard ApplicationDashboard()=>CreateDashboard() with { Page=_applicationPage,Surface=MonitorSurface.Application,ControlCenter=null,ScrollOffset=0 };
    private void RefreshApplication()
    {
        if(!_applicationActive)return;
        try{_applicationDesktop?.UpdateApplication(ApplicationDashboard());}
        catch(Exception ex)when(ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {_preferenceWarning="The dashboard could not redraw. "+ex.Message;_applicationDesktop?.CloseApplication();}
    }
    private void OpenApplication(MonitorPage page,bool activate=true)
    {
        if(_applicationDesktop is null)return;
        _applicationPage=page;
        if(UserHidden)SetHidden(false);
        SampleDashboard();
        try{_applicationDesktop.ShowApplication(ApplicationDashboard(),activate);}
        catch(Exception ex)when(ex is not OutOfMemoryException and not StackOverflowException and not AccessViolationException)
        {_preferenceWarning="The dashboard could not open. "+ex.Message;_applicationDesktop.CloseApplication();}
    }
    private void OnApplicationInput(MonitorWindowEvent input)
    {
        if(_closed)return;
        if(input.Kind==MonitorWindowEventKind.VisibilityChanged)
        {
            var active=input.Value!=0;
            if(active==_applicationActive)return;
            _applicationActive=active;
            if(!_suppressed){ConfigureVisibleTimers();if(active){SampleDashboard();RefreshApplication();}}
            return;
        }
        if(_options.HoldDiagnosticInput){HeldDiagnosticInputs++;return;}
        if(input.Value is >=400 and <=408){_applicationPage=(MonitorPage)(input.Value-400);RefreshApplication();return;}
        ExecuteCommand(input.Value);RefreshApplication();
    }
    private void SavePreference(Func<SettingsDocument,SettingsDocument> change)
    {
        _preferences=change(_preferences);
        if(_settingsStore is null)return;
        try
        {
            var latest=_settingsStore.Load();
            if(_settingsStore.LastWarning?.Contains("newer",StringComparison.OrdinalIgnoreCase)==true)
                throw new InvalidOperationException("A newer settings file was preserved. This change applies only until exit.");
            _settingsStore.Save(change(latest));_preferenceWarning=_settingsStore.LastWarning;
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {_preferenceWarning="Could not save this preference. It applies until exit. "+ex.Message;}
    }
    private bool ExecuteWorkspaceCommand(int command)
    {
        if(command==DesktopDashboardLayout.OpenApplication){if(_applicationDesktop is null)return false;OpenApplication(_page);return true;}
        if(command==ModulePanelLayout.Dismiss){_modulePanel=false;SetState(IslandState.Collapsed);return true;}
        if(command is 483 or 484){ScrollModule(command==483?-140:140);return true;}
        if(command==DesktopDashboardLayout.ToggleEdgeBar)
        {
            _barAutoReveal=!_barAutoReveal;_edgeBar.Reset();
            SavePreference(p=>p with{BarAutoReveal=_barAutoReveal});
            ConfigureVisibleTimers();Draw();RefreshApplication();return true;
        }
        if(command==DashboardLayout.ThemeCommand)
        {
            _theme=_theme==ThemeMode.Light?ThemeMode.Dark:ThemeMode.Light;
            SavePreference(p=>p with{Theme=_theme});Draw();RefreshApplication();return true;
        }
        if(command is >=200 and <=202)
        {SetStyle((IslandStyle)(command-200));SavePreference(p=>p with{Style=Style});RefreshApplication();return true;}
        if(command is >=520 and <=524)
        {
            var mode = (Islet.Core.Metrics.FanControlMode)(command - 520);
            _sensors?.TrySetFanProfile(mode, out _);
            SampleDashboard(); Draw(); RefreshApplication(); return true;
        }
        return false;
    }
    private void ScrollModule(int delta)
    {
        if(!_modulePanel||State!=IslandState.Expanded)return;
        _moduleScroll=ModulePanelLayout.ClampScroll(_moduleScroll+delta,880,590);Draw();
    }
}