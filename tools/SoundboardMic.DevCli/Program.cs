using System.Runtime.InteropServices;
using SoundboardMic.Core.AudioEngine;
using SoundboardMic.Core.Hotkeys;

// CLI de desenvolvimento para testar a camada de áudio sem a interface WPF.
//
// Uso:
//   devcli devices                                  → lista dispositivos de entrada/saída
//   devcli play <arquivo> [--device N] [--volume V] → toca arquivo (N = índice de "devices")

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var deviceService = new AudioDeviceService();

switch (args[0].ToLowerInvariant())
{
    case "devices":
        ListDevices(deviceService);
        return 0;

    case "play":
        return Play(deviceService, args);

    case "inject":
        return Inject(deviceService, args);

    case "hook":
        return HookTest();

    default:
        PrintUsage();
        return 1;
}

static void ListDevices(AudioDeviceService service)
{
    Console.WriteLine("=== Dispositivos de SAÍDA (render) ===");
    var outputs = service.GetOutputDevices();
    for (var i = 0; i < outputs.Count; i++)
        Console.WriteLine($"  [{i}] {outputs[i].Nome}{(outputs[i].PadraoDoSistema ? "  (padrão)" : "")}");

    Console.WriteLine();
    Console.WriteLine("=== Dispositivos de ENTRADA (capture) ===");
    var inputs = service.GetInputDevices();
    for (var i = 0; i < inputs.Count; i++)
        Console.WriteLine($"  [{i}] {inputs[i].Nome}{(inputs[i].PadraoDoSistema ? "  (padrão)" : "")}");

    Console.WriteLine();
    var cable = service.FindCableInput();
    Console.WriteLine(cable is not null
        ? $"VB-Cable detectado: {cable.Nome}"
        : "VB-Cable NÃO detectado (instale o VB-Audio Virtual Cable).");
}

static int Play(AudioDeviceService deviceService, string[] args)
{
    if (args.Length < 2)
    {
        PrintUsage();
        return 1;
    }

    var filePath = Path.GetFullPath(args[1]);
    string? deviceId = null;
    var volume = 1.0f;

    for (var i = 2; i < args.Length - 1; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--device" when int.TryParse(args[i + 1], out var index):
                var outputs = deviceService.GetOutputDevices();
                if (index < 0 || index >= outputs.Count)
                {
                    Console.Error.WriteLine($"Índice de dispositivo inválido: {index} (use 'devices').");
                    return 1;
                }
                deviceId = outputs[index].Id;
                Console.WriteLine($"Dispositivo: {outputs[index].Nome}");
                break;

            case "--volume" when float.TryParse(args[i + 1],
                System.Globalization.CultureInfo.InvariantCulture, out var v):
                volume = v;
                break;
        }
    }

    try
    {
        Console.WriteLine($"Duração: {AudioFileDecoder.GetDurationMs(filePath)} ms");

        using var playback = new PlaybackService();
        using var done = new ManualResetEventSlim();
        playback.PlaybackStopped += (_, _) => done.Set();

        playback.Play(filePath, deviceId, volume);
        Console.WriteLine($"Tocando \"{Path.GetFileName(filePath)}\" (volume {volume:0.0#}). Enter para parar...");

        // Termina quando o áudio acabar ou o usuário pressionar Enter.
        var enterThread = new Thread(() => { Console.ReadLine(); done.Set(); }) { IsBackground = true };
        enterThread.Start();
        done.Wait();

        playback.Stop();
        Console.WriteLine("Finalizado.");
        return 0;
    }
    catch (Exception ex) when (ex is FileNotFoundException
        or FormatoNaoSuportadoException
        or AudioDeviceNotFoundException)
    {
        Console.Error.WriteLine($"Erro: {ex.Message}");
        return 1;
    }
}

static int Inject(AudioDeviceService deviceService, string[] args)
{
    if (args.Length < 2)
    {
        PrintUsage();
        return 1;
    }

    var filePath = Path.GetFullPath(args[1]);
    var options = new MicInjectionOptions();
    var soundVolume = 1.0f;
    var outputs = deviceService.GetOutputDevices();
    var inputs = deviceService.GetInputDevices();

    for (var i = 2; i < args.Length; i++)
    {
        switch (args[i].ToLowerInvariant())
        {
            case "--mic" when i + 1 < args.Length && int.TryParse(args[i + 1], out var m)
                && m >= 0 && m < inputs.Count:
                options.MicDeviceId = inputs[m].Id;
                break;
            case "--output" when i + 1 < args.Length && int.TryParse(args[i + 1], out var o)
                && o >= 0 && o < outputs.Count:
                options.OutputDeviceId = outputs[o].Id;
                break;
            case "--volume" when i + 1 < args.Length && float.TryParse(args[i + 1],
                System.Globalization.CultureInfo.InvariantCulture, out var v):
                soundVolume = v;
                break;
            case "--monitor":
                options.MonitorEnabled = true;
                break;
        }
    }

    // Sem --output explícito, tenta o CABLE Input; senão avisa e usa o padrão.
    if (options.OutputDeviceId is null)
    {
        var cable = deviceService.FindCableInput();
        if (cable is not null)
        {
            options.OutputDeviceId = cable.Id;
            Console.WriteLine($"Saída: {cable.Nome}");
        }
        else
        {
            Console.WriteLine("VB-Cable não detectado — usando a saída PADRÃO do sistema " +
                              "(você ouvirá o próprio mic; use fones para evitar feedback!).");
        }
    }

    try
    {
        using var engine = new MicInjectionEngine();
        engine.StoppedUnexpectedly += (_, e) =>
            Console.Error.WriteLine($"\nMotor parou inesperadamente: {e.Exception?.Message}");

        engine.Start(options);
        Console.WriteLine($"""
            Motor rodando (mic {(options.MicDeviceId is null ? "padrão" : "selecionado")} → mix).
              [Enter]  toca "{Path.GetFileName(filePath)}"
              [s]      pânico (para todos os sons)
              [m]      liga/desliga monitor local
              [q]      sai
            """);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Q)
                break;
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    engine.PlaySound(filePath, soundVolume);
                    Console.WriteLine($"♪ tocando ({engine.ActiveSoundCount} ativo(s))");
                    break;
                case ConsoleKey.S:
                    engine.StopAllSounds();
                    Console.WriteLine("■ pânico: sons interrompidos");
                    break;
                case ConsoleKey.M:
                    engine.MonitorEnabled = !engine.MonitorEnabled;
                    Console.WriteLine($"monitor: {(engine.MonitorEnabled ? "ligado" : "desligado")}");
                    break;
            }
        }

        engine.Stop();
        Console.WriteLine("Motor parado.");
        return 0;
    }
    catch (Exception ex) when (ex is FileNotFoundException
        or FormatoNaoSuportadoException
        or AudioDeviceNotFoundException)
    {
        Console.Error.WriteLine($"Erro: {ex.Message}");
        return 1;
    }
}

static int HookTest()
{
    using var hook = new GlobalKeyboardHook();
    using var dispatcher = new HotkeyDispatcher(hook);

    // Alguns atalhos de exemplo; pânico em Ctrl+Alt+P.
    dispatcher.SetBindings(new[]
    {
        new HotkeyBinding(KeyComboParser.Parse("Ctrl+Alt+F1"),
            () => Console.WriteLine("  → Ctrl+Alt+F1 disparado")),
        new HotkeyBinding(KeyComboParser.Parse("Ctrl+Shift+3"),
            () => Console.WriteLine("  → Ctrl+Shift+3 disparado")),
    });
    dispatcher.SetPanicKey(KeyComboParser.Parse("Ctrl+Alt+P"),
        () => Console.WriteLine("  → PÂNICO (Ctrl+Alt+P)"));

    hook.Install();
    Console.WriteLine("""
        Hook global instalado. Pressione (mesmo com esta janela sem foco):
          Ctrl+Alt+F1   Ctrl+Shift+3   Ctrl+Alt+P (pânico)
        As combinações registradas são CONSUMIDAS (não chegam ao app em foco).
        Ctrl+C encerra.
        """);

    // WH_KEYBOARD_LL exige uma fila de mensagens sendo bombeada nesta thread.
    const uint WM_QUIT = 0x0012;
    var threadId = Native.GetCurrentThreadId();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        Native.PostThreadMessageW(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
    };

    while (Native.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
    {
        Native.TranslateMessage(ref msg);
        Native.DispatchMessage(ref msg);
    }

    Console.WriteLine("Hook removido.");
    return 0;
}

static void PrintUsage()
{
    Console.WriteLine("""
        SoundboardMic.DevCli — utilitário de teste da camada de áudio

        Comandos:
          devices                                   Lista dispositivos e detecta o VB-Cable
          play <arquivo> [--device N] [--volume V]  Reproduz .mp3/.wav/.ogg no dispositivo N
          inject <arquivo> [--mic N] [--output N] [--volume V] [--monitor]
                                                    Captura o mic, mixa e injeta no CABLE
                                                    Input (ou --output N). Enter=toca,
                                                    s=pânico, m=monitor, q=sai
          hook                                      Testa o hook global: Ctrl+Alt+F1,
                                                    Ctrl+Shift+3, Ctrl+Alt+P (pânico)
        """);
}

// ---- P/Invoke do message loop (necessário para o WH_KEYBOARD_LL no console) ----

[StructLayout(LayoutKind.Sequential)]
struct MSG
{
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public int ptX;
    public int ptY;
}

static class Native
{
    [DllImport("user32.dll")]
    public static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern bool PostThreadMessageW(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
}
