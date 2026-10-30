using Islet.Core.Activities;

namespace Islet.Windows.Media;

/// <summary>No window/message loop. EOF wins even while a player API is unresponsive.</summary>
public static class MediaWorkerServer
{
    public static async Task<int> RunAsync(Stream input, Stream output, IMediaProvider provider,
        TimeSpan? operationTimeout = null, TimeSpan? idleTimeout = null)
    {
        var operationLimit = operationTimeout ?? TimeSpan.FromSeconds(8);
        var idleLimit = idleTimeout ?? TimeSpan.FromSeconds(12);
        var reads = 0; var commands = 0;
        try
        {
            var next = MediaWorkerProtocol.ReadAsync<MediaWorkerRequest>(input);
            while (true)
            {
                using var idle = new CancellationTokenSource();
                var idleDeadline = Task.Delay(idleLimit, idle.Token);
                if (await Task.WhenAny(next, idleDeadline).ConfigureAwait(false) != next) return 0;
                idle.Cancel();
                var request = await next.ConfigureAwait(false);
                if (request is null) return 0;
                if (request.Version != 1 || (request.Command.HasValue && (!Enum.IsDefined(request.Command.Value)
                    || string.IsNullOrWhiteSpace(request.SessionToken) || request.SessionToken.Length > 160))) return 2;
                next = MediaWorkerProtocol.ReadAsync<MediaWorkerRequest>(input);
                async Task<MediaWorkerResponse> Execute()
                {
                    MediaReading? reading = null; MediaCommandResult? result = null;
                    if (request.Command is { } command) { commands++; result = await provider.ExecuteAsync(command, request.SessionToken!).ConfigureAwait(false); }
                    else { reads++; reading = await provider.ReadAsync().ConfigureAwait(false); }
                    var assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToArray();
                    return new(1, reading, result, new(Environment.ProcessId, reads, commands,
                        assemblies.Where(n => n is "Microsoft.Windows.SDK.NET" or "WinRT.Runtime").Select(n => n!).ToArray(),
                        assemblies.Where(n => n is "PresentationCore" or "PresentationFramework" or "WindowsBase").Select(n => n!).ToArray()));
                }
                // Run even the synchronous prefix off the input-reading path.
                var operation = Task.Run(Execute);
                using var deadline = new CancellationTokenSource();
                var timeout = Task.Delay(operationLimit, deadline.Token);
                var completed = await Task.WhenAny(operation, next, timeout).ConfigureAwait(false);
                deadline.Cancel();
                if (completed != operation) return 0; // EOF/premature pipelining/deadline: return, never kill.
                var response = await operation.ConfigureAwait(false);
                await MediaWorkerProtocol.WriteAsync(output, response).WaitAsync(operationLimit).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or System.Text.Json.JsonException
            or InvalidOperationException or TimeoutException or UnauthorizedAccessException)
        { return 1; }
    }
}
