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
    public void Calibration_tries_the_highest_rate_first() =>
        Assert.Equal([120, 100, 60, 59], Refresh.Candidates(Modes, 5120, 1440));
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
