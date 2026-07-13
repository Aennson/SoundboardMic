using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SoundboardMic.App.Services;
using SoundboardMic.App.ViewModels;
using SoundboardMic.Core.AudioEngine;
using SoundboardMic.Core.Hotkeys;
using SoundboardMic.Core.Repositories;
using SoundboardMic.Data;

namespace SoundboardMic.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    public IServiceProvider Services =>
        _services ?? throw new InvalidOperationException("Serviços não inicializados.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SoundboardMic");
        Directory.CreateDirectory(appDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(appDir, "logs", "soundboard-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();

        Log.Information("=== SoundboardMic iniciando ===");

        DispatcherUnhandledException += OnUnhandledException;

        try
        {
            _services = ConfigureServices();

            // Bootstrap do banco.
            await _services.GetRequiredService<DatabaseBootstrapper>().InitializeAsync();

            var window = _services.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();

            await window.ViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Falha fatal na inicialização.");
            MessageBox.Show($"Erro ao iniciar o SoundboardMic:\n\n{ex.Message}",
                "SoundboardMic", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Dados
        var dbPath = SqliteConnectionFactory.DefaultDatabasePath();
        services.AddSingleton(new SqliteConnectionFactory(dbPath));
        services.AddSingleton<DatabaseBootstrapper>();
        services.AddSingleton<IAudioRepository, AudioRepository>();
        services.AddSingleton<IMapeamentoRepository, MapeamentoRepository>();

        // Áudio / teclado
        services.AddSingleton<IAudioDeviceService, AudioDeviceService>();
        services.AddSingleton<IMicInjectionEngine, MicInjectionEngine>();
        services.AddSingleton<IGlobalKeyboardHook, GlobalKeyboardHook>();
        services.AddSingleton<IHotkeyDispatcher, HotkeyDispatcher>();
        services.AddTransient<IPlaybackService, PlaybackService>();

        // App
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<SoundboardController>();

        // ViewModels + janelas
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Exceção não tratada na UI.");
        MessageBox.Show($"Ocorreu um erro inesperado:\n\n{e.Exception.Message}",
            "SoundboardMic", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("=== SoundboardMic encerrando ===");
        (_services?.GetService<SoundboardController>())?.Dispose();
        _services?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
