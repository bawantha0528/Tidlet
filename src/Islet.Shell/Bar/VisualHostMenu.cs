using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace Islet.Shell.Bar;
internal static class VisualHostMenu
{
    public static Action Open(UIElement target,bool keyboard,Action exit)
    {
        var menu=new ContextMenu{PlacementTarget=target,Placement=keyboard?PlacementMode.Center:PlacementMode.MousePoint};
        var item=new MenuItem{Header="Exit Tidlet"};item.Click+=(_,_)=>exit();menu.Items.Add(item);menu.IsOpen=true;
        return ()=>menu.IsOpen=false;
    }
}
