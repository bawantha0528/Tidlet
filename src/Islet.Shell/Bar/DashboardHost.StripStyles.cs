using System.Windows;
using System.Windows.Media;
using Islet.Core.Bar;

namespace Islet.Shell.Bar;

public sealed partial class DashboardHost
{
    private sealed partial class DashboardVisual
    {
        private double StripPreviewTop=>ShowsPowerMode?143:ShowsInspection?155:65;
        private readonly Dictionary<BarItemStyle,Drawing> _stripPreviews=[];
        private (BarModule Module,DashboardPalette Palette,double Scale)? _stripPreviewKey;

        private static string StyleName(BarItemStyle style)=>style switch
        {
            BarItemStyle.Auto=>"Automatic",
            BarItemStyle.TwoLine=>"Text",
            BarItemStyle.Ring=>"Circle",
            BarItemStyle.Bar=>"Bar",
            BarItemStyle.Sparkline=>"Sparkline",
            BarItemStyle.IconWithText=>"Icon + text",
            BarItemStyle.IconOnly=>"Icon only",
            BarItemStyle.Speed=>"Up / down",
            BarItemStyle.Battery=>"Battery",
            _=>style.ToString()
        };

        private void DrawStripPreviews(DrawingContext drawing,BarModule module)
        {
            var key=(module,_palette,_scale);
            if(_stripPreviewKey!=key){_stripPreviews.Clear();_stripPreviewKey=key;}
            var settings=_settings();
            var options=BarStyles.Options(module);
            var requested=settings.Preference(module).Style;
            var selected=options.Contains(requested)?requested:BarItemStyle.Auto;
            var y=StripPreviewTop;
            var width=(Width-255-(options.Count-1)*5)/options.Count;
            Label(drawing,"Monitor strip · style previews",230,y,11,_dim,true);
            for(var index=0;index<options.Count;index++)
            {
                var option=options[index];
                var tile=new Rect(230+index*(width+5),y+18,width,47);
                var active=selected==option;
                drawing.DrawRoundedRectangle(active?_palette.Metrics.Surface:_surface,active?new Pen(_blue,1.5):_palette.CardBorder,tile,5,5);
                if(!_stripPreviews.TryGetValue(option,out var preview))
                {
                    var light=_palette==DashboardPalette.For(BarTheme.Light);
                    _stripPreviews[option]=preview=MonitorBarHost.PreviewItem(module,option,light,_scale);
                }
                var factor=Math.Min(.86,(tile.Width-10)/Math.Max(1,preview.Bounds.Width));
                if(factor>0)
                {
                    var left=tile.Left+(tile.Width-preview.Bounds.Width*factor)/2-preview.Bounds.Left*factor;
                    drawing.PushTransform(new MatrixTransform(factor,0,0,factor,left,tile.Top+3));
                    drawing.DrawDrawing(preview);
                    drawing.Pop();
                }
                var name=StyleName(option);
                Label(drawing,name,tile.Left,tile.Top+29,9,active?_text:_dim,active,tile.Width,center:true);
                Hit(name+" strip style"+(active?" (selected)":""),tile,
                    ()=>{if(!active)Preference(module,p=>p with{Style=option},$"{module.ToString().ToUpperInvariant()} strip style set to {name}. Saved.");},
                    pinned:true);
            }
        }
    }
}
