#requires -Version 5.1
# Offline conversion only. WPF is never initialized by the native Tidlet runtime.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$root=Split-Path -Parent $PSScriptRoot
$source=Join-Path $root 'assets/lucide'
$inventory=Get-Content (Join-Path $source 'manifest.json') -Raw|ConvertFrom-Json
$builder=[Text.StringBuilder]::new()
$null=$builder.AppendLine('// Generated from approved Lucide 1.62.0 assets. See assets/lucide/LICENSE and manifest.json.')
$null=$builder.AppendLine('namespace Islet.Windows.Native;')
$null=$builder.AppendLine('internal static class LucideGeometry { internal static readonly IReadOnlyDictionary<string, double[][]> Paths = new Dictionary<string, double[][]> {')
foreach($item in $inventory){
    $file=Join-Path $source ($item.Name+'.svg')
    if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $item.SHA256){throw 'Unverified source icon'}
    [xml]$svg=Get-Content -LiteralPath $file -Raw
    $null=$builder.AppendLine('    ["'+$item.Name+'"] = new double[][] {')
    foreach($node in $svg.svg.ChildNodes){
        $geometry=switch($node.LocalName){
            'path' { [Windows.Media.Geometry]::Parse($node.d) }
            'line' { [Windows.Media.LineGeometry]::new([Windows.Point]::new([double]$node.x1,[double]$node.y1),[Windows.Point]::new([double]$node.x2,[double]$node.y2)) }
            'circle' { [Windows.Media.EllipseGeometry]::new([Windows.Point]::new([double]$node.cx,[double]$node.cy),[double]$node.r,[double]$node.r) }
            'rect' { $radius=if($node.HasAttribute('rx')){[double]$node.rx}else{0}; [Windows.Media.RectangleGeometry]::new([Windows.Rect]::new([double]$node.x,[double]$node.y,[double]$node.width,[double]$node.height),$radius,$radius) }
            default { throw ('Unsupported SVG primitive: '+$node.LocalName) }
        }
        $flat=$geometry.GetFlattenedPathGeometry(0.025,[Windows.Media.ToleranceType]::Absolute)
        foreach($figure in $flat.Figures){
            $points=[Collections.Generic.List[Windows.Point]]::new();$points.Add($figure.StartPoint)
            foreach($segment in $figure.Segments){
                if($segment -is [Windows.Media.LineSegment]){$points.Add($segment.Point)}
                elseif($segment -is [Windows.Media.PolyLineSegment]){foreach($point in $segment.Points){$points.Add($point)}}
                else{throw 'Flattening retained a curve'}
            }
            if($figure.IsClosed){$points.Add($figure.StartPoint)}
            $values=foreach($point in $points){$point.X.ToString('0.###',[Globalization.CultureInfo]::InvariantCulture);$point.Y.ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)}
            $null=$builder.AppendLine('        new double[] { '+($values -join ', ')+' },')
        }
    }
    $null=$builder.AppendLine('    },')
}
$null=$builder.AppendLine('}; }')
[IO.File]::WriteAllText((Join-Path $root 'src/Islet.Windows/Native/LucideGeometry.g.cs'),$builder.ToString(),[Text.UTF8Encoding]::new($false))
Write-Output ('Generated '+$inventory.Count+' icons from hash-verified source assets.')