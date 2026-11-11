namespace Islet.Core.Bar;

public readonly record struct BarPoint(int X,int Y);

/// <summary>Per-monitor pointer latch. Clock and coordinates are supplied by the host.</summary>
public sealed class TopEdgeReveal
{
    private long? _awaySince;
    public bool Visible{get;private set;}

    public void Reset(){Visible=false;_awaySince=null;}

    public bool Update(BarRect display,BarRect strip,BarPoint? point,double scale,bool popupOpen,long now)
    {
        var edge=Math.Max(1,(int)Math.Ceiling(2*scale));
        var slack=Math.Max(1,(int)Math.Ceiling(6*scale));
        var atEdge=point is{}p&&p.X>=display.Left&&p.X<display.Right&&p.Y>=display.Top&&p.Y<display.Top+edge;
        var nearStrip=point is{}q&&q.X>=strip.Left&&q.X<strip.Right&&q.Y>=display.Top&&q.Y<strip.Bottom+slack;
        if(atEdge||Visible&&(nearStrip||popupOpen)){Visible=true;_awaySince=null;}
        else if(Visible)
        {
            _awaySince??=now;
            if(now-_awaySince.Value>=450)Reset();
        }
        return Visible;
    }
}
