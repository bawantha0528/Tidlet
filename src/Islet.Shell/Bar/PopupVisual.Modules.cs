using System.Windows;
using System.Windows.Media;
using Islet.Core.Bar;
using Islet.Core.Metrics;
using Islet.Core.Popups;
namespace Islet.Shell.Bar;
internal sealed partial class PopupVisual
{
    private TransferHistory? _transferHistory;
    private readonly Dictionary<ulong,SampleRing> _gpuHistory=[];
    private void TrackModule(PopupSample sample,double now)
    {
        if(sample.Network is{Download:{} down,Upload:{} up}){_transferHistory??=new();_transferHistory.Push(now,down,up);}
        if(sample.Disk is{}disk&&disk.Disks.Length>0&&disk.Disks.All(item=>item.Read.HasValue&&item.Write.HasValue))
        {
            _transferHistory??=new();
            _transferHistory.Push(now,disk.Disks.Sum(item=>item.Read!.Value),disk.Disks.Sum(item=>item.Write!.Value));
        }
        if(sample.Gpu is{}gpu)foreach(var adapter in gpu.Adapters)
        {if(!_gpuHistory.TryGetValue(adapter.Luid,out var ring)&&_gpuHistory.Count<32)_gpuHistory[adapter.Luid]=ring=new(121);if(adapter.Usage is{}value)ring?.Push(now,value);}
    }
    private void DrawModule(DrawingContext d,PopupSample sample)
    {
        switch(_module)
        {
            case BarModule.Network:DrawNetwork(d,sample);break;
            case BarModule.Battery:DrawBattery(d,sample);break;
            case BarModule.Disk:DrawDisk(d,sample);break;
            case BarModule.Gpu:DrawGpu(d,sample);break;
            default:Unavailable(d,sample.Explanation);break;
        }
    }
    private void Unavailable(DrawingContext d,string reason)
    {Hero(d,49,140);Text(d,"Not available",160,80,18,TextBrush,bold:true,center:true);Text(d,reason,25,119,11,Dim,maxWidth:270);_contentBottom=208;}
    private void DrawNetwork(DrawingContext d,PopupSample sample)
    {
        if(sample.Network is not{}net||net.Adapters.Length==0&&!(net.Download is>=0&&net.Upload is>=0)){Unavailable(d,sample.Network?.Explanation??sample.Explanation);return;}
        Hero(d,49,70);d.DrawLine(SectionPen,new Point(160,64),new Point(160,105));
        Text(d,Rate(net.Download),87,62,19,TextBrush,bold:true,center:true,maxWidth:132);Text(d,Rate(net.Upload),233,62,19,TextBrush,bold:true,center:true,maxWidth:132);
        Text(d,"↓ DOWNLOAD",87,94,9,_palette.Download.Line.Brush,center:true);Text(d,"↑ UPLOAD",233,94,9,_palette.Upload.Line.Brush,center:true);
        double y=125;Section(d,"USAGE HISTORY",ref y);TransferChart(d,y);y+=98;
        Section(d,"CONNECTIVITY",ref y);
        for(int i=0;i<40;i++)d.DrawRoundedRectangle(Gray,null,new Rect(16+i%20*14.5,y+i/20*10,11,7),2,2);
        y+=30;Text(d,"Internet checks are off · no requests are sent",16,y,9.5,Dim);y+=27;
        Section(d,"RECORDED TRANSFER",ref y);Row(d,"Download",Bytes(net.RecordedDownload),Blue,ref y);Row(d,"Upload",Bytes(net.RecordedUpload),Red,ref y);
        Text(d,"Activity while monitoring is paused is excluded.",16,y,9,Dim);y+=28;
        for(int i=0;i<net.Adapters.Length;i++)
        {
            var adapter=net.Adapters[i];Section(d,net.Adapters.Length==1?"INTERFACE":$"INTERFACE {i+1}",ref y);
            Row(d,"Name",Short(adapter.Name),null,ref y);Pill(d,"Physical link","UP",Green,ref y);
            Row(d,"Local IP",adapter.Address??"",null,ref y,adapter.Address is not null);Row(d,"MAC",adapter.Mac??"",null,ref y,adapter.Mac is not null);
            Row(d,"Gateway",adapter.Gateway??"",null,ref y,adapter.Gateway is not null);
        }
        _contentBottom=y+12;
    }
    private void DrawBattery(DrawingContext d,PopupSample sample)
    {
        if(sample.Battery is not{}battery||battery.Reading.State!=BatteryReadingState.Available){Unavailable(d,sample.Battery?.Reading.Explanation??sample.Explanation);return;}
        Hero(d,49,102);var b=battery.Reading;var color=b.Percent<=15?Red:_palette.BatteryFill;
        d.DrawRoundedRectangle(_palette.Panel,_palette.BatteryOutline,new Rect(69,66,89,40),6,6);d.DrawRoundedRectangle(Dim,null,new Rect(161,78,4,16),1,1);
        if(b.Percent is{}pct&&double.IsFinite(pct)&&pct is>0 and<=100)d.DrawRoundedRectangle(color,null,new Rect(73,70,81*pct/100,32),3,3);
        Text(d,Percent(b.Percent),176,66,28,TextBrush,bold:true);
        var state=b.ChargingStatus switch{BatteryChargingStatus.Charging=>"Charging",BatteryChargingStatus.Full=>"Full",BatteryChargingStatus.Critical=>"Low battery",_=>battery.OnAc==true?"Connected · not charging":"Discharging"};
        Text(d,state,160,122,11,Dim,center:true);
        double y=162;Section(d,"DETAILS",ref y);
        Row(d,"Source",battery.OnAc==true?"AC power":"Battery",null,ref y,battery.OnAc.HasValue);
        if(battery.OnAc==false&&b.ChargingStatus!=BatteryChargingStatus.Charging&&b.EstimatedRemaining is{}remaining)Row(d,"Time remaining",$"{(int)remaining.TotalHours}h {remaining.Minutes}m",null,ref y);
        if(b.DesignedCapacityMWh is{}design&&b.FullChargedCapacityMWh is{}full&&design>0&&full>0)
        {
            y+=8;Section(d,"REPORTED CAPACITY",ref y);Row(d,"Full charge",$"{full:N0} mWh",Blue,ref y);Row(d,"Design",$"{design:N0} mWh",Gray,ref y);
            d.DrawRoundedRectangle(Gray,null,new Rect(15,y,290,7),3,3);d.DrawRoundedRectangle(Blue,null,new Rect(15,y,290*Math.Clamp(full/(double)design,0,1),7),3,3);y+=22;
            Text(d,battery.CapacityScope,15,y,9,Dim);y+=24;
        }
        Row(d,"Health",Percent(b.HealthPercent),Blue,ref y,b.HealthPercent is{}health&&double.IsFinite(health)&&health is>=0 and<=100);
        Row(d,"Cycles",b.CycleCount?.ToString()??"",null,ref y,b.CycleCount is>0);
        Row(d,"Power",$"{Math.Abs(b.ChargeRateMilliwatts??0)/1000:0.0} W",null,ref y,b.ChargeRateMilliwatts is{}power&&double.IsFinite(power));
        _contentBottom=y+16;
    }
    private void DrawDisk(DrawingContext d,PopupSample sample)
    {
        if(sample.Disk is not{}disk){Unavailable(d,sample.Explanation);return;}
        double y=60;
        var active=disk.Disks.Where(x=>x.Active.HasValue).Select(x=>x.Active!.Value).ToArray();
        if(active.Length>0){Hero(d,49,117);Ring(d,160,99,36,active.Max(),Percent(active.Max()));Text(d,"BUSIEST PHYSICAL DISK",160,147,9,Dim,center:true);y=176;}
        else{Hero(d,49,49);Text(d,"Disk activity",160,65,18,TextBrush,bold:true,center:true);y=110;}
        Section(d,"READ / WRITE HISTORY",ref y);TransferChart(d,y);y+=98;
        foreach(var item in disk.Disks){Section(d,"PHYSICAL DISK "+Short(item.Name,18),ref y);Row(d,"Read",Rate(item.Read),Blue,ref y,item.Read.HasValue);Row(d,"Write",Rate(item.Write),Red,ref y,item.Write.HasValue);}
        if(disk.Disks.Length==0){Text(d,disk.Explanation,15,y,10.5,Dim,maxWidth:288);y+=44;}
        foreach(var volume in disk.Volumes.Where(x=>x.Valid))
        {
            Section(d,"VOLUME "+volume.Name,ref y);Row(d,"Used",Bytes(volume.Total-volume.Free),Blue,ref y);Row(d,"Free",Bytes(volume.Free),Gray,ref y);
            d.DrawRoundedRectangle(Gray,null,new Rect(15,y,290,7),3,3);d.DrawRoundedRectangle(Blue,null,new Rect(15,y,290*volume.UsedPercent/100,7),3,3);y+=22;
        }
        _contentBottom=y+12;
    }
    private void DrawGpu(DrawingContext d,PopupSample sample)
    {
        if(sample.Gpu is not{}gpu||gpu.Adapters.Length==0){Unavailable(d,sample.Gpu?.Explanation??sample.Explanation);return;}
        double y=55;
        foreach(var adapter in gpu.Adapters)
        {
            Hero(d,y-6,adapter.Usage.HasValue?132:88);
            Text(d,adapter.Name,24,y+4,12,TextBrush,bold:true,maxWidth:272);y+=43;
            if(adapter.Usage is{}usage){Ring(d,160,y+35,32,usage,Percent(usage));y+=86;}
            else{Text(d,"Waiting for valid engine readings",160,y+8,11,Dim,center:true);y+=48;}
            Section(d,"BUSIEST ENGINE HISTORY",ref y);
            var saved=_history;try{_history=_gpuHistory.GetValueOrDefault(adapter.Luid);Chart(d,y);}finally{_history=saved;}y+=99;
            Row(d,"Dedicated capacity",Bytes(adapter.DedicatedMemory),Blue,ref y,adapter.DedicatedMemory>0);
            Text(d,adapter.Explanation,15,y+4,9,Dim,maxWidth:288);y+=48;
        }
        _contentBottom=y+12;
    }
    private void Pill(DrawingContext d,string name,string value,Brush color,ref double y)
    {Text(d,name,15,y,11.5,TextBrush);d.DrawRoundedRectangle(color,null,new Rect(257,y-1,48,18),5,5);Text(d,value,281,y,10,Brushes.White,bold:true,center:true);y+=25;}
    private void TransferChart(DrawingContext d,double y)
    {
        if(!Intersects(y,82))return;
        d.DrawRoundedRectangle(ChartBackground,_palette.InsetBorder,new Rect(14,y,292,82),8,8);
        var history=_transferHistory;
        if(history is null||history.Count<2){Text(d,"Collecting transfer history",160,y+32,10,Dim,center:true);return;}
        var scale=history.Scale(_now);Text(d,Rate(scale),20,y+3,8,Dim);d.DrawLine(SectionPen,new Point(15,y+41),new Point(305,y+41));
        for(int direction=0;direction<2;direction++)
        {
            var geometry=new StreamGeometry();using(var context=geometry.Open())
            {
                bool started=false;double last=16;
                for(int i=0;i<history.Count;i++){var point=history.At(i);var age=_now-point.Seconds;if(age>120)continue;last=16+288*(1-age/120);if(!started)context.BeginFigure(new Point(last,y+41),true,true);context.LineTo(new Point(last,y+41+(direction==0?1:-1)*37*(direction==0?point.Down:point.Up)/scale),started,false);started=true;}
                if(started)context.LineTo(new Point(last,y+41),false,false);
            }
            geometry.Freeze();d.DrawGeometry(direction==0?_palette.Download.Fill:_palette.Upload.Fill,direction==0?_palette.Download.Line:_palette.Upload.Line,geometry);
        }
    }
    private string Rate(double? bytes)=>MetricFormat.TransferRate(bytes,_module==BarModule.Network&&_useBits,"Measuring");
    private static string Short(string text,int max=26)=>text.Length>max?text[..(max-1)]+"…":text;
}
