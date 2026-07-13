using NAudio.Wave;
using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.Tests;

public class AudioFileDecoderTests : IDisposable
{
    private readonly string _dir;

    public AudioFileDecoderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"soundboardmic-decoder-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>Gera um WAV PCM 16-bit mono de silêncio com a duração pedida.</summary>
    private string CreateWav(int durationMs, int sampleRate = 44100)
    {
        var path = Path.Combine(_dir, $"{Guid.NewGuid():N}.wav");
        var format = new WaveFormat(sampleRate, 16, 1);
        using var writer = new WaveFileWriter(path, format);
        var bytes = new byte[format.AverageBytesPerSecond * durationMs / 1000];
        writer.Write(bytes, 0, bytes.Length);
        return path;
    }

    [Theory]
    [InlineData("som.mp3", true)]
    [InlineData("som.WAV", true)]
    [InlineData("som.Ogg", true)]
    [InlineData("som.flac", false)]
    [InlineData("som.txt", false)]
    [InlineData("som", false)]
    public void IsSupported_ChecaExtensao(string nome, bool esperado)
    {
        Assert.Equal(esperado, AudioFileDecoder.IsSupported(nome));
    }

    [Fact]
    public void OpenRead_ArquivoInexistente_LancaFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(
            () => AudioFileDecoder.OpenRead(Path.Combine(_dir, "nao-existe.wav")));
    }

    [Fact]
    public void OpenRead_ExtensaoNaoSuportada_Lanca()
    {
        var path = Path.Combine(_dir, "dados.txt");
        File.WriteAllText(path, "não é áudio");

        var ex = Assert.Throws<FormatoNaoSuportadoException>(() => AudioFileDecoder.OpenRead(path));
        Assert.Equal(path, ex.CaminhoArquivo);
    }

    [Fact]
    public void OpenRead_ConteudoInvalidoComExtensaoValida_Lanca()
    {
        var path = Path.Combine(_dir, "falso.wav");
        File.WriteAllText(path, "isto não é um WAV de verdade");

        Assert.Throws<FormatoNaoSuportadoException>(() => AudioFileDecoder.OpenRead(path));
    }

    [Fact]
    public void OpenRead_WavValido_RetornaStreamDecodificado()
    {
        var path = CreateWav(durationMs: 500);

        using var stream = AudioFileDecoder.OpenRead(path);
        var sampleProvider = AudioFileDecoder.ToSampleProvider(stream);

        // AudioFileReader converte para float 32-bit (IEEE).
        Assert.Equal(WaveFormatEncoding.IeeeFloat, sampleProvider.WaveFormat.Encoding);
        Assert.Equal(44100, sampleProvider.WaveFormat.SampleRate);
    }

    [Theory]
    [InlineData(500)]
    [InlineData(2000)]
    public void GetDurationMs_RetornaDuracaoAproximada(int durationMs)
    {
        var path = CreateWav(durationMs);

        var duracao = AudioFileDecoder.GetDurationMs(path);

        // Tolerância de 10 ms para arredondamento de blocos.
        Assert.InRange(duracao, durationMs - 10, durationMs + 10);
    }
}
