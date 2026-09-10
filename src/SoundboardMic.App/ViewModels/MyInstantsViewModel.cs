using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SoundboardMic.App.Services;
using SoundboardMic.Core.AudioEngine;
using SoundboardMic.Core.Models;
using SoundboardMic.Core.Repositories;

namespace SoundboardMic.App.ViewModels;

/// <summary>
/// ViewModel da aba "Sons da web": lista/busca sons públicos do myinstants.com,
/// permite tocar cada um em preview (sem adicionar) e importá-lo para o acervo
/// pessoal do usuário (banco local). Todo o conteúdo listado aqui pertence ao
/// myinstants.com — a atribuição é exibida na própria view.
/// </summary>
public partial class MyInstantsViewModel : ObservableObject
{
    private readonly IMyInstantsService _service;
    private readonly IAudioRepository _audioRepo;
    private readonly AudioFileCache _fileCache;
    private readonly IPlaybackService _player;

    private static readonly string PreviewDir = Path.Combine(Path.GetTempPath(), "SoundboardMic_MyInstantsPreview");

    // Evita rebaixar o mesmo som repetidas vezes numa mesma sessão (tocar → adicionar).
    private readonly Dictionary<string, byte[]> _conteudoBaixado = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _arquivoPreviaLocal = new(StringComparer.OrdinalIgnoreCase);

    private MyInstantsSoundViewModel? _tocandoAtual;

    public MyInstantsViewModel(
        IMyInstantsService service,
        IAudioRepository audioRepo,
        AudioFileCache fileCache,
        IPlaybackService player)
    {
        _service = service;
        _audioRepo = audioRepo;
        _fileCache = fileCache;
        _player = player;

        _player.PlaybackStopped += (_, _) =>
        {
            if (_tocandoAtual is not null)
            {
                _tocandoAtual.Tocando = false;
                _tocandoAtual = null;
            }
        };
    }

    public ObservableCollection<MyInstantsSoundViewModel> Sons { get; } = new();

    /// <summary>Disparado após adicionar um som com sucesso, para o "Meus sons" recarregar.</summary>
    public event EventHandler? SomAdicionado;

    [ObservableProperty] private string _termo = string.Empty;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string? _erro;
    [ObservableProperty] private bool _listaVazia;
    [ObservableProperty] private bool _carregadoAlgumaVez;

    /// <summary>Carrega a lista "em alta no Brasil" na primeira vez que a aba é aberta.</summary>
    public async Task CarregarInicialAsync()
    {
        if (CarregadoAlgumaVez) return;
        await BuscarInternoAsync(null);
    }

    [RelayCommand]
    private async Task Buscar() => await BuscarInternoAsync(Termo);

    [RelayCommand]
    private async Task LimparBusca()
    {
        Termo = string.Empty;
        await BuscarInternoAsync(null);
    }

    private async Task BuscarInternoAsync(string? termo)
    {
        Erro = null;
        Carregando = true;
        _player.Stop();
        try
        {
            var resultados = await _service.BuscarAsync(termo);
            Sons.Clear();
            foreach (var som in resultados)
                Sons.Add(new MyInstantsSoundViewModel(som));
            ListaVazia = Sons.Count == 0;
            CarregadoAlgumaVez = true;
        }
        catch (Exception ex)
        {
            Erro = ex.Message;
            ListaVazia = Sons.Count == 0;
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private async Task Tocar(MyInstantsSoundViewModel? item)
    {
        if (item is null || item.Ocupado) return;

        // Clicar de novo no que já está tocando apenas para.
        if (ReferenceEquals(_tocandoAtual, item))
        {
            _player.Stop();
            item.Tocando = false;
            _tocandoAtual = null;
            return;
        }

        if (_tocandoAtual is not null)
            _tocandoAtual.Tocando = false;

        item.Ocupado = true;
        Erro = null;
        try
        {
            var caminho = await ObterArquivoPreviaAsync(item.Som);
            _player.Play(caminho);
            item.Tocando = true;
            _tocandoAtual = item;
        }
        catch (Exception ex)
        {
            Erro = $"Não foi possível tocar \"{item.Som.Nome}\": {ex.Message}";
        }
        finally
        {
            item.Ocupado = false;
        }
    }

    [RelayCommand]
    private async Task Adicionar(MyInstantsSoundViewModel? item)
    {
        if (item is null || item.Ocupado || item.Adicionado) return;

        item.Ocupado = true;
        Erro = null;
        try
        {
            var conteudo = await ObterConteudoAsync(item.Som);
            var nomeSugerido = NomeArquivoDaUrl(item.Som.UrlAudio);
            var (caminhoCache, bytesGravados, nomeOriginal) =
                await _fileCache.ImportarBytesAsync(conteudo, nomeSugerido);

            long duracaoMs;
            try { duracaoMs = AudioFileDecoder.GetDurationMs(caminhoCache); }
            catch { duracaoMs = 0; }

            var audio = new Audio
            {
                Nome = item.Som.Nome,
                CaminhoArquivo = caminhoCache,
                ArquivoConteudo = bytesGravados,
                ArquivoNomeOriginal = nomeOriginal,
                DuracaoMs = duracaoMs,
                VolumePadrao = 1.0,
                Icone = IconCatalog.GlifoPadrao,
                Cor = NormalizarCor(item.Som.CorHex),
            };

            await _audioRepo.AddAsync(audio);
            item.Adicionado = true;
            SomAdicionado?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Erro = $"Falha ao adicionar \"{item.Som.Nome}\": {ex.Message}";
        }
        finally
        {
            item.Ocupado = false;
        }
    }

    private async Task<byte[]> ObterConteudoAsync(MyInstantsSound som)
    {
        if (_conteudoBaixado.TryGetValue(som.UrlAudio, out var cache))
            return cache;

        var bytes = await _service.BaixarAudioAsync(som);
        _conteudoBaixado[som.UrlAudio] = bytes;
        return bytes;
    }

    private async Task<string> ObterArquivoPreviaAsync(MyInstantsSound som)
    {
        if (_arquivoPreviaLocal.TryGetValue(som.UrlAudio, out var caminhoExistente) && File.Exists(caminhoExistente))
            return caminhoExistente;

        var bytes = await ObterConteudoAsync(som);
        Directory.CreateDirectory(PreviewDir);
        var extensao = Path.GetExtension(NomeArquivoDaUrl(som.UrlAudio));
        if (string.IsNullOrEmpty(extensao)) extensao = ".mp3";
        var caminho = Path.Combine(PreviewDir, $"{Guid.NewGuid():N}{extensao}");
        await File.WriteAllBytesAsync(caminho, bytes);
        _arquivoPreviaLocal[som.UrlAudio] = caminho;
        return caminho;
    }

    private static string NomeArquivoDaUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? Path.GetFileName(uri.LocalPath) : "som.mp3";

    /// <summary>Normaliza "#RGB" para "#RRGGBB" (o myinstants às vezes usa a forma curta).</summary>
    private static string? NormalizarCor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        if (hex.Length == 4 && hex[0] == '#')
            return $"#{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}".ToUpperInvariant();
        return hex.ToUpperInvariant();
    }

    /// <summary>Chamado ao sair da aba: interrompe qualquer preview em andamento.</summary>
    public void PararPreview() => _player.Stop();
}
