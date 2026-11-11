namespace Islet.Core.Bar;

public sealed record TempCleanupFile(string Name, long Bytes, long LastWrite, uint Volume, ulong FileId);
public sealed record TempCleanupScan(long TotalBytes, int TotalFiles, IReadOnlyList<TempCleanupFile> Eligible, bool Limited, string? Error = null)
{
    public long EligibleBytes => Eligible.Sum(file => file.Bytes);
}
public sealed record TempCleanupResult(int Removed, int Skipped, long RemovedBytes, long? FreeSpaceChange);
