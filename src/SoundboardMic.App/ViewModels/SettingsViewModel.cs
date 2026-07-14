using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundboardMic.App.Services;
using SoundboardMic.Core.AudioEngine;
using SoundboardMic.Core.Hotkeys;

namespace SoundboardMic.App.ViewModels;

/// <summary>
/// Configurações: dispositivos, volumes, monitoramento, tecla de pânico e integração
/// com o Windows. Salva e aplica em tempo real a cada alteração.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IAudioDeviceService _devices;
    private readonly IStartupService _startup;
    private readonly SoundboardController _controller;
    private readonly QuickBarService _quickBar;

    private bool _loading;

    public SettingsViewModel(
        ISettingsService settings,
        IAudioDeviceService devices,
        IStartupService startup,
        SoundboardController controller,
        QuickBarService quickBar)
    {
        _settings = settings;
        _devices = devices;
        _startup = startup;
        _controller = controller;
        _quickBar = quickBar;
        RefreshDevices();
        LoadFromSettings();

        // Reflete toggles vindos da tray ou do menu da própria barra.
        _quickBar.VisibilidadeAlterada += (_, visivel) =>
        {
            if (QuickBarVisible != visivel)
            {
                _loading = true;
                QuickBarVisible = visivel;
                _loading = false;
            }
        };
    }

    public ObservableCollection<DeviceOption> MicDevices { get; } = new();
    public ObservableCollection<DeviceOption> OutputDevices { get; } = new();
    public ObservableCollection<DeviceOption> MonitorDevices { get; } = new();

    [ObservableProperty] private DeviceOption? _selectedMic;
    [ObservableProperty] private DeviceOption? _selectedOutput;
    [ObservableProperty] private DeviceOption? _selectedMonitor;

    [ObservableProperty] private double _micVolume = 1.0;
    [ObservableProperty] private double _soundboardVolume = 1.0;
    [ObservableProperty] private bool _monitorEnabled;
    [ObservableProperty] private bool _noiseSuppressionEnabled;
    [ObservableProperty] private bool _noiseGateEnabled;
    [ObservableProperty] private bool _noiseSuppressionAvailable = true;
    [ObservableProperty] private bool _autoStartEngine = true;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _minimizeToTray = true;
    [ObservableProperty] private bool _quickBarVisible;

    [ObservableProperty] private string _panicKey = string.Empty;
    [ObservableProperty] private bool _gravandoPanico;

    [ObservableProperty] private bool _cableDetected;
    [ObservableProperty] private string? _cableNome;

    private void LoadFromSettings()
    {
        _loading = true;
        var s = _settings.Current;

        SelectedMic = MicDevices.FirstOrDefault(d => d.Id == s.MicDeviceId) ?? MicDevices.FirstOrDefault();
        SelectedOutput = OutputDevices.FirstOrDefault(d => d.Id == s.OutputDeviceId) ?? OutputDevices.FirstOrDefault();
        SelectedMonitor = MonitorDevices.FirstOrDefault(d => d.Id == s.MonitorDeviceId) ?? MonitorDevices.FirstOrDefault();

        MicVolume = s.MicVolume;
        SoundboardVolume = s.SoundboardVolume;
        MonitorEnabled = s.MonitorEnabled;
        NoiseSuppressionEnabled = s.NoiseSuppressionEnabled;
        NoiseGateEnabled = s.NoiseGateEnabled;
        NoiseSuppressionAvailable = _controller.NoiseSuppressionAvailable;
        AutoStartEngine = s.AutoStartEngine;
        MinimizeToTray = s.MinimizeToTray;
        QuickBarVisible = s.QuickBarVisible;
        PanicKey = s.PanicKey ?? string.Empty;
        StartWithWindows = _startup.IsEnabled();

        _loading = false;
    }

    [RelayCommand]
    private void RefreshDevices()
    {
        MicDevices.Clear();
        MicDevices.Add(DeviceOption.Default("microfone"));
        foreach (var d in _devices.GetInputDevices())
            MicDevices.Add(DeviceOption.From(d));

        OutputDevices.Clear();
        OutputDevices.Add(DeviceOption.Default("saída"));
        foreach (var d in _devices.GetOutputDevices())
            OutputDevices.Add(DeviceOption.From(d));

        MonitorDevices.Clear();
        MonitorDevices.Add(DeviceOption.Default("fones"));
        foreach (var d in _devices.GetOutputDevices())
            MonitorDevices.Add(DeviceOption.From(d));

        var cable = _devices.FindCableInput();
        CableDetected = cable is not null;
        CableNome = cable?.Nome;
    }

    // ---- Persistência reativa ----

    partial void OnSelectedMicChanged(DeviceOption? value) => Persist(s => s.MicDeviceId = value?.Id);
    partial void OnSelectedOutputChanged(DeviceOption? value) => Persist(s => s.OutputDeviceId = value?.Id);
    partial void OnSelectedMonitorChanged(DeviceOption? value) => Persist(s => s.MonitorDeviceId = value?.Id);
    partial void OnMonitorEnabledChanged(bool value)
    {
        Persist(s => s.MonitorEnabled = value);
        _controller.ApplyRuntimeSettings();
    }
    partial void OnNoiseSuppressionEnabledChanged(bool value)
    {
        Persist(s => s.NoiseSuppressionEnabled = value);
        _controller.ApplyRuntimeSettings();
    }
    partial void OnNoiseGateEnabledChanged(bool value)
    {
        Persist(s => s.NoiseGateEnabled = value);
        _controller.ApplyRuntimeSettings();
    }
    partial void OnAutoStartEngineChanged(bool value) => Persist(s => s.AutoStartEngine = value);
    partial void OnMinimizeToTrayChanged(bool value) => Persist(s => s.MinimizeToTray = value);

    partial void OnQuickBarVisibleChanged(bool value)
    {
        if (_loading) return;
        // O serviço persiste e mostra/oculta a janela.
        if (value)
            _quickBar.Mostrar();
        else
            _quickBar.Ocultar();
    }

    partial void OnMicVolumeChanged(double value)
    {
        Persist(s => s.MicVolume = (float)value);
        _controller.ApplyRuntimeSettings();
    }

    partial void OnSoundboardVolumeChanged(double value)
    {
        Persist(s => s.SoundboardVolume = (float)value);
        _controller.ApplyRuntimeSettings();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading) return;
        _startup.SetEnabled(value);
        Persist(s => s.StartWithWindows = value);
    }

    [RelayCommand]
    private void ToggleGravacaoPanico()
    {
        GravandoPanico = !GravandoPanico;
    }

    [RelayCommand]
    private async Task LimparPanico()
    {
        PanicKey = string.Empty;
        GravandoPanico = false;
        Persist(s => s.PanicKey = null);
        await _controller.ReloadBindingsAsync();
    }

    /// <summary>Recebe o combo capturado para a tecla de pânico.</summary>
    public async Task<bool> AplicarPanicoCapturado(KeyCombo? combo)
    {
        if (combo is null)
            return false;
        PanicKey = combo.ToString();
        GravandoPanico = false;
        Persist(s => s.PanicKey = PanicKey);
        await _controller.ReloadBindingsAsync();
        return true;
    }

    private void Persist(Action<AppSettings> mutate)
    {
        if (_loading) return;
        mutate(_settings.Current);
        _settings.Save();
    }
}
