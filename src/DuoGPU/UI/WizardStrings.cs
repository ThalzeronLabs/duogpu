using DuoGPU.Core;
namespace DuoGPU.UI;

/// <summary>The texts of the guided setup. Each step says what it does, why, and how long it takes.</summary>
public static partial class S
{
    public static string Step(int n, int total) => T($"Step {n} of {total}", $"Passo {n} de {total}");
    public static string Back => T("Back", "Voltar");
    public static string Next => T("Next", "Próximo");
    public static string Finish => T("Finish", "Concluir");
    public static string Close => T("Close", "Fechar");
    public static string NoScreenChange => T("Nothing changes on the screen in this step.", "Nada muda na tela neste passo.");

    // 1. welcome
    public static string WelcomeTitle => T("Welcome to DuoGPU", "Bem-vindo ao DuoGPU");
    public static string WelcomeBody => T(
        """
        DuoGPU moves your main monitor between your two GPUs with one click.

        •  AI mode: the monitor runs on the small GPU. The big one drives no display at all, so its whole VRAM is free for your model.
        •  Game mode: the monitor runs on the big GPU. Games render and display on the same card, with no frame copy.

        This setup has 7 short steps. Each one explains what it does before you click, and you can go back at any time.
        """,
        """
        O DuoGPU leva o seu monitor principal de uma placa de vídeo para a outra com um clique.

        •  Modo IA: o monitor roda na placa fraca. A forte não mostra tela nenhuma, então a VRAM inteira fica livre para o seu modelo.
        •  Modo Jogo: o monitor roda na placa forte. O jogo é desenhado e mostrado na mesma placa, sem cópia de quadros.

        Esta configuração tem 7 passos curtos. Cada um explica o que faz antes de você clicar, e dá para voltar a qualquer momento.
        """);
    public static string WelcomeSafety => T(
        "Nothing here is permanent. Every time the screen changes, a window asks \"Can you see this?\". If the screen goes black, just wait: after 15 seconds DuoGPU puts everything back by itself. It needs no administrator rights and never uses the internet.",
        "Nada aqui é permanente. Toda vez que a tela muda, uma janela pergunta \"Você está vendo esta janela?\". Se a tela ficar preta, é só esperar: em 15 segundos o DuoGPU volta tudo sozinho. Ele não pede administrador e nunca usa a internet.");
    public static string WelcomeTime => T("The whole setup: about 3 to 5 minutes.", "A configuração inteira: uns 3 a 5 minutos.");

    // 2. what was found
    public static string FoundTitle => T("What DuoGPU found", "O que o DuoGPU achou");
    public static string FoundGpus => T("Your GPUs", "As suas placas de vídeo");
    public static string FoundMonitor => T("The monitor with a cable to each GPU", "O monitor com um cabo em cada placa");
    public static string FoundOthers => T("Your other monitors stay exactly where they are:", "Os outros monitores ficam exatamente onde estão:");
    public static string FoundWhy => T(
        "The monitor that shows up on both GPUs is the one DuoGPU moves. The GPU with the most VRAM is kept free for AI, because that is where the model fits best. Change it below if you prefer.",
        "O monitor que aparece nas duas placas é o que o DuoGPU vai mover. A placa com mais VRAM fica livre para a IA, porque é nela que o modelo cabe melhor. Troque abaixo se preferir.");

    // 3. the inputs
    public static string InputsTitle => T("Which input is each cable on?", "Em qual entrada está cada cabo?");
    public static string InputsWhy => T(
        "Your monitor has several inputs (DisplayPort 1, HDMI 1…). DuoGPU switches between them through the cable itself, with DDC/CI: a small control channel inside the video cable that most monitors have. It needs to know which input each GPU's cable is plugged into.",
        "O seu monitor tem várias entradas (DisplayPort 1, HDMI 1…). O DuoGPU troca entre elas pelo próprio cabo, com o DDC/CI: um pequeno canal de controle dentro do cabo de vídeo, que a maioria dos monitores tem. Ele precisa saber em qual entrada está o cabo de cada placa.");
    public static string InputsRead(string input, string gpu) => T(
        $"The monitor says it is showing {input} right now. That is the input of the {gpu} cable.",
        $"O monitor diz que está mostrando a {input} agora. Essa é a entrada do cabo da {gpu}.");
    public static string InputsNoAnswer => T(
        "The monitor did not answer over DDC/CI. Turn on DDC/CI in the monitor's menu and open the setup again, or choose \"by hand\": DuoGPU will then ask you to press the monitor's input button at each switch.",
        "O monitor não respondeu pelo DDC/CI. Ligue o DDC/CI no menu do monitor e abra a configuração de novo, ou escolha \"à mão\": aí o DuoGPU pede para você apertar o botão de entrada do monitor a cada troca.");
    public static string InputsTip => T(
        "Not sure? Look at the labels on the back of the monitor, or go with your best guess: the test in step 5 shows right away if it is right.",
        "Na dúvida, olhe as etiquetas atrás do monitor ou vá no seu melhor palpite: o teste do passo 5 mostra na hora se está certo.");

    // 4. preferences
    public static string PrefsTitle => T("Your preferences", "As suas preferências");
    public static string PrefsScaleWhy => T(
        "Windows keeps one scale (\"Make everything bigger\") for each cable and resets it when the cable changes. DuoGPU puts back the scale you choose here after every switch.",
        "O Windows guarda uma escala (o \"Deixar tudo maior\") para cada cabo e zera quando o cabo muda. O DuoGPU devolve a escala que você escolher aqui depois de cada troca.");
    public static string PrefsStartWhy => T(
        "Starting with Windows keeps the icon in the tray and the guard checking your displays every 5 seconds.",
        "Iniciar com o Windows deixa o ícone na bandeja e o guarda conferindo as telas a cada 5 segundos.");
    public static string PrefsSaves => T("\"Next\" saves these settings.", "O \"Próximo\" salva estas escolhas.");

    // 5 and 6. the two paths
    public static string PathTitle(Mode mode, string gpu) => T(
        $"Test the {ModeShort(mode)} path ({gpu})", $"Testar o caminho do {ModeShort(mode)} ({gpu})");
    public static string PathWhat => T(
        """
        Now DuoGPU switches for real. What happens:
        1.  The monitor goes dark for about 5 seconds while the input and the outputs change.
        2.  A window appears in the middle of the monitor: "Can you see this window?". Click "Yes, keep it".
        3.  If the screen stays black, don't touch anything: after 15 seconds it goes back by itself.
        """,
        """
        Agora o DuoGPU troca de verdade. O que acontece:
        1.  O monitor apaga por uns 5 segundos enquanto a entrada e as saídas mudam.
        2.  Aparece no meio do monitor a janela "Você está vendo esta janela?". Clique em "Sim, manter".
        3.  Se a tela continuar preta, não mexa em nada: em 15 segundos ela volta sozinha.
        """);
    public static string PathHzWhy => T(
        "Then find the highest refresh rate this path can carry. Why: the driver can offer a rate that the cable or the port cannot carry (for example 120 Hz offered and a black screen above 60 Hz). DuoGPU starts at 60 Hz, which every cable carries, and goes up one step at a time while you confirm each one. At the first black screen it stops and keeps the last good rate, so the screen goes black at most once.",
        "Depois, ache o maior Hz que este caminho aguenta. Por quê: o driver pode oferecer um Hz que o cabo ou a porta não aguentam (por exemplo, 120 Hz oferecidos e tela preta acima de 60 Hz). O DuoGPU começa em 60 Hz, que todo cabo aguenta, e sobe um degrau por vez, com você confirmando cada um. Na primeira tela preta ele para e fica com o último Hz bom, então a tela fica preta no máximo uma vez.");
    public static string PathTime => T(
        "The switch: about 5 s. Each refresh step: up to 20 s. This step: about 1 to 2 minutes.",
        "A troca: uns 5 s. Cada degrau de Hz: até 20 s. Este passo: uns 1 a 2 minutos.");
    public static string SwitchNow(Mode mode) => T($"1.  Switch to {ModeShort(mode)}", $"1.  Trocar para o {ModeShort(mode)}");
    public static string AlreadyIn(Mode mode) => T($"1.  Already in {ModeShort(mode)}  ✓", $"1.  Já está no {ModeShort(mode)}  ✓");
    public static string FindHz => T("2.  Find the highest refresh", "2.  Achar o maior Hz");
    public static string FoundHz(int hz) => T($"2.  Highest refresh: {hz} Hz  ✓", $"2.  Maior Hz: {hz} Hz  ✓");
    public static string Trying(int hz) => T($"Trying {hz} Hz… confirm on the monitor if you see the window.",
        $"Testando {hz} Hz… confirme no monitor se estiver vendo a janela.");
    public static string SwitchFirst(Mode mode) => T($"Switch to {ModeShort(mode)} first.", $"Troque para o {ModeShort(mode)} primeiro.");
    public static string SkipHint => T("You can skip this step and run it later from the setup.", "Dá para pular este passo e fazer depois pela configuração.");

    // 7. done
    public static string DoneTitle => T("All set", "Tudo pronto");
    public static string DoneHow => T(
        """
        The DuoGPU icon lives in the tray, next to the clock (click ^ if it is hidden; drag it out to keep it visible):
        •  purple "AI" = AI mode   •  green gamepad = Game mode   •  amber "!" = something to fix

        Click the icon for the panel: the mode, the free VRAM, the checks and the one button that switches. Right-click for the menu. Every switch takes about 5 seconds and always asks for your confirmation.

        You can open this setup again from the panel ("Setup…").
        """,
        """
        O ícone do DuoGPU fica na bandeja, perto do relógio (clique na setinha ^ se ele estiver escondido; arraste para fora para deixar à vista):
        •  "IA" roxo = Modo IA   •  controle verde = Modo Jogo   •  "!" âmbar = algo para arrumar

        Clique no ícone para abrir o painel: o modo, a VRAM livre, as conferências e o botão único que troca. O botão direito abre o menu. Cada troca leva uns 5 segundos e sempre pede a sua confirmação.

        Dá para abrir esta configuração de novo pelo painel ("Configurar…").
        """);
    public static string Summary => T("What was saved", "O que ficou salvo");
    public static string NotTested => T("not tested yet (starts at 60 Hz)", "ainda não testado (começa em 60 Hz)");
    public static string ByHand => T("by hand", "à mão");
}
