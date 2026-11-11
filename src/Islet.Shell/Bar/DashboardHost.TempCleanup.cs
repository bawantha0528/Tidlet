using System.Windows;
using System.Windows.Media;
using Islet.Core.Bar;

namespace Islet.Shell.Bar;

public sealed partial class DashboardHost
{
    private sealed partial class DashboardVisual
    {
        private TempCleanupScan? _tempScan;
        private TempCleanupResult? _tempResult;
        private CancellationTokenSource? _tempOperation;
        private bool _confirmTempRemoval;
        private string? _tempMessage;

        private bool CanOpenCleanup => _actions.TempScan is not null || _actions.StorageCleanup is not null;
        private void OpenCleanup()
        {
            if (_actions.TempScan is not null) Select(TempCleanupPage);
            else if (_actions.StorageCleanup is { } fallback) Run(fallback);
        }

        private void ResetTempCleanup()
        {
            _tempOperation?.Cancel();
            _tempScan = null;
            _tempResult = null;
            _tempMessage = null;
            _confirmTempRemoval = false;
        }

        private void DrawTempCleanup(DrawingContext drawing)
        {
            var width = Width - 255;
            Label(drawing, "Your user Temp folder", 230, 80, 16, _text, true);
            Label(drawing, "Scans up to 5,000 accessible files. Cleanup only removes top-level .tmp and .temp files at least 7 days old.",
                230, 108, 12, _dim, width: width);
            var canScan = _tempOperation is null && _actions.TempScan is not null;
            if (canScan)
                DrawPageButton(drawing, "Scan temp files", new Rect(230, 154, 140, 31), false, () => _ = ScanTemp(), pinned: false);
            else
                Label(drawing, _tempMessage ?? "Please wait…", 230, 160, 12, _dim);
            DrawPageButton(drawing, "Back to Disk", new Rect(Width - 163, 154, 128, 31), false, () => Select("Disk"), pinned: false);

            if (_tempScan is { } scan)
            {
                drawing.DrawRoundedRectangle(_surface, _palette.CardBorder, new Rect(230, 204, width, 92), 8, 8);
                Label(drawing, "Temp files scanned", 246, 220, 12, _dim);
                Label(drawing, $"{scan.TotalFiles:N0} files · {CleanupSize(scan.TotalBytes)}", 246, 244, 18, _text, true, width / 2 - 25);
                Label(drawing, "Estimated space to free", 230 + width / 2, 220, 12, _dim);
                Label(drawing, $"{scan.Eligible.Count:N0} files · {CleanupSize(scan.EligibleBytes)}", 230 + width / 2, 244, 18, _text, true, width / 2 - 15);
                Label(drawing, scan.Error ?? (scan.Limited ? "Partial scan. The estimate covers files that were read." : "Locked or changed files are skipped during cleanup."),
                    230, 307, 11, _dim, width: width);
                if (_tempOperation is null && scan.Error is null && scan.Eligible.Count > 0 && _actions.TempClean is not null)
                {
                    if (_confirmTempRemoval)
                    {
                        Label(drawing, $"Permanently delete {scan.Eligible.Count:N0} eligible files? This cannot be undone.", 230, 341, 12, _text, true, width);
                        DrawPageButton(drawing, "Delete permanently", new Rect(230, 375, 157, 32), true, () => _ = CleanTemp(), pinned: false);
                        DrawPageButton(drawing, "Cancel", new Rect(398, 375, 94, 32), false,
                            () => { _confirmTempRemoval = false; Draw(_scale); }, pinned: false);
                    }
                    else
                        DrawPageButton(drawing, "Review removal", new Rect(230, 342, 150, 32), false,
                            () => { _confirmTempRemoval = true; Draw(_scale); }, pinned: false);
                }
            }
            if (_tempResult is { } result)
            {
                Label(drawing, $"Removed {result.Removed:N0} files · {CleanupSize(result.RemovedBytes)} of file data", 230, 215, 14, _text, true, width);
                Label(drawing, $"Skipped {result.Skipped:N0} files.", 230, 247, 12, _dim, width: width);
                if (result.FreeSpaceChange is { } change)
                    Label(drawing, $"Drive free-space change: {(change < 0 ? "−" : "+")}{CleanupSize(Math.Abs(change))}. Other activity can affect this number.",
                        230, 277, 12, _dim, width: width);
            }
            if (_tempOperation is null && _tempMessage is { } message) Label(drawing, message, 230, 436, 12, _dim, width: width);
            _bottom = 466;
        }

        private Task ScanTemp() => RunTempOperation("Scanning…", token => _actions.TempScan!(token), result => _tempScan = result);

        private Task CleanTemp()
        {
            if (!_confirmTempRemoval || _tempScan is not { } scan || scan.Error is not null || _actions.TempClean is not { } clean)
                return Task.CompletedTask;
            return RunTempOperation("Removing eligible files…", token => clean(scan, token), result => _tempResult = result);
        }

        private async Task RunTempOperation<T>(string message, Func<CancellationToken,T> work, Action<T> present)
        {
            if (_tempOperation is not null) return;
            using var cancellation = new CancellationTokenSource();
            _tempOperation = cancellation;
            _tempScan = null;
            _tempResult = null;
            _confirmTempRemoval = false;
            _tempMessage = message;
            Draw(_scale);
            try
            {
                var token = cancellation.Token;
                var result = await Task.Run(() => work(token), token);
                if (!cancellation.IsCancellationRequested) { present(result); _tempMessage = null; }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is not OutOfMemoryException and not AccessViolationException)
            {
                if (!cancellation.IsCancellationRequested) _tempMessage = "Windows could not complete the operation. Scan again to check what remains.";
            }
            finally
            {
                _tempOperation = null;
                if (_page == TempCleanupPage && IsVisible) Draw(_scale);
            }
        }

        private static string CleanupSize(long bytes) => bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.00} GiB"
            : bytes >= 1L << 20 ? $"{bytes / (double)(1L << 20):0.0} MiB"
            : bytes >= 1024 ? $"{bytes / 1024.0:0.0} KiB" : $"{bytes:N0} B";
    }
}
