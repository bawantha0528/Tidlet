namespace Islet.Core.Popups;
public static class PopupProcessMath
{
    public static double? Cpu(ulong oldCreation,ulong creation,ulong oldTicks,ulong ticks,double seconds,int processors)
    {
        if(oldCreation!=creation||ticks<oldTicks||!double.IsFinite(seconds)||seconds<=0||processors<1)return null;
        return Math.Clamp((ticks-oldTicks)/10000000.0/seconds/processors*100,0,100);
    }
}
