namespace SoundboardMic.App.Services;

/// <summary>
/// Um som listado no myinstants.com (não persistido — apenas exibido/tocado a partir
/// da lista ao vivo do site, até que o usuário decida adicioná-lo ao próprio acervo).
/// </summary>
/// <param name="Nome">Nome amigável exibido no site.</param>
/// <param name="UrlAudio">URL absoluta do arquivo de áudio (.mp3).</param>
/// <param name="UrlPagina">URL absoluta da página do som no myinstants.com.</param>
/// <param name="CorHex">Cor do botão original no site ("#RRGGBB"), usada no card e ao importar.</param>
public sealed record MyInstantsSound(string Nome, string UrlAudio, string UrlPagina, string? CorHex);
