using System.Windows;
using System.Windows.Media;
using Islet.Core.Bar;
using Islet.Core.Metrics;
using Islet.Core.Popups;
namespace Islet.Shell.Bar;

internal sealed partial class ApplicationModuleVisual
{
    internal Func<string>? BatteryCapacity { get; set; }
    private TransferHistory? _networkHistory;
    private static readonly Brush UploadFill=Frozen(Color.FromArgb(80,255,69,58));
    private readonly Dictionary<ulong,SampleRing> _gpuHistories=[];
    private readonly Dictionary<string,TransferHistory> _diskHistories=[];
    private void TrackModule(PopupSample sample,double now)
    {
        if(sample.Network is{Download:{}down,Upload:{}up}&&ValidRate(down)&&ValidRate(up)){_networkHistory??=new();_networkHistory.Push(now,down,up);}
        foreach(var adapter in sample.Gpu?.Adapters??[])
        {
            if(!_gpuHistories.TryGetValue(adapter.Luid,out var ring)&&_gpuHistories.Count<32)_gpuHistories[adapter.Luid]=ring=new(121);
            if(adapter.Usage is{}value)ring?.Push(now,value);
        }
        foreach(var disk in sample.Disk?.Disks??[])
        {
            if(!_diskHistories.TryGetValue(disk.Name,out var ring)&&_diskHistories.Count<32)_diskHistories[disk.Name]=ring=new();
            if(disk.Read is{}read&&disk.Write is{}write&&ValidRate(read)&&ValidRate(write))ring?.Push(now,read,write);
        }
    }
    private void Module(DrawingContext d)
    {
        switch(_module){case BarModule.Gpu:Gpu(d);break;case BarModule.Disk:Disk(d);break;case BarModule.Network:Network(d);break;case BarModule.Battery:Battery(d);break;case BarModule.Sensors:Sensors(d);break;}
    }
    private void Gpu(DrawingContext d)
    {
        var gpu=_sample?.Gpu;
        if(gpu is null||gpu.Adapters.Length==0){Empty(d,gpu?.Explanation??_sample?.Explanation??"Reading graphics capabilities…");return;}
        var adapters=gpu.Adapters.AsSpan(0,Math.Min(32,gpu.Adapters.Length));
        var height=44+adapters.Length*28;
        CardAt(d,0,height);
        Label(d,"GPU temperatures",16,12,14,Text,true);
        var rowY=42.0;
        foreach(var adapter in adapters)
        {
            if(Intersects(rowY,24))
            {
                var value=adapter.TemperatureCelsius is >0 and <=150
                    ?SensorTemp(adapter.TemperatureCelsius.Value):"Not reported";
                Label(d,adapter.Name,16,rowY,12,Dim,maxWidth:_width-165);
                Label(d,value,_width-145,rowY,12,adapter.TemperatureCelsius is >0 and <=150?SensorTempBrush(adapter.TemperatureCelsius.Value):Text,true,right:true,maxWidth:130);
            }
            rowY+=28;
        }
        double y=height+18;
        foreach(var adapter in adapters)
        {
            CardAt(d,y,142);Ring(d,new(69,y+71),48,adapter.Usage,UsageBrush(BarModule.Gpu,adapter.Usage));
            if(ValidPercent(adapter.Usage))Label(d,$"{adapter.Usage:0}%",69,y+71,24,Text,true,centerOnPoint:true);
            else Label(d,IsMeasuring?"Measuring":"Unavailable",69,y+71,11,Dim,centerOnPoint:true);
            Label(d,adapter.Name,144,y+18,16,Text,true);
            Label(d,"Busiest engine · Windows GPU counters",144,y+63,11,Dim);
            if(adapter.DedicatedMemory>0)Label(d,"Dedicated capacity: "+Bytes(adapter.DedicatedMemory),144,y+94,12,Dim);
            _gpuHistories.TryGetValue(adapter.Luid,out var history);
            y+=176;if(ValidPercent(adapter.Usage)){History(d,y,"GPU utilization history",history);y+=230;}
            if(!string.IsNullOrWhiteSpace(adapter.Explanation)){Label(d,adapter.Explanation,16,y,11,Dim);y+=48;}
        }
        _bottom=y+15;
    }
    private void Disk(DrawingContext d)
    {
        double y=0;var data=_sample?.Disk;
        foreach(var disk in data?.Disks.Take(32)??[])
        {
            CardAt(d,y,142);Ring(d,new(69,y+71),48,disk.Active,UsageBrush(BarModule.Disk,disk.Active));
            if(ValidPercent(disk.Active))Label(d,$"{disk.Active:0}%",69,y+71,24,Text,true,centerOnPoint:true);
            else Label(d,"Unavailable",69,y+71,11,Dim,centerOnPoint:true);
            Label(d,disk.Name,144,y+20,16,Text,true);Label(d,"Physical disk activity",144,y+51,11,Dim);
            Label(d,"Read: "+Rate(disk.Read),144,y+83,12,Blue,true);Label(d,"Write: "+Rate(disk.Write),_width*.6,y+83,12,Red,true);
            y+=176;_diskHistories.TryGetValue(disk.Name,out var history);TransferChart(d,y,"Read / write history",history,false);y+=230;
        }
        var volumes=data?.Volumes.Where(v=>v.Valid).Take(32).ToArray()??[];
        if(volumes.Length>0)
        {
            Label(d,"Volume capacity",6,y,16,Text,true);y+=36;
            foreach(var volume in volumes)
            {
                CardAt(d,y,130);Label(d,volume.Name,18,y+15,15,Text,true);
                Label(d,Bytes(volume.Total)+" total",_width/2,y+18,12,Dim,right:true);
                CapacityBar(d,y+56,volume.UsedPercent,_palette.Metrics.Status(BarThresholds.StorageLevel(volume.UsedPercent),Blue));
                Label(d,"Used: "+Bytes(volume.Total-volume.Free),18,y+85,12,Blue);
                var low=volume.Free<=volume.Total/10;
                Label(d,(low?"Low space · ":"Free: ")+Bytes(volume.Free),_width/2,y+85,12,low?Orange:Dim,right:true);y+=146;
            }
        }
        if(y==0){Empty(d,data?.Explanation??_sample?.Explanation??"Reading storage capabilities…");return;}
        Label(d,data?.Explanation??"Physical disk counters and volume capacity are separate Windows readings.",16,y,11,Dim);_bottom=y+65;
    }
    private void Network(DrawingContext d)
    {
        var data=_sample?.Network;if(data is null){Empty(d,_sample?.Explanation??"Reading network capabilities…");return;}
        if(data.Adapters.Length==0&&!ValidRate(data.Download)&&!ValidRate(data.Upload)){Empty(d,data.Explanation);return;}
        CardAt(d,0,138);Label(d,"Download",20,20,12,Dim);Label(d,"Upload",_width/2+12,20,12,Dim);
        Label(d,Rate(data.Download),20,53,29,Blue,true);Label(d,Rate(data.Upload),_width/2+12,53,29,Red,true);
        double y=176;
        if(IsMeasuring||_networkHistory is not null||ValidRate(data.Download)&&ValidRate(data.Upload))
        {
            TransferChart(d,y,"Transfer history",_networkHistory,true);
            y=410;
        }
        Label(d,"Recorded this session",6,y,16,Text,true);y+=35;CardAt(d,y,94);
        Row(d,"Downloaded",Bytes(data.RecordedDownload),y+15,Blue);Row(d,"Uploaded",Bytes(data.RecordedUpload),y+51,Red);y+=118;
        if(ValidRate(data.Latency)||ValidRate(data.Jitter))
        {CardAt(d,y,94);if(ValidRate(data.Latency))Row(d,"Latency",$"{data.Latency:0.0} ms",y+15,Blue);if(ValidRate(data.Jitter))Row(d,"Jitter",$"{data.Jitter:0.0} ms",y+51,Orange);y+=118;}
        foreach(var adapter in data.Adapters.Take(32))
        {
            Label(d,adapter.Name,6,y,16,Text,true);y+=34;var top=y;var count=0;
            if(!string.IsNullOrWhiteSpace(adapter.Address))count++;
            if(!string.IsNullOrWhiteSpace(adapter.Mac))count++;
            if(!string.IsNullOrWhiteSpace(adapter.Gateway))count++;
            CardAt(d,y,Math.Max(50,count*37+20));y+=15;
            if(!string.IsNullOrWhiteSpace(adapter.Address)){Row(d,"Local IP",adapter.Address,y,Blue);y+=37;}
            if(!string.IsNullOrWhiteSpace(adapter.Mac)){Row(d,"Physical address",adapter.Mac,y,Gray);y+=37;}
            if(!string.IsNullOrWhiteSpace(adapter.Gateway)){Row(d,"Gateway",adapter.Gateway,y,Gray);y+=37;}
            y=Math.Max(y,top+50)+25;
        }
        Label(d,data.Explanation,16,y,11,Dim);y+=48;Label(d,"Internet reachability is not inferred from adapter traffic. Optional network checks stay off until enabled.",16,y,11,Dim);_bottom=y+65;
    }
    private void Battery(DrawingContext d)
    {
        var data=_sample?.Battery;var reading=data?.Reading;
        if(reading is not{State:BatteryReadingState.Available}||!ValidPercent(reading.Percent)){Empty(d,reading?.Explanation??_sample?.Explanation??"Reading battery capabilities…");return;}
        CardAt(d,0,198);var center=(_width-10)/2;
        if(Intersects(24,65))
        {
            d.DrawRoundedRectangle(null,Pen(Gray,2),new Rect(center-70,24,140,58),10,10);d.DrawRoundedRectangle(Gray,null,new Rect(center+74,42,6,23),2,2);
            if(reading.Percent is>0)d.DrawRoundedRectangle(_palette.Metrics.Status(BarThresholds.BatteryChargeLevel(reading.Percent),Green),null,new Rect(center-64,30,128*reading.Percent.Value/100,46),6,6);
        }
        Label(d,$"{reading.Percent:0}%",center,123,32,_palette.Metrics.Status(BarThresholds.BatteryChargeLevel(reading.Percent),Text),true,centerOnPoint:true);
        var state=reading.ChargingStatus switch{BatteryChargingStatus.Charging=>"Charging",BatteryChargingStatus.Full=>"Full",BatteryChargingStatus.Critical=>"Battery low",BatteryChargingStatus.Discharging=>data!.OnAc==true?"AC connected · not charging":"Discharging",_=>"State not reported"};
        Label(d,state,center,151,14,Dim,centerOnPoint:true);
        var temperature=data?.TemperatureCelsius is double temp?SensorTemp(temp):"Not reported";
        Label(d,"Battery temperature · "+temperature,center,177,11,Dim,centerOnPoint:true);
        double y=254;
        History(d,y,"Charge level · last 2 minutes");y+=228;
        if(BatteryCapacity is { } capacity)
        {
            Label(d,"Full-charge capacity trend",6,y,16,Text,true);y+=34;
            CardAt(d,y,65);Label(d,capacity(),18,y+16,12,Dim);y+=88;
        }
        Label(d,"Battery details",6,y,16,Text,true);y+=34;var rows=new List<(string,string)>();
        if(data!.OnAc is{}ac)rows.Add(("Power source",ac?"AC power":"Battery"));
        if(reading.HealthPercent is{}health&&ValidPercent(health))rows.Add(("Health",$"{health:0}%"));
        if(reading.ChargeRateMilliwatts is{}power&&double.IsFinite(power))rows.Add(("Reported power",$"{power/1000:0.00} W"));
        if(reading.CycleCount is>=0)rows.Add(("Cycles",reading.CycleCount.Value.ToString()));
        if(reading.EstimatedRemaining is{}time&&time>TimeSpan.Zero&&reading.ChargingStatus is BatteryChargingStatus.Discharging or BatteryChargingStatus.Critical)rows.Add(("Estimated remaining",$"{(int)time.TotalHours} h {time.Minutes} min"));
        if(rows.Count>0){CardAt(d,y,rows.Count*38+18);foreach(var row in rows){Row(d,row.Item1,row.Item2,y+13,Blue);y+=38;}y+=40;}
        if(reading.DesignedCapacityMWh is>0 and<uint.MaxValue&&reading.FullChargedCapacityMWh is>0 and<uint.MaxValue)
        {
            Label(d,"Reported capacity",6,y,16,Text,true);y+=34;CardAt(d,y,146);
            Row(d,"Full charge",$"{reading.FullChargedCapacityMWh/1000.0:0.00} Wh",y+15,Blue);
            Row(d,"Design",$"{reading.DesignedCapacityMWh/1000.0:0.00} Wh",y+49,Gray);
            CapacityBar(d,y+94,Math.Min(100,100.0*reading.FullChargedCapacityMWh.Value/reading.DesignedCapacityMWh.Value),_palette.Metrics.Status(BarThresholds.BatteryHealthLevel(reading.HealthPercent),Green));y+=168;
        }
        Label(d,data.CapacityScope,16,y,11,Dim);y+=40;Label(d,reading.Explanation,16,y,11,Dim);_bottom=y+65;
    }
    private void Empty(DrawingContext d,string reason){CardAt(d,0,145);Label(d,"Reading unavailable",20,20,18,Text,true);Label(d,reason,20,62,12,Dim);_bottom=160;}
    private void Row(DrawingContext d,string label,string value,double y,Brush color)
    {if(!Intersects(y,36))return;d.DrawRoundedRectangle(color,null,new Rect(16,y+4,8,8),2,2);Label(d,label,32,y,12,Dim);Label(d,value,_width/2,y,12,Text,true,true);}
    private void CapacityBar(DrawingContext d,double y,double percent,Brush fill)
    {if(!Intersects(y,12))return;d.DrawRoundedRectangle(Track,null,new Rect(18,y,_width-46,12),4,4);if(double.IsFinite(percent)&&percent is>0 and<=100)d.DrawRoundedRectangle(fill,null,new Rect(18,y,(_width-46)*percent/100,12),4,4);}
    private static bool ValidPercent(double? value)=>value is{}v&&double.IsFinite(v)&&v is>=0 and<=100;
    private static bool ValidRate(double? value)=>value is{}v&&double.IsFinite(v)&&v>=0&&v<ulong.MaxValue;
    private string Rate(double? value)=>MetricFormat.TransferRate(value,_module==BarModule.Network&&_useBits,IsMeasuring?"Measuring":"Not reported");
    private void TransferChart(DrawingContext d,double y,string title,TransferHistory? history,bool mirrored)
    {
        Label(d,title,6,y-27,16,Text,true);CardAt(d,y,200);if(!Intersects(y,200))return;
        var rect=new Rect(20,y+35,_width-50,130);var middle=mirrored?rect.Top+rect.Height/2:rect.Bottom;var amplitude=mirrored?rect.Height/2:rect.Height;
        if(history is not{Count:>=2})
        {
            Label(d,MetricLoading.CollectingHistory,0,y+82,12,Dim,centered:true);
            return;
        }
        d.DrawLine(Grid,new Point(rect.Left,middle),new Point(rect.Right,middle));var scale=history.Scale(_now);
        Label(d,Rate(scale),rect.Left,y+10,10,Dim);
        for(int channel=0;channel<2;channel++)
        {
            var geometry=new StreamGeometry();var started=false;double lastX=0;
            using(var context=geometry.Open())
            {
                for(int i=0;i<history.Count;i++)
                {
                    var point=history.At(i);if(point.Seconds<_now-120)continue;var x=rect.Left+rect.Width*Math.Clamp((point.Seconds-(_now-120))/120,0,1);
                    if(!started){context.BeginFigure(new(x,middle),true,true);started=true;}
                    var value=channel==0?point.Down:point.Up;var sign=mirrored&&channel==0?1:-1;
                    context.LineTo(new(x,middle+sign*amplitude*Math.Clamp(value/scale,0,1)),true,false);lastX=x;
                }
                if(started)context.LineTo(new(lastX,middle),true,false);
            }
            geometry.Freeze();if(started)d.DrawGeometry(channel==0?Fill:UploadFill,channel==0?Line:Pen(Red,1),geometry);
        }
        Label(d,"2 minutes ago",rect.Left,rect.Bottom+14,10,Dim);Label(d,"Now",rect.Right-26,rect.Bottom+14,10,Dim);
    }
    private void Sensors(DrawingContext d)
    {
        if(_sample?.Sensors is not{Items.Length:>0}sensors)
        {
            Empty(d,_sample?.Sensors?.Explanation??_sample?.Explanation??"No supported hardware sensors reported.");
            return;
        }
        CardAt(d,0,148);
        var primaryTemp=sensors.CpuPackageCelsius??sensors.MaxTemperatureCelsius;
        var primaryTitle=sensors.CpuPackageCelsius.HasValue?"CPU Package Temperature":primaryTemp.HasValue?"Peak Temperature":sensors.MaxFanRpm.HasValue?"Peak Fan Speed":"Active Sensors";
        var primaryValue=primaryTemp is double t?SensorTemp(t):sensors.MaxFanRpm is int rpm?$"{rpm} RPM":$"{sensors.Items.Length}";
        var primaryBrush=primaryTemp is double temp?SensorTempBrush(temp):Cyan;
        var labelMax=Math.Max(100,_width*0.44-28);
        Label(d,primaryTitle,20,18,12,Dim,maxWidth:labelMax);
        Label(d,primaryValue,20,42,30,primaryBrush,true,maxWidth:labelMax);
        var rx=_width*0.48;
        void SummaryRow(string caption,string value,double ry)
        {
            if(!Intersects(ry,24))return;
            Label(d,caption,rx,ry,12,Dim,maxWidth:Math.Max(70,_width-rx-130));
            Label(d,value,_width/2,ry,12,Text,true,right:true,maxWidth:Math.Max(60,_width/2-12));
        }
        SummaryRow("Peak Temp",sensors.MaxTemperatureCelsius is double maxT?SensorTemp(maxT):"Unavailable",20);
        SummaryRow("Peak Fan",sensors.MaxFanRpm is int maxF?$"{maxF} RPM":"Unavailable",46);
        SummaryRow("Windows session",sensors.IsElevated?"Elevated":"Standard user",72);
        Label(d,sensors.Explanation,20,104,11,Dim,maxWidth:Math.Max(120,_width-52));
        double y=188;
        y=SensorSection(d,y,"Temperatures",sensors.Items.Where(s=>s.Kind==SensorKind.Temperature).ToArray());
        y=SensorSection(d,y,"Cooling & Fans",sensors.Items.Where(s=>s.Kind is SensorKind.Fan or SensorKind.Control).ToArray());
        y=SensorSection(d,y,"Power, Clocks & Voltages",sensors.Items.Where(s=>s.Kind is SensorKind.Power or SensorKind.Clock or SensorKind.Voltage).ToArray());
        _bottom=Math.Max(165,y-14);
    }
    private double SensorSection(DrawingContext d,double y,string title,SensorItem[] items)
    {
        if(items.Length==0)return y;
        var height=16+items.Length*32;
        Label(d,title,6,y-27,16,Text,true);
        CardAt(d,y,height);
        var ry=y+14;
        var labelMax=Math.Max(100,_width-170);
        foreach(var item in items)
        {
            if(Intersects(ry,32))
            {
                d.DrawRoundedRectangle(SensorItemBrush(item),null,new Rect(16,ry+4,8,8),2,2);
                Label(d,$"{item.Hardware} · {item.Name}",32,ry,12,Dim,maxWidth:labelMax);
                Label(d,SensorItemValue(item),_width-132,ry,12,Text,true,right:true,maxWidth:120);
            }
            ry+=32;
        }
        return y+height+40;
    }
    private string SensorTemp(double celsius)=>_fahrenheit?$"{celsius*1.8+32:0.0} °F":$"{celsius:0.0} °C";
    private Brush SensorTempBrush(double celsius)=>_palette.Metrics.Status(BarThresholds.TemperatureLevel(celsius),Green);
    private Brush SensorItemBrush(SensorItem item)=>item.Kind switch
    {
        SensorKind.Temperature=>SensorTempBrush(item.Value),
        SensorKind.Fan=>Cyan,
        SensorKind.Control=>Cyan,
        SensorKind.Clock=>Blue,
        SensorKind.Power=>Orange,
        SensorKind.Voltage=>Gray,
        _=>Blue
    };
    private string SensorItemValue(SensorItem item)=>item.Kind switch
    {
        SensorKind.Temperature=>SensorTemp(item.Value),
        SensorKind.Fan=>$"{item.Value:0} {item.Unit}",
        SensorKind.Clock=>$"{item.Value:0} {item.Unit}",
        SensorKind.Power=>$"{item.Value:0.0} {item.Unit}",
        SensorKind.Voltage=>$"{item.Value:0.00} {item.Unit}",
        _=>$"{item.Value:0} {item.Unit}"
    };
}
