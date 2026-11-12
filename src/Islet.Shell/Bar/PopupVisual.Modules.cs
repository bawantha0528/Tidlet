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
            case BarModule.Sensors:DrawSensors(d,sample);break;
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
        double y=125;
        if(IsMeasuring||_transferHistory is not null||net.Download.HasValue&&net.Upload.HasValue)
        {
            Section(d,"USAGE HISTORY",ref y);TransferChart(d,y);y+=98;
        }
        Section(d,"CONNECTIVITY",ref y);
        Text(d,"Open Network in Dashboard to run connectivity checks.",16,y,10,Dim,maxWidth:288);
        y+=35;
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
        Hero(d,49,112);var b=battery.Reading;var color=_palette.Status(BarThresholds.BatteryChargeLevel(b.Percent),_palette.BatteryFill);
        d.DrawRoundedRectangle(_palette.Panel,_palette.BatteryOutline,new Rect(69,66,89,40),6,6);d.DrawRoundedRectangle(Dim,null,new Rect(161,78,4,16),1,1);
        if(b.Percent is{}pct&&double.IsFinite(pct)&&pct is>0 and<=100)d.DrawRoundedRectangle(color,null,new Rect(73,70,81*pct/100,32),3,3);
        Text(d,Percent(b.Percent),176,66,28,color,bold:true);
        var state=b.ChargingStatus switch{BatteryChargingStatus.Charging=>"Charging",BatteryChargingStatus.Full=>"Full",BatteryChargingStatus.Critical=>"Low battery",_=>battery.OnAc==true?"Connected · not charging":"Discharging"};
        Text(d,state,160,122,11,Dim,center:true);
        var temperature=battery.TemperatureCelsius is double temp?FormatSensorTemp(temp):"Not reported";
        Text(d,"Battery temperature · "+temperature,160,140,10.5,TextBrush,center:true);
        double y=172;Section(d,"DETAILS",ref y);
        Row(d,"Source",battery.OnAc==true?"AC power":"Battery",null,ref y,battery.OnAc.HasValue);
        if(battery.OnAc==false&&b.ChargingStatus!=BatteryChargingStatus.Charging&&b.EstimatedRemaining is{}remaining)Row(d,"Time remaining",$"{(int)remaining.TotalHours}h {remaining.Minutes}m",null,ref y);
        if(b.DesignedCapacityMWh is{}design&&b.FullChargedCapacityMWh is{}full&&design>0&&full>0)
        {
            y+=8;Section(d,"REPORTED CAPACITY",ref y);Row(d,"Full charge",$"{full:N0} mWh",Blue,ref y);Row(d,"Design",$"{design:N0} mWh",Gray,ref y);
            d.DrawRoundedRectangle(Gray,null,new Rect(15,y,290,7),3,3);d.DrawRoundedRectangle(_palette.Status(BarThresholds.BatteryHealthLevel(b.HealthPercent),Green),null,new Rect(15,y,290*Math.Clamp(full/(double)design,0,1),7),3,3);y+=22;
            Text(d,battery.CapacityScope,15,y,9,Dim);y+=24;
        }
        Row(d,"Health",Percent(b.HealthPercent),_palette.Status(BarThresholds.BatteryHealthLevel(b.HealthPercent),Green),ref y,b.HealthPercent is{}health&&double.IsFinite(health)&&health is>=0 and<=100);
        Row(d,"Cycles",b.CycleCount?.ToString()??"",null,ref y,b.CycleCount is>0);
        Row(d,"Power",$"{Math.Abs(b.ChargeRateMilliwatts??0)/1000:0.0} W",null,ref y,b.ChargeRateMilliwatts is{}power&&double.IsFinite(power));
        _contentBottom=y+16;
    }
    private void DrawDisk(DrawingContext d,PopupSample sample)
    {
        if(sample.Disk is not{}disk){Unavailable(d,sample.Explanation);return;}
        double y=60;
        var active=disk.Disks.Where(x=>x.Active.HasValue).Select(x=>x.Active!.Value).ToArray();
        if(active.Length>0){Hero(d,49,117);Ring(d,160,99,36,active.Max(),Percent(active.Max()),UsageBrush(BarModule.Disk,active.Max()));Text(d,"BUSIEST PHYSICAL DISK",160,147,9,Dim,center:true);y=176;}
        else{Hero(d,49,49);Text(d,IsMeasuring?"Measuring disk activity":"Disk activity unavailable",160,65,16,TextBrush,bold:true,center:true);y=110;}
        if(IsMeasuring||_transferHistory is not null||disk.Disks.Any(item=>item.Read.HasValue&&item.Write.HasValue))
        {
            Section(d,"READ / WRITE HISTORY",ref y);TransferChart(d,y);y+=98;
        }
        foreach(var item in disk.Disks){Section(d,"PHYSICAL DISK "+Short(item.Name,18),ref y);Row(d,"Read",Rate(item.Read),Blue,ref y,item.Read.HasValue);Row(d,"Write",Rate(item.Write),Red,ref y,item.Write.HasValue);}
        if(disk.Disks.Length==0){Text(d,disk.Explanation,15,y,10.5,Dim,maxWidth:288);y+=44;}
        foreach(var volume in disk.Volumes.Where(x=>x.Valid))
        {
            var used=_palette.Status(BarThresholds.StorageLevel(volume.UsedPercent),Blue);
            Section(d,"VOLUME "+volume.Name,ref y);Row(d,"Used",Bytes(volume.Total-volume.Free),used,ref y);Row(d,"Free",Bytes(volume.Free),Gray,ref y);
            d.DrawRoundedRectangle(Gray,null,new Rect(15,y,290,7),3,3);d.DrawRoundedRectangle(used,null,new Rect(15,y,290*volume.UsedPercent/100,7),3,3);y+=22;
        }
        _contentBottom=y+12;
    }
    private void DrawGpu(DrawingContext d,PopupSample sample)
    {
        if(sample.Gpu is not{}gpu||gpu.Adapters.Length==0){Unavailable(d,sample.Gpu?.Explanation??sample.Explanation);return;}
        double y=55;
        Section(d,"GPU TEMPERATURES",ref y);
        foreach(var adapter in gpu.Adapters)
        {
            var temperature=adapter.TemperatureCelsius is >0 and <=150
                ?FormatSensorTemp(adapter.TemperatureCelsius.Value):"Not reported";
            Row(d,adapter.Name,temperature,adapter.TemperatureCelsius is >0 and <=150?SensorTempBrush(adapter.TemperatureCelsius.Value):null,ref y);
        }
        y+=10;
        foreach(var adapter in gpu.Adapters)
        {
            var usage=adapter.Usage is >=0 and <=100 ? adapter.Usage : null;
            Hero(d,y-6,usage.HasValue?105:73);
            Text(d,adapter.Name,24,y+4,12,TextBrush,bold:true,maxWidth:272);
            if(usage is{}value)
            {
                Ring(d,65,y+63,29,value,Percent(value),UsageBrush(BarModule.Gpu,value));
                Text(d,"Busiest engine",113,y+32,12,TextBrush,bold:true);
                var history=_gpuHistory.GetValueOrDefault(adapter.Luid);
                var average=history is{Count:>=2}?history.Average(_now,120):default;
                if(average.Percent is{}mean)
                    MetricBar(d,average.Complete?"2-minute average":"Recent average",Percent(mean),mean,y+59,UsageBrush(BarModule.Gpu,mean));
                else
                    Text(d,"Collecting usage average…",113,y+62,10.5,Dim,maxWidth:181);
                y+=110;
            }
            else
            {
                Text(d,IsMeasuring?"Measuring engine usage":"Engine usage unavailable",160,y+51,11,Dim,center:true);
                y+=76;
            }
            if(IsMeasuring||adapter.Usage.HasValue)
            {
                Section(d,"BUSIEST ENGINE HISTORY",ref y);
                var saved=_history;try{_history=_gpuHistory.GetValueOrDefault(adapter.Luid);Chart(d,y);}finally{_history=saved;}y+=99;
            }
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
    private string Rate(double? bytes)=>MetricFormat.TransferRate(bytes,_module==BarModule.Network&&_useBits,IsMeasuring?"Measuring":"Not reported");
    private void DrawSensors(DrawingContext d,PopupSample sample)
    {
        if(sample.Sensors is not{Items.Length:>0}sensors)
        {
            Unavailable(d,sample.Sensors?.Explanation??sample.Explanation);
            return;
        }
        Hero(d,49,76);
        var heroTemp=sensors.CpuPackageCelsius??sensors.MaxTemperatureCelsius;
        var heroTitle=sensors.CpuPackageCelsius.HasValue?"CPU Package":heroTemp.HasValue?"Peak Temp":sensors.MaxFanRpm.HasValue?"Peak Fan":"Sensors";
        var heroValue=heroTemp is double t?FormatSensorTemp(t):sensors.MaxFanRpm is int rpm?$"{rpm} RPM":$"{sensors.Items.Length}";
        var heroBrush=heroTemp is double temp?SensorTempBrush(temp):Cyan;
        Text(d,heroTitle,24,59,10.5,Dim,maxWidth:130);
        Text(d,heroValue,24,78,24,heroBrush,bold:true,maxWidth:155);
        if(sensors.MaxFanRpm is int maxFan)
        {
            Text(d,"Peak fan",296,59,10.5,Dim,right:true,maxWidth:110);
            Text(d,$"{maxFan} RPM",296,82,13,Cyan,bold:true,right:true,maxWidth:120);
        }
        else
        {
            Text(d,"Detected",296,59,10.5,Dim,right:true,maxWidth:110);
            Text(d,$"{sensors.Items.Length} sensors",296,82,13,TextBrush,bold:true,right:true,maxWidth:120);
        }
        double y=137;
        void DrawGroup(string heading,SensorItem[] items)
        {
            if(items.Length==0)return;
            Section(d,heading,ref y);
            foreach(var item in items)
            {
                if(Intersects(y,36))
                {
                    d.DrawRoundedRectangle(SensorDot(item),null,new Rect(15,y+8,6,6),2,2);
                    Text(d,$"{Short(item.Hardware,18)} · {item.Name}",28,y+3,11,TextBrush,maxWidth:172);
                    Text(d,FormatSensorValue(item),305,y+8,11,TextBrush,bold:true,right:true,maxWidth:96,maxLines:1);
                }
                y+=36;
            }
        }
        DrawGroup("TEMPERATURES",sensors.Items.Where(s=>s.Kind==SensorKind.Temperature).ToArray());
        DrawGroup("FANS & CONTROL",sensors.Items.Where(s=>s.Kind is SensorKind.Fan or SensorKind.Control).ToArray());
        DrawGroup("POWER & CLOCKS",sensors.Items.Where(s=>s.Kind is SensorKind.Power or SensorKind.Clock).ToArray());
        DrawGroup("VOLTAGES",sensors.Items.Where(s=>s.Kind==SensorKind.Voltage).ToArray());
        var explanation=GetText(sensors.Explanation,15,y+4,10,Dim,false,false,288,5);
        if(Intersects(y+4,explanation.Height))
            d.DrawText(explanation,MonitorText.PixelAligned(15,y+4,_scale));
        _contentBottom=y+explanation.Height+14;
    }
    private string FormatSensorTemp(double celsius)=>Fahrenheit?$"{celsius*1.8+32:0.0} °F":$"{celsius:0.0} °C";
    private Brush SensorTempBrush(double celsius)=>_palette.Status(BarThresholds.TemperatureLevel(celsius),Green);
    private Brush SensorDot(SensorItem item)=>item.Kind switch
    {
        SensorKind.Temperature=>SensorTempBrush(item.Value),
        SensorKind.Fan=>Cyan,
        SensorKind.Control=>Cyan,
        SensorKind.Clock=>Blue,
        SensorKind.Power=>Orange,
        SensorKind.Voltage=>Gray,
        _=>Blue
    };
    private string FormatSensorValue(SensorItem item)=>item.Kind switch
    {
        SensorKind.Temperature=>FormatSensorTemp(item.Value),
        SensorKind.Fan=>$"{item.Value:0} {item.Unit}",
        SensorKind.Clock=>$"{item.Value:0} {item.Unit}",
        SensorKind.Power=>$"{item.Value:0.0} {item.Unit}",
        SensorKind.Voltage=>$"{item.Value:0.00} {item.Unit}",
        _=>$"{item.Value:0} {item.Unit}"
    };
    private static string Short(string text,int max=26)=>text.Length>max?text[..(max-1)]+"…":text;
}
