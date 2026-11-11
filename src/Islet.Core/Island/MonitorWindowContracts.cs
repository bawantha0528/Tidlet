using Islet.Core.Presentation;
namespace Islet.Core.Island;
public enum MonitorWindowEventKind { Command, VisibilityChanged }
public readonly record struct MonitorWindowEvent(MonitorWindowEventKind Kind,int Value=0);
public interface IMonitorWindowDesktop
{
    event Action<MonitorWindowEvent>? ApplicationInput;
    bool ApplicationVisible {get;}
    void ShowApplication(MonitorDashboard dashboard,bool activate);
    void UpdateApplication(MonitorDashboard dashboard);
    void CloseApplication();
}