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
        private TempCleanupProgress _tempProgress;
        private bool _removingTemp;

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
            drawing.DrawRoundedRectangle(_surface, _palette.CardBorder, new Rect(230, 75, width, 69), 10, 10);
            Label(drawing, "Free up space from temporary files", 246, 84, 16, _text, true, width - 32);
            Label(drawing, "Review the estimate first. Only .tmp and .temp files at least 7 days old in the main Temp folder qualify.",
                246, 109, 11, _dim, width: width - 32);
            var canScan = _tempOperation is null && _actions.TempScan is not null;
            if (canScan)
                DrawPageButton(drawing, "Scan temp files", new Rect(230, 154, 140, 31), false, () => _ = ScanTemp(), pinned: false);
            DrawPageButton(drawing, "Back to Disk", new Rect(Width - 163, 154, 128, 31), false, () => Select("Disk"), pinned: false);

            if (_tempOperation is not null)
            {
                drawing.DrawRoundedRectangle(_surface, _palette.CardBorder, new Rect(230, 204, width, 118), 10, 10);
                Label(drawing, TempActivity, 246, 217, 15, _text, true, width - 32);
                Label(drawing, TempProgressDetail, 246, 246, 12, _dim, width: width - 32);
                WorkProgress.Draw(drawing, new Rect(246, 276, width - 32, 6), _palette.Metrics.Track, _blue,
                    _tempProgress.Total is > 0 ? (double)_tempProgress.Processed / _tempProgress.Total.Value : null);
                if (!_removingTemp && !_tempOperation.IsCancellationRequested)
                    DrawPageButton(drawing, "Cancel scan", new Rect(246, 337, 110, 31), false, CancelTempScan, pinned: false);
            }

            if (_tempScan is { } scan)
            {
                drawing.DrawRoundedRectangle(_surface, _palette.CardBorder, new Rect(230, 204, width, 92), 8, 8);
                Label(drawing, "Temp files scanned", 246, 220, 12, _dim);
                Label(drawing, $"{scan.TotalFiles:N0} files · {CleanupSize(scan.TotalBytes)}", 246, 244, 18, _text, true, width / 2 - 25);
                Label(drawing, "Estimated space to free", 230 + width / 2, 220, 12, _dim);
                Label(drawing, $"{scan.Eligible.Count:N0} files · {CleanupSize(scan.EligibleBytes)}", 230 + width / 2, 244, 18, _text, true, width / 2 - 15);
                Label(drawing, scan.Error ?? (scan.Limited ? "Partial scan. The estimate covers files that were read." : scan.Eligible.Count == 0 ? "Scan complete. No files qualify for removal." : "Scan complete. Locked or changed files are skipped during removal."),
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
                drawing.DrawRoundedRectangle(_surface, _palette.CardBorder, new Rect(230, 204, width, 125), 10, 10);
                Label(drawing, $"Removed {result.Removed:N0} files · {CleanupSize(result.RemovedBytes)} of file data", 246, 220, 15, _text, true, width - 32);
                Label(drawing, $"Skipped {result.Skipped:N0} files.", 246, 252, 12, _dim, width: width - 32);
                if (result.FreeSpaceChange is { } change)
                    Label(drawing, $"Drive free-space change: {(change < 0 ? "−" : "+")}{CleanupSize(Math.Abs(change))}. Other activity can affect this number.",
                        246, 280, 12, _dim, width: width - 32);
            }
            if (_tempOperation is null && _tempMessage is { } message) Label(drawing, message, 230, 219, 12, _dim, width: width);
            _bottom = 466;
        }

        private string TempActivity => _tempOperation?.IsCancellationRequested == true ? "Cancelling scan…"
            : _removingTemp ? "Removing eligible files…" : "Scanning temporary files…";

        private string TempProgressDetail => _removingTemp
            ? $"{_tempProgress.Processed:N0} of {_tempProgress.Total:N0} checked · {_tempProgress.EligibleOrRemoved:N0} removed · {CleanupSize(_tempProgress.Bytes)}"
            : $"{_tempProgress.Processed:N0} files checked · {_tempProgress.EligibleOrRemoved:N0} eligible · {CleanupSize(_tempProgress.Bytes)} scanned";

        private void CancelTempScan()
        {
            if (_removingTemp || _tempOperation is null) return;
            _tempOperation.Cancel();
            _tempMessage = "Scan cancelled. No files were removed.";
            Draw(_scale);
        }

        private Task ScanTemp() => RunTempOperation(false, _actions.TempScan!, result => _tempScan = result);

        private Task CleanTemp()
        {
            if (!_confirmTempRemoval || _tempScan is not { } scan || scan.Error is not null || _actions.TempClean is not { } clean)
                return Task.CompletedTask;
            return RunTempOperation(true, (token, report) => clean(scan, token, report), result => _tempResult = result, scan.Eligible.Count);
        }

        private async Task RunTempOperation<T>(bool removing, Func<CancellationToken,Action<TempCleanupProgress>,T> work,
            Action<T> present, int? total = null)
        {
            if (_tempOperation is not null) return;
            using var cancellation = new CancellationTokenSource();
            _tempOperation = cancellation;
            _tempScan = null;
            _tempResult = null;
            _confirmTempRemoval = false;
            _tempMessage = null;
            _tempProgress = new(0, 0, total);
            _removingTemp = removing;
            Draw(_scale);
            try
            {
                var token = cancellation.Token;
                void Report(TempCleanupProgress progress)
                {
                    if (token.IsCancellationRequested || Dispatcher.HasShutdownStarted) return;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!ReferenceEquals(_tempOperation, cancellation) || token.IsCancellationRequested ||
                            _page != TempCleanupPage || !IsVisible) return;
                        _tempProgress = progress;
                        Draw(_scale);
                    }));
                }
                var result = await Task.Run(() => work(token, Report), token);
                if (!token.IsCancellationRequested) present(result);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
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
