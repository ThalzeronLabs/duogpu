using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DuoGPU.Core;

public enum Mode
{
    Unknown,
    /// <summary>The shared monitor runs on the smaller GPU, so the big one has no display at all.</summary>
    Ai,
    /// <summary>The shared monitor runs on the big GPU, so games render and display on the same card.</summary>
    Game,
}

/// <summary>What the first-run setup learned. Saved in %APPDATA%\DuoGPU\config.json.</summary>
public sealed class Config
{
    /// <summary>The monitor with one cable to each GPU, as Windows names it ("VIE0490").</summary>
    public string? SharedMonitor { get; set; }

    /// <summary>The GPU kept free for AI (its stable device path). By default, the one with the most VRAM.</summary>
    public string? AiGpu { get; set; }

    /// <summary>The other GPU, which drives the shared monitor in AI mode.</summary>
    public string? OtherGpu { get; set; }

    /// <summary>The monitor input (VCP 0x60 value) each GPU's cable is plugged into. Empty = switch by hand.</summary>
    public Dictionary<string, int> InputByGpu { get; set; } = [];

    /// <summary>The highest refresh rate that showed a picture on each GPU's path, found with the user watching.</summary>
    public Dictionary<string, int> MaxHzByGpu { get; set; } = [];

    /// <summary>The scale to keep on the shared monitor (Windows resets it when the cable changes). Null = leave it.</summary>
    public int? Scale { get; set; }

    /// <summary>The monitors DuoGPU does not move, as they were saved: the guard keeps them that way.</summary>
    public Dictionary<string, SavedOutput> OtherMonitors { get; set; } = [];

    public int ConfirmSeconds { get; set; } = 15;

    /// <summary>The guard puts back the scale and the best refresh by itself.</summary>
    public bool AutoFix { get; set; } = true;

    public Mode LastMode { get; set; } = Mode.Unknown;

    /// <summary>"pt-BR", "en" or null to follow Windows.</summary>
    public string? Language { get; set; }

    [JsonIgnore]
    public bool IsComplete => SharedMonitor != null && AiGpu != null && OtherGpu != null && AiGpu != OtherGpu;

    public int? InputFor(string gpu) => InputByGpu.TryGetValue(gpu, out var v) ? v : null;

    public int? MaxHzFor(string gpu) => MaxHzByGpu.TryGetValue(gpu, out var v) ? v : null;

    public string GpuFor(Mode mode) => mode == Mode.Game ? AiGpu! : OtherGpu!;

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DuoGPU");

    public static string FilePath => Path.Combine(Folder, "config.json");

    public static Config Load(string? path = null)
    {
        path ??= FilePath;
        try
        {
            if (File.Exists(path)) return JsonSerializer.Deserialize<Config>(File.ReadAllText(path), Json) ?? new Config();
        }
        catch (Exception e)
        {
            Log.Write($"config unreadable, starting fresh: {e.Message}");
        }
        return new Config();
    }

    public void Save(string? path = null)
    {
        path ??= FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, path, overwrite: true);
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static Config FromJson(string json) => JsonSerializer.Deserialize<Config>(json, Json) ?? new Config();
}

public static class Log
{
    static readonly object Gate = new();

    /// <summary>Where the log goes instead of the user's real one (the tests set it, so they never write there).</summary>
    internal static string? PathOverride { get; set; }

    public static string FilePath => PathOverride ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DuoGPU", "duogpu.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 1_000_000) File.Move(FilePath, FilePath + ".old", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // logging never breaks the app
        }
    }
}
