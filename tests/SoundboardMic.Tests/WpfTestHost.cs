using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SoundboardMic.Tests;

/// <summary>
/// Uma única thread STA com um <see cref="Application"/> carregando os temas do app, como em
/// produção. WPF só permite um Application por processo, então todos os testes de layout
/// compartilham este host.
/// </summary>
internal static class WpfTestHost
{
    private static readonly Lazy<Dispatcher> Host = new(Start);

    public static void Run(Action action) => Host.Value.Invoke(action);

    /// <summary>Roda código assíncrono na thread do host: os awaits voltam para o dispatcher.</summary>
    public static Task RunAsync(Func<Task> action) => Host.Value.InvokeAsync(action).Task.Unwrap();

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            foreach (var name in new[] { "Converters", "Palette", "Controls" })
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/SoundboardMic.App;component/Themes/{name}.xaml")
                });
            application.Resources["AppFont"] = new FontFamily("Segoe UI Variable, Segoe UI");
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    }
}
