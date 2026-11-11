using System.Windows;
using System.Windows.Media;

namespace Islet.Shell.Bar;

public sealed partial class DashboardHost
{
    private sealed partial class DashboardVisual
    {
        private CancellationTokenSource? _inspectionCancellation;
        private Task<string>? _inspectionTask;
        private string? _inspectionResult;
        private bool ShowsInspection => _page == "Network" && _actions.WifiDiagnostics is not null ||
            _page == "Disk" && (_actions.StorageHealth is not null || _actions.StorageCleanup is not null || _actions.TempScan is not null);

        private void DrawInspection(DrawingContext drawing)
        {
            var wifi = _page == "Network";
            var check = wifi ? _actions.WifiDiagnostics : _actions.StorageHealth;
            var card = new Rect(230, 65, Width - 255, 85);
            drawing.DrawRoundedRectangle(_surface, _palette.CardBorder, card, 8, 8);
            Label(drawing, wifi ? "Wi-Fi diagnostics" : "Storage health", 244, 74, 14, _text, true);
            if (check is not null)
                DrawPageButton(drawing, wifi ? "Run checks" : "Check health", new Rect(card.Right - 123, 70, 109, 26),
                    false, () => _ = Inspect(check));
            if (!wifi && CanOpenCleanup)
                DrawPageButton(drawing, "Review cleanup", new Rect(card.Right - 248, 70, 116, 26), false, OpenCleanup);
            var detail = _inspectionResult ?? (wifi
                ? "On demand · pings the router, checks 1.1.1.1:443 and resolves example.com."
                : "Check physical drive status or review temporary files.");
            Label(drawing, detail, 244, 107, 11, _dim, width: card.Width - 28);
        }

        private async Task Inspect(Func<CancellationToken,Task<string>> check)
        {
            if (_inspectionTask is { IsCompleted: false }) return;
            using var cancellation = new CancellationTokenSource();
            _inspectionCancellation = cancellation;
            _inspectionResult = "Checking…";
            Draw(_scale);
            var token = cancellation.Token;
            _inspectionTask = Task.Run(() => check(token), token);
            try
            {
                var result = await _inspectionTask.WaitAsync(TimeSpan.FromSeconds(8), cancellation.Token);
                if (!cancellation.IsCancellationRequested) _inspectionResult = result;
            }
            catch (OperationCanceledException) { }
            catch (TimeoutException)
            {
                cancellation.Cancel();
                _inspectionResult = "Windows is taking too long to respond.";
                Draw(_scale);
            }
            catch (Exception error) when (error is not OutOfMemoryException and not AccessViolationException)
            {
                _inspectionResult = "Windows could not complete this check.";
            }
            finally
            {
                if (ReferenceEquals(_inspectionCancellation, cancellation)) _inspectionCancellation = null;
                if (!cancellation.IsCancellationRequested) Draw(_scale);
            }
        }
    }
}
