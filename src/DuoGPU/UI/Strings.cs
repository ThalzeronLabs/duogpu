using System.Globalization;
using DuoGPU.Core;

namespace DuoGPU.UI;

/// <summary>Every visible text, in English and Brazilian Portuguese. Follows Windows unless the config says otherwise.</summary>
public static class S
{
    static bool pt = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt";

    public static void Use(string? language)
    {
        if (language != null) pt = language.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPortuguese => pt;

    static string T(string en, string ptBr) => pt ? ptBr : en;

    public static string ModeName(Mode m) => m switch
    {
        Mode.Ai => T("AI MODE", "MODO IA"),
        Mode.Game => T("GAME MODE", "MODO JOGO"),
        _ => T("ATTENTION", "ATENÇÃO"),
    };

    public static string ModeShort(Mode m) => m switch
    {
        Mode.Ai => T("AI mode", "Modo IA"),
        Mode.Game => T("Game mode", "Modo Jogo"),
        _ => T("Attention", "Atenção"),
    };

    public static string IconText => T("AI", "IA");

    public static string Subtitle(Mode m, string aiGpu, string monitor) => m switch
    {
        Mode.Ai => T($"{aiGpu} is free for your local model.", $"A {aiGpu} fica livre para o seu modelo local."),
        Mode.Game => T($"{monitor} runs straight from {aiGpu}.", $"O {monitor} sai direto da {aiGpu}, sem cópia."),
        _ => T("The displays are not where DuoGPU left them.", "As telas não estão como o DuoGPU deixou."),
    };

    public static string SwitchTo(Mode target) => T($"Switch to {ModeShort(target)}", $"Trocar para o {ModeShort(target)}");
    public static string Switching => T("Switching…  confirm on the monitor", "Trocando…  confirme no monitor");
    public static string FixNow => T("Fix now", "Arrumar agora");
    public static string OpenPanel => T("Open panel", "Abrir o painel");
    public static string Settings => T("Setup…", "Configurar…");
    public static string Quit => T("Quit", "Sair");
    public static string Help => T("Offline help", "Ajuda sem internet");
    public static string HideHelp => T("Hide help", "Esconder a ajuda");
    public static string CheckedAt(DateTime t) => T($"checked at {t:HH:mm:ss}", $"conferido às {t:HH:mm:ss}");
    static CultureInfo Numbers => pt ? CultureInfo.GetCultureInfo("pt-BR") : CultureInfo.InvariantCulture;

    public static string FreeVram(string gpu, double usedGb, double freeGb) => string.Format(Numbers,
        T("{0}: {1:F1} GB in use · {2:F1} GB free", "{0}: {1:F1} GB em uso · {2:F1} GB livres"), gpu, usedGb, freeGb);
    public static string Path(string monitor, string gpu) => $"{monitor}  →  {gpu}";
    public static string Signal(int w, int h, int hz, int? scale) =>
        T($"{w}×{h} · {hz} Hz", $"{w}×{h} · {hz} Hz") + (scale is int s ? T($" · scale {s}%", $" · escala {s}%") : "");

    public static string CheckText(Check c, string monitor)
    {
        var p = c.Detail.Split('|');
        return c.Kind switch
        {
            CheckKind.DuplicateMonitor => T($"{monitor} is on through BOTH GPUs (Windows sees it twice)",
                $"O {monitor} está ligado pelas DUAS placas (o Windows vê ele duas vezes)"),
            CheckKind.NoOutput => T($"{monitor} is not on through either GPU", $"O {monitor} não está ligado por nenhuma placa"),
            CheckKind.Scale => c.Ok ? T($"Scale {p[0]}", $"Escala {p[0]}")
                : T($"Scale {p[0]} (should be {p[1]})", $"Escala {p[0]} (o certo: {p[1]})"),
            CheckKind.Refresh => c.Ok ? T($"{p[0]} at {p[1]} Hz", $"{p[0]} a {p[1]} Hz")
                : T($"{p[0]} at {p[1]} Hz (should be {p[2]} Hz)", $"{p[0]} a {p[1]} Hz (o certo: {p[2]} Hz)"),
            _ => c.Detail,
        };
    }

    public static string OnceOnly(string monitor) => T($"{monitor} shows up once", $"O {monitor} aparece uma vez só");

    public static string HelpText(string monitor, string aiGpu, string otherGpu, string? aiInput, string? otherInput, int seconds)
    {
        string game = aiInput ?? T($"the input with the {aiGpu} cable", $"a entrada do cabo da {aiGpu}");
        string ai = otherInput ?? T($"the input with the {otherGpu} cable", $"a entrada do cabo da {otherGpu}");
        return T(
            $"""
            • Black screen after a switch: wait {seconds} s, DuoGPU goes back by itself. If it does not, press the INPUT button on {monitor} and pick {game} for Game mode or {ai} for AI mode. Then click "Fix now" here.
            • Amber icon (!): open this panel and click "Fix now".
            • The scale or the refresh changed: DuoGPU puts them back within 5 s.
            • The icon is gone: press Windows, type "DuoGPU" and press Enter.
            • The log: %LOCALAPPDATA%\DuoGPU\duogpu.log
            """,
            $"""
            • Tela preta depois de uma troca: espere {seconds} s, o DuoGPU volta sozinho. Se não voltar, aperte o botão de ENTRADA do {monitor} e escolha {game} para o Modo Jogo ou {ai} para o Modo IA. Depois, "Arrumar agora" aqui.
            • Ícone âmbar (!): abra este painel e clique em "Arrumar agora".
            • A escala ou os Hz mudaram: o DuoGPU conserta sozinho em até 5 s.
            • O ícone sumiu: tecla Windows, digite "DuoGPU" e Enter.
            • O registro: %LOCALAPPDATA%\DuoGPU\duogpu.log
            """);
    }

    public static string Tooltip(Mode m, bool attention) =>
        $"DuoGPU: {ModeShort(m)}" + (attention ? T(" · needs fixing (click)", " · precisa arrumar (clique)") : "");

    public static string BalloonTitle => T("The displays left the right setup", "As telas saíram da configuração certa");
    public static string BalloonText => T("Open the panel and click \"Fix now\".", "Abra o painel e clique em \"Arrumar agora\".");

    public static string ConfirmQuestion => T("Can you see this window?", "Você está vendo esta janela?");
    public static string ConfirmCountdown(int s) => T($"Without a click, DuoGPU goes back in {s} s.", $"Sem o clique, o DuoGPU volta em {s} s.");
    public static string Keep => T("Yes, keep it", "Sim, manter");
    public static string Reverted => T("No confirmation: went back to the previous mode.", "Sem confirmação: voltei ao modo anterior.");

    public static string Result(SwitchResult r) => r.Message switch
    {
        "switched" => T($"{ModeShort(r.Mode)} is on.", $"{ModeShort(r.Mode)} ligado."),
        "already" => T($"Already in {ModeShort(r.Mode)}.", $"Já está no {ModeShort(r.Mode)}."),
        "reverted" => Reverted,
        "busy" => T("A switch is already running.", "Já tem uma troca em andamento."),
        "cable-missing" => T("The second cable is not detected. Check that both cables are plugged in.",
            "Não achei o segundo cabo. Confira se os dois cabos estão ligados."),
        _ => T($"The switch failed ({r.Message}). See the log.", $"A troca falhou ({r.Message}). Veja o registro."),
    };

    // the setup
    public static string SetupTitle => T("DuoGPU setup", "Configurar o DuoGPU");
    public static string SetupIntro => T(
        "Plug your main monitor into BOTH GPUs, one cable each, into two different inputs of the monitor. DuoGPU found this:",
        "Ligue o monitor principal nas DUAS placas, um cabo em cada, em duas entradas diferentes dele. O DuoGPU achou isto:");
    public static string SharedMonitorLabel => T("Monitor with two cables", "Monitor com dois cabos");
    public static string AiGpuLabel => T("GPU kept free for AI (Game mode uses it for the monitor)", "Placa livre para a IA (o Modo Jogo usa ela no monitor)");
    public static string OtherGpuLabel => T("GPU that drives the monitor in AI mode", "Placa que mostra o monitor no Modo IA");
    public static string InputFor(string gpu) => T($"Monitor input of the {gpu} cable", $"A entrada do monitor onde está o cabo da {gpu}");
    public static string InputManual => T("Switch the input by hand (no DDC/CI)", "Trocar a entrada à mão (sem DDC/CI)");
    public static string InputHint(string? now) => now == null
        ? T("The monitor did not answer over DDC/CI. Turn on DDC/CI in its menu, or switch the input by hand.",
            "O monitor não respondeu pelo DDC/CI. Ligue o DDC/CI no menu dele ou troque a entrada à mão.")
        : T($"Showing now: {now}. Pick the input where the other GPU's cable is.",
            $"Na tela agora: {now}. Escolha a entrada onde está o cabo da outra placa.");
    public static string ScaleLabel => T("Scale to keep on this monitor", "Escala para manter neste monitor");
    public static string KeepAsIs => T("Leave as Windows sets it", "Deixar como o Windows puser");
    public static string StartWithWindows => T("Start with Windows", "Iniciar com o Windows");
    public static string Language => T("Language", "Idioma");
    public static string Save => T("Save", "Salvar");
    public static string TestSwitch => T("Save and test a switch", "Salvar e testar uma troca");
    public static string Calibrate => T("Find the highest refresh for this path", "Achar o maior Hz deste caminho");
    public static string CalibrateDone(int hz) => T($"This path shows a picture up to {hz} Hz. Saved.", $"Este caminho mostra imagem até {hz} Hz. Salvo.");
    public static string CalibrateFailed => T("No refresh rate was confirmed.", "Nenhum Hz foi confirmado.");
    public static string NoDualCable => T(
        "No monitor is connected to both GPUs yet. Plug a second cable from the other GPU into another input of your main monitor, then open the setup again.",
        "Ainda não há monitor ligado nas duas placas. Ligue um segundo cabo da outra placa em outra entrada do monitor principal e abra a configuração de novo.");
    public static string OneGpu => T("DuoGPU needs two GPUs. Windows reports only one.", "O DuoGPU precisa de duas placas. O Windows mostra só uma.");
    public static string Gb(ulong bytes) => $"{bytes / (double)(1UL << 30):F0} GB";
}
