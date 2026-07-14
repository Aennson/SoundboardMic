using SoundboardMic.App.Services;
using SoundboardMic.Core.AudioEngine;
using SoundboardMic.Core.Hotkeys;
using SoundboardMic.Core.Models;
using SoundboardMic.Data;

namespace SoundboardMic.Tests;

/// <summary>
/// Testes de integração do orquestrador: verifica que os mapeamentos do banco viram
/// bindings de hotkey que disparam o motor, e que a tecla de pânico das configurações
/// interrompe os sons. Usa repositórios reais (SQLite temporário), dispatcher real com
/// um hook falso, e motor/dispositivos falsos.
/// </summary>
public class SoundboardControllerTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly AudioRepository _audioRepo;
    private readonly MapeamentoRepository _mapeamentoRepo;
    private readonly FakeHook _hook = new();
    private readonly HotkeyDispatcher _dispatcher;
    private readonly FakeEngine _engine = new();
    private readonly FakeSettings _settings = new();
    private readonly SoundboardController _controller;

    public SoundboardControllerTests()
    {
        _audioRepo = new AudioRepository(_db.Factory);
        _mapeamentoRepo = new MapeamentoRepository(_db.Factory);
        _dispatcher = new HotkeyDispatcher(_hook);
        _controller = new SoundboardController(
            _engine, _hook, _dispatcher, new FakeDevices(),
            _audioRepo, _mapeamentoRepo, _settings);
    }

    public void Dispose()
    {
        _controller.Dispose();
        _db.Dispose();
    }

    /// <summary>
    /// As ações do dispatcher rodam no ThreadPool; espera até a condição valer ou dar timeout.
    /// </summary>
    private static bool WaitFor(Func<bool> condition, int timeoutMs = 2000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return true;
            Thread.Sleep(10);
        }
        return condition();
    }

    private async Task<Audio> SeedAsync(string nome, string caminho, double volume, string? teclas)
    {
        var audio = await _audioRepo.AddAsync(new Audio
        {
            Nome = nome,
            CaminhoArquivo = caminho,
            VolumePadrao = volume,
        });
        if (teclas is not null)
            await _mapeamentoRepo.AddAsync(new Mapeamento { AudioId = audio.Id, Teclas = teclas });
        return audio;
    }

    [Fact]
    public async Task ReloadBindings_MapeamentoAtivo_DisparaOMotorComOArquivoEVolume()
    {
        _engine.Running = true;
        await SeedAsync("Buzina", @"C:\sons\buzina.mp3", 0.7, "Ctrl+Alt+F1");

        await _controller.ReloadBindingsAsync();
        _hook.Press("Ctrl+Alt+F1");

        Assert.True(WaitFor(() => _engine.Played.Count == 1));
        Assert.Equal(@"C:\sons\buzina.mp3", _engine.Played[0].Path);
        Assert.Equal(0.7f, _engine.Played[0].Volume, precision: 4);
    }

    [Fact]
    public async Task ReloadBindings_MapeamentoInativo_NaoRegistraBinding()
    {
        _engine.Running = true;
        var audio = await _audioRepo.AddAsync(new Audio { Nome = "X", CaminhoArquivo = @"C:\x.wav" });
        await _mapeamentoRepo.AddAsync(new Mapeamento { AudioId = audio.Id, Teclas = "Ctrl+1", Ativo = false });

        await _controller.ReloadBindingsAsync();

        Assert.False(_hook.Press("Ctrl+1"));
        Assert.Empty(_engine.Played);
    }

    [Fact]
    public async Task ReloadBindings_TeclaDePanico_ParaTodosOsSons()
    {
        _settings.Current.PanicKey = "Ctrl+Alt+P";
        await _controller.ReloadBindingsAsync();

        var handled = _hook.Press("Ctrl+Alt+P");

        Assert.True(handled);
        Assert.True(WaitFor(() => _engine.StopAllCount == 1));
    }

    [Fact]
    public async Task ReloadBindings_SemTeclaDePanico_NaoRegistraPanico()
    {
        _settings.Current.PanicKey = null;
        await _controller.ReloadBindingsAsync();

        Assert.False(_hook.Press("Ctrl+Alt+P"));
        Assert.Equal(0, _engine.StopAllCount);
    }

    [Fact]
    public async Task ReloadBindings_AposExcluirAudio_RemoveOBinding()
    {
        _engine.Running = true;
        var audio = await SeedAsync("Tada", @"C:\tada.wav", 1.0, "Ctrl+2");
        await _controller.ReloadBindingsAsync();
        Assert.True(_hook.Press("Ctrl+2"));

        await _audioRepo.DeleteAsync(audio.Id); // cascade remove o mapeamento
        await _controller.ReloadBindingsAsync();

        _engine.Played.Clear();
        Assert.False(_hook.Press("Ctrl+2"));
        Assert.Empty(_engine.Played);
    }

    [Fact]
    public void GetStatus_ComSonsTocando_ExpoeOsCaminhos()
    {
        _engine.PlaySound(@"C:\sons\a.mp3");
        _engine.PlaySound(@"C:\sons\b.mp3");
        _engine.PlaySound(@"C:\sons\a.mp3"); // repetido não duplica

        var status = _controller.GetStatus();

        Assert.Equal(2, status.ActiveSoundPaths.Count);
        Assert.Contains(@"C:\sons\a.mp3", status.ActiveSoundPaths);
        Assert.Contains(@"C:\sons\b.mp3", status.ActiveSoundPaths);
    }

    [Fact]
    public void GetStatus_AposStopAllSounds_ListaVazia()
    {
        _engine.PlaySound(@"C:\sons\a.mp3");
        _controller.StopAllSounds();

        Assert.Empty(_controller.GetStatus().ActiveSoundPaths);
    }

    [Fact]
    public void StartEngine_PassaTogglesDeRuidoDoSettingsNasOptions()
    {
        _settings.Current.NoiseSuppressionEnabled = true;
        _settings.Current.NoiseGateEnabled = true;

        _controller.StartEngine();

        Assert.NotNull(_engine.LastStartOptions);
        Assert.True(_engine.LastStartOptions!.NoiseSuppressionEnabled);
        Assert.True(_engine.LastStartOptions.NoiseGateEnabled);
    }

    [Fact]
    public void ApplyRuntimeSettings_EmpurraTogglesDeRuidoParaOEngine()
    {
        _settings.Current.NoiseSuppressionEnabled = true;
        _settings.Current.NoiseGateEnabled = true;

        _controller.ApplyRuntimeSettings();

        Assert.True(_engine.NoiseSuppressionEnabled);
        Assert.True(_engine.NoiseGateEnabled);
    }

    // ---- Fakes ----

    private sealed class FakeHook : IGlobalKeyboardHook
    {
        public bool IsInstalled { get; private set; }
        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
        public void Install() => IsInstalled = true;
        public void Uninstall() => IsInstalled = false;
        public void Dispose() { }

        public bool Press(string combo)
        {
            var args = new HotkeyPressedEventArgs { Combo = KeyComboParser.Parse(combo) };
            HotkeyPressed?.Invoke(this, args);
            return args.Handled;
        }
    }

    private sealed class FakeEngine : IMicInjectionEngine
    {
        public record Sound(string Path, float Volume);
        public List<Sound> Played { get; } = new();
        public int StopAllCount { get; private set; }

        public bool Running { get; set; }
        public bool IsRunning => Running;
        public int ActiveSoundCount => Played.Count;

        public IReadOnlyList<string> GetActiveSoundPaths() =>
            Played.Select(p => p.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        public float MicVolume { get; set; } = 1f;
        public float SoundboardVolume { get; set; } = 1f;
        public bool MonitorEnabled { get; set; }
        public bool NoiseSuppressionEnabled { get; set; }
        public bool NoiseGateEnabled { get; set; }
        public bool NoiseSuppressionAvailable { get; set; } = true;
        public MicInjectionOptions? LastStartOptions { get; private set; }

        public event EventHandler? ActiveSoundsChanged;
        public event EventHandler<EngineStoppedEventArgs>? StoppedUnexpectedly;

        public void Start(MicInjectionOptions options)
        {
            LastStartOptions = options;
            Running = true;
        }
        public void Stop() => Running = false;

        public void PlaySound(string filePath, float volume = 1f)
        {
            Played.Add(new Sound(filePath, volume));
            ActiveSoundsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void StopAllSounds()
        {
            StopAllCount++;
            Played.Clear();
        }

        public void Dispose()
        {
            _ = StoppedUnexpectedly; // silencia aviso de evento não usado
        }
    }

    private sealed class FakeDevices : IAudioDeviceService
    {
        public IReadOnlyList<AudioDeviceInfo> GetOutputDevices() => Array.Empty<AudioDeviceInfo>();
        public IReadOnlyList<AudioDeviceInfo> GetInputDevices() => Array.Empty<AudioDeviceInfo>();
        public AudioDeviceInfo? FindCableInput() => null;
    }

    private sealed class FakeSettings : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public void Save() { }
    }
}
