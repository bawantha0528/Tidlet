using Islet.Core.Island;
using Islet.Core.Presentation;
namespace Islet.Windows.Native;
public sealed partial class NativeIslandDesktop
{
    private NativeMonitorWindow? _application;
    private int _closedApplicationRenders;
    private string? _closedApplicationFailure;
    public event Action<MonitorWindowEvent>? ApplicationInput;
    public bool ApplicationVisible=>_application?.Visible==true;
    public int ApplicationRenders=>_closedApplicationRenders+(_application?.RenderCount??0);
    public string? ApplicationFailure=>_application?.Failure??_closedApplicationFailure;
    private void DisposeApplication(){if(_application is null)return;_closedApplicationRenders+=_application.RenderCount;_closedApplicationFailure=_application.Failure;_application.Dispose();_application=null;}
    public void ShowApplication(MonitorDashboard dashboard,bool activate)
    { _application??=new NativeMonitorWindow(input=>ApplicationInput?.Invoke(input));_application.Show(dashboard,ReadDisplay(),activate); }
    public void UpdateApplication(MonitorDashboard dashboard)=>_application?.Update(dashboard);
    public void CloseApplication()=>_application?.Hide();
    public void SaveApplicationBitmap(string path)=>(_application??throw new InvalidOperationException("Dashboard is not open.")).SaveBitmap(path);
}