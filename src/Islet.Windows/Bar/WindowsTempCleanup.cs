using System.Runtime.InteropServices;
using System.Text;
using Islet.Core.Bar;
using Microsoft.Win32.SafeHandles;

namespace Islet.Windows.Bar;

public sealed class WindowsTempCleanup
{
    private const int ScanLimit = 5000;
    private static readonly TimeSpan MinimumAge = TimeSpan.FromDays(7);
    private const FileAttributes Excluded = FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.ReadOnly |
        FileAttributes.System | FileAttributes.Hidden | FileAttributes.Offline | FileAttributes.Device;
    private readonly string _root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");

    public TempCleanupScan Scan(CancellationToken cancellation, Action<TempCleanupProgress> progress)
    {
        long bytes = 0;
        var files = 0;
        var limited = false;
        var candidates = new List<TempCleanupFile>();
        var nextReport = 0L;
        try
        {
            if (!UsableRoot()) return new(0, 0, [], false, "The local user Temp folder is unavailable.");
            var cutoff = DateTime.UtcNow.Subtract(MinimumAge).ToFileTimeUtc();
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                MaxRecursionDepth = 16,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            foreach (var file in Directory.EnumerateFiles(_root, "*", options))
            {
                cancellation.ThrowIfCancellationRequested();
                if (files >= ScanLimit) { limited = true; break; }
                files++;
                try
                {
                    var info = new FileInfo(file);
                    if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    bytes = checked(bytes + info.Length);
                    if (!string.Equals(info.DirectoryName, _root, StringComparison.OrdinalIgnoreCase) ||
                        !EligibleName(info.Name) || (info.Attributes & Excluded) != 0 ||
                        info.LastWriteTimeUtc.ToFileTimeUtc() > cutoff) continue;
                    using var handle = Open(file, delete: false);
                    if (TryRead(handle, file, cutoff, out var candidate)) candidates.Add(candidate);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { limited = true; }
                finally { ReportProgress(progress, ref nextReport, new(files, bytes, EligibleOrRemoved: candidates.Count)); }
            }
            return new(bytes, files, candidates.ToArray(), limited);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { return new(bytes, files, [], true, "Windows could not finish reading the local user Temp folder."); }
    }

    public TempCleanupResult Clean(TempCleanupScan scan, CancellationToken cancellation, Action<TempCleanupProgress> progress)
    {
        var removed = 0;
        long bytes = 0;
        var processed = 0;
        var nextReport = 0L;
        var before = FreeSpace();
        var cutoff = DateTime.UtcNow.Subtract(MinimumAge).ToFileTimeUtc();
        if (scan.Error is null && UsableRoot())
        {
            foreach (var candidate in scan.Eligible)
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    if (!EligibleName(candidate.Name)) continue;
                    var file = Path.Combine(_root, candidate.Name);
                    using var handle = Open(file, delete: true);
                    if (!TryRead(handle, file, cutoff, out var current) || current != candidate) continue;
                    byte disposition = 1;
                    if (!SetFileInformationByHandle(handle, 4, ref disposition, sizeof(byte))) continue;
                    removed++;
                    bytes += current.Bytes;
                }
                finally
                {
                    processed++;
                    ReportProgress(progress, ref nextReport, new(processed, bytes, scan.Eligible.Count, removed));
                }
            }
        }
        var after = FreeSpace();
        return new(removed, scan.Eligible.Count - removed, bytes, before.HasValue && after.HasValue ? after - before : null);
    }

    private static void ReportProgress(Action<TempCleanupProgress> report, ref long nextReport, TempCleanupProgress progress)
    {
        var now = Environment.TickCount64;
        if (now < nextReport) return;
        nextReport = now + 150;
        report(progress);
    }

    private bool UsableRoot()
    {
        try
        {
            return Path.IsPathFullyQualified(_root) && !new Uri(_root).IsUnc &&
                (File.GetAttributes(_root) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == FileAttributes.Directory;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or UriFormatException) { return false; }
    }

    private long? FreeSpace()
    {
        try { return new DriveInfo(Path.GetPathRoot(_root)!).AvailableFreeSpace; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    private static bool EligibleName(string name) => name.Length > 0 && name.IndexOfAny([':', '/', '\\']) < 0 &&
        (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".temp", StringComparison.OrdinalIgnoreCase));

    private static SafeFileHandle Open(string path, bool delete) =>
        CreateFileW(path, delete ? 0x10080u : 0x80u, delete ? 0u : 7u, 0, 3, 0x00200000, 0);

    private static bool TryRead(SafeFileHandle handle, string expectedPath, long cutoff, out TempCleanupFile candidate)
    {
        candidate = null!;
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info) || info.Links != 1 ||
            (info.Attributes & Excluded) != 0) return false;
        var lastWrite = ((long)info.WriteHigh << 32) | info.WriteLow;
        if (lastWrite <= 0 || lastWrite > cutoff) return false;
        var name = new StringBuilder(512);
        var length = GetFinalPathNameByHandleW(handle, name, (uint)name.Capacity, 0);
        if (length == 0 || length >= name.Capacity ||
            !string.Equals(name.ToString(), @"\\?\" + expectedPath, StringComparison.OrdinalIgnoreCase)) return false;
        var size = ((long)info.SizeHigh << 32) | info.SizeLow;
        if (size < 0) return false;
        var identity = ((ulong)info.IndexHigh << 32) | info.IndexLow;
        if (identity == 0) return false;
        candidate = new(Path.GetFileName(expectedPath), size, lastWrite, info.Volume, identity);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInfoByHandle
    {
        public FileAttributes Attributes;
        public uint CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInfoByHandle info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint size, uint flags);
    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int infoClass, ref byte info, uint size);
}
