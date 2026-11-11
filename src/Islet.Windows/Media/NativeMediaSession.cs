using System.Diagnostics;
using System.Runtime.InteropServices;
using Islet.Core.Activities;

namespace Islet.Windows.Media;

/// <summary>Raw SDK ABI. Construct, use and dispose only on the owning initialized MTA thread.</summary>
internal sealed unsafe class NativeMediaSession : INativeMediaSession
{
    private nint _manager, _session;
    private string? _token;
    private MediaReading _latest = MediaReading.Waiting;
    public NativeMediaSession() => Check(RoInitialize(1));
    public MediaReading Read(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (_manager == 0)
        {
            using var name = new HString("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager");
            var iid = new Guid("2050c4ee-11a0-57de-aed7-c97c70338245");
            nint factory = 0;
            try
            {
                Check(RoGetActivationFactory(name.Value, &iid, &factory));
                _manager = AwaitObject(factory, 6, cancellation);
            }
            finally { Release(factory); }
        }
        var session = Object(_manager, 6);
        Release(_session); _session = 0; _token = null;
        if (session == 0) return _latest = new(MediaState.NoSession, "Not playing", "Play something in a media app");
        try
        {
            using var properties = Own(AwaitObject(session, 7, cancellation));
            using var current = Own(Object(_manager, 6));
            if (!SameIdentity(session, current.Value)) return _latest = MediaReading.Waiting;
            using var playback = Own(Object(session, 9));
            using var controls = Own(Object(playback.Value, 6));
            using var timeline = Own(Object(session, 8));
            var playing = Int32(playback.Value, 7) == 4;
            var start = Int64(timeline.Value, 6); var end = Int64(timeline.Value, 7); var pos = Int64(timeline.Value, 10);
            var duration = end >= start && (double)end - start <= TimeSpan.TicksPerDay * 7.0 ? (double)end - start : 0;
            TimeSpan? total = duration > 0 ? TimeSpan.FromTicks((long)duration) : null;
            TimeSpan? position = total.HasValue ? TimeSpan.FromTicks((long)Math.Clamp((double)pos - start, 0, duration)) : null;
            _token = Guid.NewGuid().ToString("N");
            _latest = new(MediaState.Available, Text(properties.Value, 6, "Untitled media"), Text(properties.Value, 9, "Windows media player"), _token,
                playing, Boolean(controls.Value, 16) || Boolean(controls.Value, playing ? 7 : 6), Boolean(controls.Value, 13), Boolean(controls.Value, 12), position, total);
            _session = session; session = 0;
            return _latest;
        }
        finally { Release(session); }
    }
    public MediaCommandResult Execute(MediaCommand command, string token, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (_session == 0 || token != _token || !_latest.Allows(command)) return new(false, "Player changed or this control is unavailable.");
        using var current = Own(Object(_manager, 6));
        if (!SameIdentity(_session, current.Value)) return new(false, "Player changed. Try again.");
        using var playback = Own(Object(_session, 9));
        using var controls = Own(Object(playback.Value, 6));
        var slot = NativeMediaCommandPolicy.Slot(command, Int32(playback.Value, 7) == 4,
            Boolean(controls.Value, 6), Boolean(controls.Value, 7), Boolean(controls.Value, 16), Boolean(controls.Value, 13), Boolean(controls.Value, 12));
        if (slot == 0) return new(false, "This player does not support that control.");
        using var operation = Own(Object(_session, slot));
        Wait(operation.Value, cancellation);
        byte result = 0;
        Check(((delegate* unmanaged[Stdcall]<nint, byte*, int>)Vtable(operation.Value, 8))(operation.Value, &result));
        return new(result != 0, result != 0 ? "Control sent to player" : "This player did not accept the control");
    }
    public void Dispose() { Release(_session); Release(_manager); _session = _manager = 0; RoUninitialize(); }
    private static nint AwaitObject(nint owner, int slot, CancellationToken cancellation)
    {
        using var operation = Own(Object(owner, slot));
        Wait(operation.Value, cancellation);
        return Object(operation.Value, 8);
    }
    private static void Wait(nint operation, CancellationToken cancellation)
    {
        using var info = Own(Query(operation, new Guid("00000036-0000-0000-C000-000000000046")));
        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 5;
        while (true)
        {
            var status = Int32(info.Value, 7);
            if (status == 1) return;
            if (status == 2) throw new OperationCanceledException();
            if (status == 3) { Check(Int32(info.Value, 8)); throw new COMException("Media operation failed."); }
            if (cancellation.IsCancellationRequested || Stopwatch.GetTimestamp() >= deadline)
            {
                ((delegate* unmanaged[Stdcall]<nint, int>)Vtable(info.Value, 9))(info.Value);
                cancellation.ThrowIfCancellationRequested();
                throw new TimeoutException("The media player did not respond in time.");
            }
            cancellation.WaitHandle.WaitOne(20);
        }
    }
    private static bool SameIdentity(nint a, nint b)
    {
        if (a == 0 || b == 0) return false;
        using var x = Own(Query(a, new Guid("00000000-0000-0000-C000-000000000046")));
        using var y = Own(Query(b, new Guid("00000000-0000-0000-C000-000000000046")));
        return x.Value == y.Value;
    }
    private static nint Query(nint value, Guid iid)
    {
        nint result = 0;
        Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Vtable(value, 0))(value, &iid, &result));
        return result;
    }
    private static nint Object(nint owner, int slot)
    {
        nint value = 0;
        Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Vtable(owner, slot))(owner, &value));
        return value;
    }
    private static int Int32(nint owner, int slot)
    { int value = 0; Check(((delegate* unmanaged[Stdcall]<nint, int*, int>)Vtable(owner, slot))(owner, &value)); return value; }
    private static long Int64(nint owner, int slot)
    { long value = 0; Check(((delegate* unmanaged[Stdcall]<nint, long*, int>)Vtable(owner, slot))(owner, &value)); return value; }
    private static bool Boolean(nint owner, int slot)
    { byte value = 0; Check(((delegate* unmanaged[Stdcall]<nint, byte*, int>)Vtable(owner, slot))(owner, &value)); return value != 0; }
    private static string Text(nint owner, int slot, string fallback)
    {
        var value = Object(owner, slot);
        try
        {
            uint length = 0; var buffer = WindowsGetStringRawBuffer(value, &length);
            var text = new string(buffer, 0, (int)Math.Min(length, 160));
            text = new string(text.Where(c => !char.IsControl(c)).ToArray());
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }
        finally { WindowsDeleteString(value); }
    }
    private static nint Vtable(nint value, int slot) => value != 0 ? (*(nint**)value)[slot] : throw new COMException("Missing media interface.");
    private static void Check(int hr) { if (hr < 0) Marshal.ThrowExceptionForHR(hr); }
    private static void Release(nint value) { if (value != 0) ((delegate* unmanaged[Stdcall]<nint, uint>)Vtable(value, 2))(value); }
    private static Reference Own(nint value) => new(value);
    private readonly struct Reference(nint value) : IDisposable { public nint Value => value; public void Dispose() => Release(value); }
    private sealed class HString : IDisposable
    {
        public nint Value;
        public HString(string text) { nint value = 0; Check(WindowsCreateString(text, (uint)text.Length, &value)); Value = value; }
        public void Dispose() => WindowsDeleteString(Value);
    }
    [DllImport("combase.dll", ExactSpelling = true)] private static extern int RoInitialize(uint type);
    [DllImport("combase.dll", ExactSpelling = true)] private static extern void RoUninitialize();
    [DllImport("combase.dll", ExactSpelling = true)] private static extern int RoGetActivationFactory(nint name, Guid* iid, nint* result);
    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string source, uint length, nint* value);
    [DllImport("combase.dll", ExactSpelling = true)] private static extern int WindowsDeleteString(nint value);
    [DllImport("combase.dll", ExactSpelling = true)] private static extern char* WindowsGetStringRawBuffer(nint value, uint* length);
}

/// <summary>SDK session vtable command selection. Does not execute any operating-system action.</summary>
public static class NativeMediaCommandPolicy
{
    public static int Slot(MediaCommand command, bool playing, bool play, bool pause, bool toggle, bool previous, bool next) => command switch
    {
        MediaCommand.Previous when previous => 17,
        MediaCommand.Next when next => 16,
        MediaCommand.TogglePlayback when toggle => 20,
        MediaCommand.TogglePlayback when playing && pause => 11,
        MediaCommand.TogglePlayback when !playing && play => 10,
        _ => 0
    };
}