using System.Globalization;
using Islet.Core.Island;
using Islet.Core.Settings;

namespace Islet.Shell.Island;

public static class IslandLaunchArguments
{
    public static bool TryParse(string[] args, out NativeIslandOptions options, out string? report)
    {
        options = new(); report = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var flag = args[index];
            if (!seen.Add(flag)) return false;
            switch (flag)
            {
                case "--native-probe": options = options with { Minimal = true }; break;
                case "--hidden": options = options with { Hidden = true }; break;
                case "--exercise-shell": options = options with { Exercise = true }; break;
                case "--exit-after-seconds":
                    if (++index >= args.Length || !int.TryParse(args[index], NumberStyles.None, CultureInfo.InvariantCulture,
                        out var seconds) || seconds < 1 || seconds > 3600) return false;
                    options = options with { ExitAfterSeconds = seconds }; break;
                case "--probe-report":
                    if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index])) return false;
                    report = args[index]; break;
                case "--style":
                    if (++index >= args.Length || !Enum.TryParse<IslandStyle>(args[index], true, out var style) || !Enum.IsDefined(style)) return false;
                    options = options with { Style = style }; break;
                case "--state":
                    if (++index >= args.Length || !Enum.TryParse<IslandState>(args[index], true, out var state) || !Enum.IsDefined(state)) return false;
                    options = options with { State = state }; break;
                default: return false;
            }
        }
        if (options.Minimal && (options.ExitAfterSeconds is null || report is null
            || seen.Any(s => s is not ("--native-probe" or "--exit-after-seconds" or "--probe-report")))) return false;
        if (options.Exercise && options.ExitAfterSeconds is null) options = options with { ExitAfterSeconds = 24 };
        return true;
    }
}
