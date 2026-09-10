using SoundboardMic.App.Services;

namespace SoundboardMic.Tests;

public class MyInstantsServiceTests
{
    // Trecho reduzido, mas fiel à estrutura real do HTML retornado pelo myinstants.com
    // (index/busca), com dois "instants" e ruído em volta para simular a página completa.
    private const string HtmlDeExemplo = """
        <div id="instants_container">
          <div class="instants result-page">
            <div class="instant">
              <div class="circle small-button-background" style="background-color:#FF0000;"></div>
              <button class="small-button" onclick="play('/media/sounds/psycho-scream-soundbible.mp3', 'loader-16558', 'jogo-do-botao')" title="Tocar o som de Jogo do botão" type="button"></button>
              <div id="loader-16558" class="loader"></div>
              <div class="small-button-shadow"></div>
              <a href="/pt/instant/jogo-do-botao/" class="instant-link link-secondary">Jogo do botão</a>
              <div class="result-page-instant-sharebox" style="margin-top: 8px;">
                <button type="button" class="instant-action-button" onclick="favorite('16558')" title="Adicionar 'Jogo do botão' aos favoritos"></button>
              </div>
            </div>
            <div class="instant">
              <div class="circle small-button-background" style="background-color:#FFD249;"></div>
              <button class="small-button" onclick="play('/media/sounds/pou-estourado_zIWCpMy.mp3', 'loader-402582', 'pou-estourado-48183')" title="Tocar o som de POU ESTOURADO" type="button"></button>
              <div id="loader-402582" class="loader"></div>
              <div class="small-button-shadow"></div>
              <a href="/pt/instant/pou-estourado-48183/" class="instant-link link-secondary">POU ESTOURADO &amp; Cia</a>
            </div>
          </div>
        </div>
        """;

    [Fact]
    public void ParseInstants_ExtraiTodosOsSons()
    {
        var sons = MyInstantsService.ParseInstants(HtmlDeExemplo);

        Assert.Equal(2, sons.Count);
    }

    [Fact]
    public void ParseInstants_PreencheNomeUrlAudioECor()
    {
        var sons = MyInstantsService.ParseInstants(HtmlDeExemplo);
        var primeiro = sons[0];

        Assert.Equal("Jogo do botão", primeiro.Nome);
        Assert.Equal("https://www.myinstants.com/media/sounds/psycho-scream-soundbible.mp3", primeiro.UrlAudio);
        Assert.Equal("https://www.myinstants.com/pt/instant/jogo-do-botao/", primeiro.UrlPagina);
        Assert.Equal("#FF0000", primeiro.CorHex);
    }

    [Fact]
    public void ParseInstants_DecodificaEntidadesHtmlNoNome()
    {
        var sons = MyInstantsService.ParseInstants(HtmlDeExemplo);
        var segundo = sons[1];

        Assert.Equal("POU ESTOURADO & Cia", segundo.Nome);
        Assert.Equal("#FFD249", segundo.CorHex);
    }

    [Fact]
    public void ParseInstants_HtmlVazio_RetornaListaVazia()
    {
        Assert.Empty(MyInstantsService.ParseInstants(""));
        Assert.Empty(MyInstantsService.ParseInstants("<html><body>nada aqui</body></html>"));
    }

    [Fact]
    public void ParseInstants_UrlJaAbsoluta_NaoDuplicaPrefixo()
    {
        const string html = """
            <div class="instant">
              <div class="circle small-button-background" style="background-color:#000000;"></div>
              <button class="small-button" onclick="play('https://cdn.example.com/som.mp3', 'loader-1', 'som-1')" title="x" type="button"></button>
              <a href="https://www.myinstants.com/pt/instant/som-1/" class="instant-link link-secondary">Som</a>
            </div>
            """;

        var sons = MyInstantsService.ParseInstants(html);

        Assert.Single(sons);
        Assert.Equal("https://cdn.example.com/som.mp3", sons[0].UrlAudio);
    }
}
