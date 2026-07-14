using Microsoft.Extensions.DependencyInjection;
using SoundboardMic.App.Views;

namespace SoundboardMic.App.Services;

/// <summary>
/// Controla a visibilidade da barra rápida e mantém tray/Configurações em
/// sincronia. Resolve a janela de forma lazy para evitar ciclo no DI
/// (ViewModel → serviço → janela → ViewModel).
/// </summary>
public class QuickBarService
{
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private QuickBarWindow? _window;

    public QuickBarService(IServiceProvider services, ISettingsService settings)
    {
        _services = services;
        _settings = settings;
    }

    /// <summary>Disparado após mostrar/ocultar, com o novo estado.</summary>
    public event EventHandler<bool>? VisibilidadeAlterada;

    public bool Visivel => _settings.Current.QuickBarVisible;

    /// <summary>Abre a barra no startup se ela estava visível na última sessão.</summary>
    public void AplicarEstadoInicial()
    {
        if (_settings.Current.QuickBarVisible)
            Janela().Show();
    }

    public void Mostrar()
    {
        if (!_settings.Current.QuickBarVisible)
        {
            _settings.Current.QuickBarVisible = true;
            _settings.Save();
        }
        Janela().Show();
        VisibilidadeAlterada?.Invoke(this, true);
    }

    public void Ocultar()
    {
        if (_settings.Current.QuickBarVisible)
        {
            _settings.Current.QuickBarVisible = false;
            _settings.Save();
        }
        _window?.Hide();
        VisibilidadeAlterada?.Invoke(this, false);
    }

    public void Alternar()
    {
        if (Visivel)
            Ocultar();
        else
            Mostrar();
    }

    private QuickBarWindow Janela() =>
        _window ??= _services.GetRequiredService<QuickBarWindow>();
}
