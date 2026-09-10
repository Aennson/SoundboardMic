using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Serilog;

namespace SoundboardMic.App.Services;

/// <summary>
/// Implementação HTTP do <see cref="IMyInstantsService"/>: baixa as páginas públicas
/// do myinstants.com (sem API oficial) e extrai os sons por regex do HTML renderizado
/// no servidor. Todo o conteúdo de áudio pertence ao myinstants.com e seus autores.
/// </summary>
public class MyInstantsService : IMyInstantsService
{
    public const string BaseUrl = "https://www.myinstants.com";
    private const string PaginaEmAlta = "/pt/index/br/";

    private readonly HttpClient _http;

    public MyInstantsService(HttpClient http)
    {
        _http = http;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(BaseUrl);
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
            _http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) SoundboardMic/1.0");
    }

    public async Task<IReadOnlyList<MyInstantsSound>> BuscarAsync(string? termo, CancellationToken ct = default)
    {
        var path = string.IsNullOrWhiteSpace(termo)
            ? PaginaEmAlta
            : $"/pt/search/?name={Uri.EscapeDataString(termo.Trim())}";

        try
        {
            var html = await _http.GetStringAsync(path, ct);
            return ParseInstants(html);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "Falha ao consultar myinstants.com ({Path}).", path);
            throw new InvalidOperationException(
                "Não foi possível carregar os sons do myinstants.com. Verifique sua conexão e tente novamente.", ex);
        }
    }

    public async Task<byte[]> BaixarAudioAsync(MyInstantsSound som, CancellationToken ct = default)
    {
        try
        {
            return await _http.GetByteArrayAsync(som.UrlAudio, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "Falha ao baixar áudio do myinstants.com: {Url}", som.UrlAudio);
            throw new InvalidOperationException($"Não foi possível baixar \"{som.Nome}\".", ex);
        }
    }

    /// <summary>
    /// Extrai os sons ("instants") de uma página do myinstants.com. Público e estático
    /// para ser testável diretamente com HTML de exemplo, sem chamadas de rede.
    /// </summary>
    public static IReadOnlyList<MyInstantsSound> ParseInstants(string html)
    {
        var resultado = new List<MyInstantsSound>();
        if (string.IsNullOrEmpty(html))
            return resultado;

        foreach (Match m in InstantRegex.Matches(html))
        {
            var cor = m.Groups["cor"].Success ? m.Groups["cor"].Value : null;
            var audioPath = m.Groups["audio"].Value;
            var paginaPath = m.Groups["pagina"].Value;
            var nome = WebUtility.HtmlDecode(m.Groups["nome"].Value).Trim();

            if (string.IsNullOrEmpty(nome) || string.IsNullOrEmpty(audioPath))
                continue;

            resultado.Add(new MyInstantsSound(
                Nome: nome,
                UrlAudio: ResolverUrl(audioPath),
                UrlPagina: ResolverUrl(paginaPath),
                CorHex: cor));
        }
        return resultado;
    }

    private static string ResolverUrl(string path) =>
        path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : BaseUrl + path;

    // Casa cada bloco <div class="instant">…</div> da listagem/busca do myinstants.com,
    // capturando a cor do botão, a URL do áudio, a URL da página e o nome exibido.
    private static readonly Regex InstantRegex = new(
        """<div\s+class="instant">.*?<div\s+class="circle\s+small-button-background"\s+style="background-color:(?<cor>#[0-9A-Fa-f]{3,6});">.*?onclick="play\('(?<audio>[^']+)'.*?<a\s+href="(?<pagina>[^"]+)"\s+class="instant-link[^"]*">(?<nome>[^<]+)</a>""",
        RegexOptions.Compiled | RegexOptions.Singleline);
}
