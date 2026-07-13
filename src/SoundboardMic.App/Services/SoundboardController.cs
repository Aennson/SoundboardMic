using System.IO;
using Serilog;
using SoundboardMic.Core.AudioEngine;
using SoundboardMic.Core.Hotkeys;
using SoundboardMic.Core.Models;
using SoundboardMic.Core.Repositories;

namespace SoundboardMic.App.Services;

/// <summary>Estado agregado exibido no cabeçalho de status da UI.</summary>
public class SoundboardStatus
{
    public bool EngineRunning { get; init; }
    public bool CableDetected { get; init; }
    public int ActiveSounds { get; init; }
    public string? OutputDeviceName { get; init; }
    public string? MicDeviceName { get; init; }
}

/// <summary>
/// Orquestra o motor de injeção, o hook global e os repositórios: liga hotkeys aos
/// áudios, aplica as configurações e expõe status/erros para a interface. É o único
/// ponto de contato entre a UI e o backend de áudio/teclado.
/// </summary>
public class SoundboardController : IDisposable
{
    private readonly IMicInjectionEngine _engine;
    private readonly IGlobalKeyboardHook _hook;
    private readonly IHotkeyDispatcher _dispatcher;
    private readonly IAudioDeviceService _devices;
    private readonly IAudioRepository _audioRepo;
    private readonly IMapeamentoRepository _mapeamentoRepo;
    private readonly ISettingsService _settings;

    private readonly object _lock = new();
    private bool _hookInstalled;

    /// <summary>Disparado quando o status muda (motor, sons ativos, dispositivo).</summary>
    public event EventHandler? StatusChanged;

    /// <summary>Mensagem de erro amigável para exibir na UI (device caiu, arquivo sumiu).</summary>
    public event EventHandler<string>? ErrorRaised;

    public SoundboardController(
        IMicInjectionEngine engine,
        IGlobalKeyboardHook hook,
        IHotkeyDispatcher dispatcher,
        IAudioDeviceService devices,
        IAudioRepository audioRepo,
        IMapeamentoRepository mapeamentoRepo,
        ISettingsService settings)
    {
        _engine = engine;
        _hook = hook;
        _dispatcher = dispatcher;
        _devices = devices;
        _audioRepo = audioRepo;
        _mapeamentoRepo = mapeamentoRepo;
        _settings = settings;

        _engine.ActiveSoundsChanged += (_, _) => OnStatusChanged();
        _engine.StoppedUnexpectedly += OnEngineStoppedUnexpectedly;
    }

    public bool IsEngineRunning => _engine.IsRunning;

    /// <summary>Instala o hook global (uma vez) — hotkeys funcionam mesmo com motor parado?</summary>
    public void EnsureHookInstalled()
    {
        lock (_lock)
        {
            if (_hookInstalled)
                return;
            _hook.Install();
            _hookInstalled = true;
            Log.Information("Hook global de teclado instalado.");
        }
    }

    /// <summary>Inicia o motor de injeção com as configurações atuais.</summary>
    public void StartEngine()
    {
        lock (_lock)
        {
            if (_engine.IsRunning)
                return;

            var s = _settings.Current;
            try
            {
                _engine.Start(new MicInjectionOptions
                {
                    MicDeviceId = s.MicDeviceId,
                    OutputDeviceId = s.OutputDeviceId ?? _devices.FindCableInput()?.Id,
                    MonitorDeviceId = s.MonitorDeviceId,
                    MonitorEnabled = s.MonitorEnabled,
                    MicVolume = s.MicVolume,
                    SoundboardVolume = s.SoundboardVolume,
                    NoiseSuppressionEnabled = s.NoiseSuppressionEnabled,
                    NoiseGateEnabled = s.NoiseGateEnabled,
                });
                Log.Information("Motor de injeção iniciado.");

                if (s.NoiseSuppressionEnabled && !_engine.NoiseSuppressionAvailable)
                    Log.Warning("Supressão de ruído pedida, mas a lib nativa do RNNoise " +
                                "não carregou; seguindo apenas com o noise gate.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Falha ao iniciar o motor de injeção.");
                RaiseError($"Não foi possível iniciar o motor de áudio: {ex.Message}");
            }
        }
        OnStatusChanged();
    }

    public void StopEngine()
    {
        lock (_lock)
            _engine.Stop();
        Log.Information("Motor de injeção parado.");
        OnStatusChanged();
    }

    /// <summary>Aplica volumes/monitor em tempo real a partir das configurações.</summary>
    public void ApplyRuntimeSettings()
    {
        var s = _settings.Current;
        _engine.MicVolume = s.MicVolume;
        _engine.SoundboardVolume = s.SoundboardVolume;
        _engine.NoiseSuppressionEnabled = s.NoiseSuppressionEnabled;
        _engine.NoiseGateEnabled = s.NoiseGateEnabled;
        if (_engine.IsRunning)
            _engine.MonitorEnabled = s.MonitorEnabled;
    }

    /// <summary>False se a lib nativa do RNNoise não carregou no último start do motor.</summary>
    public bool NoiseSuppressionAvailable => _engine.NoiseSuppressionAvailable;

    /// <summary>
    /// Reconstrói a tabela de hotkeys a partir dos mapeamentos ativos do banco.
    /// Chame após qualquer alteração de áudios/mapeamentos ou da tecla de pânico.
    /// </summary>
    public async Task ReloadBindingsAsync(CancellationToken ct = default)
    {
        var mapeamentos = await _mapeamentoRepo.GetAllAsync(ct);
        var audios = (await _audioRepo.GetAllAsync(ct)).ToDictionary(a => a.Id);

        var bindings = new List<HotkeyBinding>();
        foreach (var m in mapeamentos.Where(m => m.Ativo))
        {
            if (!audios.TryGetValue(m.AudioId, out var audio))
                continue;
            if (!KeyComboParser.TryParse(m.Teclas, out var combo, out _))
                continue;

            var path = audio.CaminhoArquivo;
            var volume = (float)audio.VolumePadrao;
            bindings.Add(new HotkeyBinding(combo!, () => TriggerSound(path, volume)));
        }

        _dispatcher.SetBindings(bindings);

        // Tecla de pânico.
        var panic = _settings.Current.PanicKey;
        if (!string.IsNullOrWhiteSpace(panic) && KeyComboParser.TryParse(panic, out var panicCombo, out _))
            _dispatcher.SetPanicKey(panicCombo, StopAllSounds);
        else
            _dispatcher.SetPanicKey(null, StopAllSounds);

        Log.Information("Hotkeys recarregadas: {Count} atalho(s) ativo(s).", bindings.Count);
    }

    /// <summary>Dispara um som imediatamente (usado pelo hook e pelo botão "play" da UI).</summary>
    public void TriggerSound(string filePath, float volume)
    {
        try
        {
            if (!_engine.IsRunning)
                StartEngine();
            _engine.PlaySound(filePath, volume);
        }
        catch (FileNotFoundException)
        {
            RaiseError($"Arquivo de áudio não encontrado: {filePath}");
        }
        catch (FormatoNaoSuportadoException)
        {
            RaiseError($"Formato de áudio não suportado: {filePath}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha ao disparar som {File}.", filePath);
            RaiseError($"Falha ao reproduzir o som: {ex.Message}");
        }
    }

    public void StopAllSounds() => _engine.StopAllSounds();

    /// <summary>Snapshot do status atual para a UI.</summary>
    public SoundboardStatus GetStatus()
    {
        var s = _settings.Current;
        var cable = _devices.FindCableInput();
        var outputName = ResolveDeviceName(_devices.GetOutputDevices(), s.OutputDeviceId ?? cable?.Id);
        var micName = ResolveDeviceName(_devices.GetInputDevices(), s.MicDeviceId);

        return new SoundboardStatus
        {
            EngineRunning = _engine.IsRunning,
            CableDetected = cable is not null,
            ActiveSounds = _engine.ActiveSoundCount,
            OutputDeviceName = outputName,
            MicDeviceName = micName,
        };
    }

    private static string? ResolveDeviceName(IReadOnlyList<AudioDeviceInfo> list, string? id)
    {
        if (id is null)
            return list.FirstOrDefault(d => d.PadraoDoSistema)?.Nome;
        return list.FirstOrDefault(d => d.Id == id)?.Nome;
    }

    private void OnEngineStoppedUnexpectedly(object? sender, EngineStoppedEventArgs e)
    {
        Log.Warning(e.Exception, "Motor parou inesperadamente.");
        RaiseError("O motor de áudio parou (dispositivo desconectado?). Verifique as configurações e reinicie.");
        OnStatusChanged();
    }

    private void OnStatusChanged() => StatusChanged?.Invoke(this, EventArgs.Empty);
    private void RaiseError(string message) => ErrorRaised?.Invoke(this, message);

    public void Dispose()
    {
        _engine.Dispose();
        _dispatcher.Dispose();
        _hook.Dispose();
        GC.SuppressFinalize(this);
    }
}
