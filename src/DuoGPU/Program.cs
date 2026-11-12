using System.Runtime.InteropServices;
using DuoGPU.Core;

namespace DuoGPU;

public static class Program
{
    [DllImport("kernel32.dll")]
    static extern bool AttachConsole(int processId);

    const int ATTACH_PARENT_PROCESS = -1;

    public const string Usage = """
        DuoGPU: moves your main monitor between two GPUs.

          DuoGPU.exe              the tray app (starts the setup the first time)
          DuoGPU.exe detect       what DuoGPU sees: GPUs, outputs, the dual-cabled monitor (changes nothing)
            --save                saves that as the first configuration
          DuoGPU.exe status       the current mode and the guard's checks
          DuoGPU.exe ai           AI mode: the shared monitor on the smaller GPU
          DuoGPU.exe game         Game mode: the shared monitor on the big GPU
          DuoGPU.exe toggle       the other mode
          DuoGPU.exe fix          the guard's safe repairs, once (scale, tested refresh, other monitors)
          DuoGPU.exe remember     save the other monitors as they are now (the guard keeps them that way)
            --no-confirm          skip the "Can you see this?" window (scripts; use only after a tested switch)
        """;

    [STAThread]
    public static int Main(string[] args)
    {
        // no command (or --tray, used by the Windows startup entry): the tray app
        var command = args.FirstOrDefault(a => !a.StartsWith("--"))?.ToLowerInvariant();
        if (command == null)
            return UI.TrayApp.Run();

        AttachConsole(ATTACH_PARENT_PROCESS);
        try
        {
            return RunCommand(command, args);
        }
        catch (Exception e)
        {
            Log.Write($"command {command} failed: {e}");
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    static int RunCommand(string command, string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var cfg = Config.Load();
        switch (command)
        {
            case "detect":
                var proposal = Detect.Propose();
                Console.WriteLine();
                Console.WriteLine(Detect.Report(proposal, cfg));
                if (args.Contains("--save") && proposal.Config.IsComplete && !cfg.IsComplete)
                {
                    proposal.Config.Save();
                    Console.WriteLine($"Saved {Config.FilePath}. Open DuoGPU to pick the other input and test a switch.");
                }
                return 0;
            case "status":
                var st = Status.Read(cfg);
                Console.WriteLine();
                Console.WriteLine($"Mode: {st.Mode}{(st.NeedsAttention ? " (needs attention)" : "")}");
                foreach (var c in st.Checks) Console.WriteLine($"  {(c.Ok ? "ok" : "!!")} {c.Kind} {c.Detail}");
                return st.Mode == Mode.Unknown ? 2 : 0;
            case "ai" or "game" or "toggle":
                if (!cfg.IsComplete)
                {
                    Console.Error.WriteLine("Run DuoGPU once to set it up first.");
                    return 2;
                }
                var mode = command switch
                {
                    "ai" => Mode.Ai,
                    "game" => Mode.Game,
                    _ => Status.Read(cfg, withVram: false).Mode == Mode.Ai ? Mode.Game : Mode.Ai,
                };
                bool confirm = !args.Contains("--no-confirm");
                var result = confirm
                    ? UI.TrayApp.RunWithConfirm(cfg, mode)
                    : new Switcher(cfg).SwitchAsync(mode, null).GetAwaiter().GetResult();
                Console.WriteLine($"{result.Mode}: {result.Message}");
                return result.Ok ? 0 : 1;
            case "fix": // the guard's safe repairs, once
                var before = Status.Read(cfg, withVram: false);
                bool fixedSomething = new Switcher(cfg).FixSafe(before);
                var after = Status.Read(cfg, withVram: false);
                Console.WriteLine(fixedSomething ? "Put back what was safe to fix." : "Nothing safe to fix.");
                foreach (var c in after.Checks) Console.WriteLine($"  {(c.Ok ? "ok" : "!!")} {c.Kind} {c.Detail}");
                return after.Checks.All(c => c.Ok) ? 0 : 1;
            case "remember": // the other monitors, as they are now
                if (!cfg.IsComplete)
                {
                    Console.Error.WriteLine("Run DuoGPU once to set it up first.");
                    return 2;
                }
                cfg.OtherMonitors = OtherMonitors.Capture(Topology.Read(), cfg.SharedMonitor!);
                cfg.Save();
                foreach (var (k, v) in cfg.OtherMonitors) Console.WriteLine($"  {k}: {v.Width}x{v.Height}@{v.Hz} at ({v.X},{v.Y})");
                Console.WriteLine($"Saved {cfg.OtherMonitors.Count} other monitor(s).");
                return 0;
            case "render": // the README images, from the real panel code
                return UI.Screenshots.Render(args.Skip(1).FirstOrDefault(a => !a.StartsWith("--")) ?? "docs/images");
            default:
                Console.WriteLine(Usage);
                return command is "help" or "-h" or "/?" ? 0 : 1;
        }
    }
}
