using System.IO;
using DuoGPU.Core;
using DuoGPU.Native;
using Xunit;

namespace DuoGPU.Tests;

public class EdidTests
{
    [Theory]
    // "VIE" = 22, 9, 5 in 5-bit letters = 0x5925, which the CCD API hands over byte-swapped as 0x2559
    [InlineData(0x2559, 0x0490, "VIE0490")]
    // "DEL" = 4, 5, 12 = 0x10AC, swapped 0xAC10
    [InlineData(0xAC10, 0xA0E1, "DELA0E1")]
    // "SAM" = 19, 1, 13 = 0x4C2D, swapped 0x2D4C
    [InlineData(0x2D4C, 0x7123, "SAM7123")]
    public void PnpId_matches_the_name_Windows_gives_the_monitor(int manufacturer, int product, string expected) =>
        Assert.Equal(expected, Edid.PnpId((ushort)manufacturer, (ushort)product));
}

public class MccsTests
{
    [Fact]
    public void Inputs_are_read_from_the_vcp_section()
    {
        const string caps = "(prot(monitor)type(LCD)model(S49)cmds(01 02 03 07 0C E3 F3)vcp(02 04 05 08 10 12 14(05 08 0B) " +
                            "16 18 1A 52 60(11 12 0F 10) 62 AC AE B2 B6 C6 C8 C9 D6(01 04) DF)mccs_ver(2.2))";
        Assert.Equal([0x11, 0x12, 0x0F, 0x10], Mccs.Inputs(caps));
    }

    [Fact]
    public void A_space_before_the_list_is_accepted() =>
        Assert.Equal([0x0F, 0x11], Mccs.Inputs("vcp(10 60 (0F 11) 62)"));

    [Fact]
    public void Other_codes_ending_in_60_are_not_taken_for_the_input_list() =>
        Assert.Empty(Mccs.Inputs("vcp(10 E60(01 02) 62)"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("vcp(10 12 62)")]
    public void No_list_gives_an_empty_result(string? caps) => Assert.Empty(Mccs.Inputs(caps));

    [Theory]
    [InlineData(0x0F, "DisplayPort 1 (0x0F)")]
    [InlineData(0x11, "HDMI 1 (0x11)")]
    [InlineData(0x55, "0x55")]
    public void Inputs_have_readable_names(int value, string expected) => Assert.Equal(expected, Mccs.InputName(value));
}

public class RefreshTests
{
    static readonly Gdi.Mode[] Modes =
    [
        new(5120, 1440, 60), new(5120, 1440, 100), new(5120, 1440, 120), new(5120, 1440, 59),
        new(3840, 1080, 144), new(1920, 1080, 165),
    ];

    [Fact]
    public void Without_a_tested_limit_the_highest_refresh_is_used() =>
        Assert.Equal(new Gdi.Mode(5120, 1440, 120), Refresh.Best(Modes, 5120, 1440, null));

    [Fact]
    public void The_tested_limit_of_the_path_is_respected() =>
        // the 60 Hz path from the measurements: the driver offers 120, the cable shows black above 60
        Assert.Equal(new Gdi.Mode(5120, 1440, 60), Refresh.Best(Modes, 5120, 1440, 60));

    [Fact]
    public void A_size_the_output_does_not_offer_falls_back_to_the_largest() =>
        Assert.Equal(new Gdi.Mode(5120, 1440, 100), Refresh.Best(Modes, 7680, 2160, 100));

    [Fact]
    public void Calibration_climbs_from_the_safe_60_Hz() =>
        // 59 Hz is left out: it is below the safe start, and 60 already shows a picture on every cable
        Assert.Equal([60, 100, 120], Refresh.Ascending(Modes, 5120, 1440));

    [Fact]
    public void A_monitor_with_only_low_rates_still_gets_steps() =>
        Assert.Equal([30, 50], Refresh.Ascending([new(3840, 2160, 50), new(3840, 2160, 30)], 3840, 2160));

    [Fact]
    public void An_untested_path_starts_at_the_safe_rate() =>
        Assert.Equal(new Gdi.Mode(5120, 1440, 60), Refresh.Best(Modes, 5120, 1440, Refresh.SafeHz));
}

public class ConfigTests
{
    [Fact]
    public void Saves_and_loads_every_field()
    {
        var path = Path.Combine(Path.GetTempPath(), $"duogpu-test-{Guid.NewGuid():N}.json");
        try
        {
            var cfg = new Config
            {
                SharedMonitor = "VIE0490",
                AiGpu = "big",
                OtherGpu = "small",
                InputByGpu = { ["big"] = 0x10, ["small"] = 0x0F },
                MaxHzByGpu = { ["big"] = 120, ["small"] = 60 },
                Scale = 150,
                LastMode = Mode.Ai,
                Language = "pt-BR",
            };
            cfg.Save(path);
            var back = Config.Load(path);
            Assert.Equal(cfg.ToJson(), back.ToJson());
            Assert.Contains("\"LastMode\": \"Ai\"", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Each_mode_uses_the_right_gpu()
    {
        var cfg = new Config { SharedMonitor = "M", AiGpu = "big", OtherGpu = "small" };
        Assert.Equal("small", cfg.GpuFor(Mode.Ai));
        Assert.Equal("big", cfg.GpuFor(Mode.Game));
    }

    [Theory]
    [InlineData(null, "big", "small", false)]
    [InlineData("M", null, "small", false)]
    [InlineData("M", "big", "big", false)]
    [InlineData("M", "big", "small", true)]
    public void A_config_is_complete_only_with_a_monitor_and_two_different_gpus(string? m, string? ai, string? other, bool ok) =>
        Assert.Equal(ok, new Config { SharedMonitor = m, AiGpu = ai, OtherGpu = other }.IsComplete);

    [Fact]
    public void A_broken_file_starts_a_fresh_setup()
    {
        var path = Path.Combine(Path.GetTempPath(), $"duogpu-test-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "{ not json");
        try
        {
            Assert.False(Config.Load(path).IsComplete);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class OtherMonitorsTests
{
    const string Shared = "VIE0490";

    static OutputInfo Out(string gdi, string monitor, int x, int y, int w = 1920, int h = 1080, int hz = 165, bool active = true) =>
        new(gdi, null, 0, active, [monitor], active ? new Gdi.Placement(x, y, w, h, hz) : null);

    static readonly OutputInfo[] Desk =
    [
        Out(@"\.\DISPLAY29", Shared, 0, 0, 5120, 1440, 60),
        Out(@"\.\DISPLAY28", "MCR3150", 640, -1080),
        Out(@"\.\DISPLAY30", "CJH3150", 2560, -1080),
    ];

    static Dictionary<string, SavedOutput> Saved() => new()
    {
        ["MCR3150"] = new(640, -1080, 1920, 1080, 165),
        ["CJH3150"] = new(2560, -1080, 1920, 1080, 165),
    };

    static string Name(string id) => id;

    [Fact]
    public void Everything_in_place_gives_one_ok_line_per_monitor()
    {
        var checks = OtherMonitors.Compare(Saved(), Desk, Shared, Name);
        Assert.Equal(2, checks.Count);
        Assert.All(checks, c => Assert.True(c.Ok));
        Assert.All(checks, c => Assert.Equal(CheckKind.OtherMode, c.Kind));
    }

    [Fact]
    public void The_shared_monitor_is_never_one_of_the_others() =>
        Assert.DoesNotContain(OtherMonitors.Compare(Saved(), Desk, Shared, Name), c => c.Detail.Contains(Shared));

    [Fact]
    public void A_lower_refresh_is_found_and_can_be_fixed()
    {
        var desk = Desk.ToArray();
        desk[1] = Out(@"\.\DISPLAY28", "MCR3150", 640, -1080, hz: 60);
        var bad = Assert.Single(OtherMonitors.Compare(Saved(), desk, Shared, Name), c => !c.Ok);
        Assert.Equal(CheckKind.OtherMode, bad.Kind);
        Assert.True(bad.SafeToFix);
        Assert.Equal(@"\.\DISPLAY28", bad.Target);
        Assert.Equal(165, bad.Want!.Hz);
    }

    [Fact]
    public void A_moved_monitor_is_found_and_can_be_fixed()
    {
        var desk = Desk.ToArray();
        desk[2] = Out(@"\.\DISPLAY30", "CJH3150", 5120, 0);
        var bad = Assert.Single(OtherMonitors.Compare(Saved(), desk, Shared, Name), c => !c.Ok);
        Assert.Equal(CheckKind.OtherPosition, bad.Kind);
        Assert.Equal(new SavedOutput(2560, -1080, 1920, 1080, 165), bad.Want);
    }

    [Fact]
    public void A_monitor_that_is_off_needs_the_person()
    {
        var desk = new[] { Desk[0], Desk[1], Out(@"\.\DISPLAY30", "CJH3150", 0, 0, active: false) };
        var bad = Assert.Single(OtherMonitors.Compare(Saved(), desk, Shared, Name), c => !c.Ok);
        Assert.Equal(CheckKind.OtherMissing, bad.Kind);
        Assert.False(bad.SafeToFix);
    }

    [Fact]
    public void Two_identical_models_are_kept_apart()
    {
        var desk = new[] { Desk[0], Out(@"\.\DISPLAY1", "DEL4141", 1920, -1080), Out(@"\.\DISPLAY2", "DEL4141", -1920, 0) };
        var topo = new Topology { Gpus = [], Outputs = desk, MonitorNames = new Dictionary<string, string>() };
        var saved = OtherMonitors.Capture(topo, Shared);
        Assert.Equal(["DEL4141", "DEL4141#2"], saved.Keys.Order().ToArray());
        Assert.Equal(-1920, saved["DEL4141"].X); // ordered left to right
    }
}
