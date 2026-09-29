using NAudio.MediaFoundation;
using NAudio.Wave;
using SoundboardMic.Core.AudioEngine;

namespace SoundboardMic.Tests;

public class AudioTrimmerTests : IDisposable
{
    private readonly string _dir;

    public AudioTrimmerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"soundboardmic-trim-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>WAV PCM16 com amplitude constante 0.5 (facilita checar fade e ganho).</summary>
    private string CriarWav(double segundos, int taxa = 44100, int canais = 2, float valor = 0.5f)
    {
        var caminho = Path.Combine(_dir, $"{Guid.NewGuid():N}.wav");
        using var writer = new WaveFileWriter(caminho, new WaveFormat(taxa, 16, canais));
        var frames = (long)Math.Round(segundos * taxa);
        for (long i = 0; i < frames * canais; i++)
            writer.WriteSample(valor);
        return caminho;
    }

    private static (WaveFormat Formato, float[] Amostras) Ler(byte[] wav)
    {
        using var reader = new WaveFileReader(new MemoryStream(wav));
        var sp = reader.ToSampleProvider();
        var lista = new List<float>();
        var buffer = new float[4096];
        int n;
        while ((n = sp.Read(buffer, 0, buffer.Length)) > 0)
            lista.AddRange(buffer.Take(n));
        return (reader.WaveFormat, lista.ToArray());
    }

    [Fact]
    public void CortarParaWav_GravaExatamenteOsFramesDoTrecho()
    {
        var caminho = CriarWav(3, taxa: 44100, canais: 2);

        var wav = AudioTrimmer.CortarParaWav(caminho, TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1.5));

        var (formato, amostras) = Ler(wav);
        Assert.Equal(44100, formato.SampleRate);
        Assert.Equal(2, formato.Channels);
        Assert.Equal(16, formato.BitsPerSample);
        Assert.Equal(44100 * 2, amostras.Length); // 1 s estéreo
    }

    [Fact]
    public void CortarParaWav_AplicaMicroFadeNasBordas()
    {
        var caminho = CriarWav(1, taxa: 48000, canais: 1);

        var wav = AudioTrimmer.CortarParaWav(caminho, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(700));

        var (_, amostras) = Ler(wav);
        var framesFade = AudioTrimmer.ParaFrames(AudioTrimmer.FadePadrao, 48000);
        Assert.Equal(0f, amostras[0], 3);
        Assert.Equal(0f, amostras[^1], 3);
        Assert.True(amostras[(int)framesFade / 2] is > 0.1f and < 0.4f, "Metade do fade-in deve estar no meio do ganho.");
        Assert.Equal(0.5f, amostras[amostras.Length / 2], 2); // miolo intacto
    }

    [Fact]
    public void CortarParaWav_FimAlemDoAudio_CortaAteOFim()
    {
        var caminho = CriarWav(1, taxa: 8000, canais: 1);

        var wav = AudioTrimmer.CortarParaWav(caminho, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(5));

        Assert.Equal(4000, Ler(wav).Amostras.Length);
    }

    [Fact]
    public void Cortar_TrechoMenorQueOMinimo_Lanca()
    {
        var caminho = CriarWav(1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AudioTrimmer.CortarParaWav(caminho, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)));
    }

    [Fact]
    public void Cortar_InicioNegativo_Lanca()
    {
        var caminho = CriarWav(1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AudioTrimmer.CortarParaWav(caminho, TimeSpan.FromMilliseconds(-10), TimeSpan.FromMilliseconds(500)));
    }

    [Fact]
    public void Cortar_InicioAlemDoFim_Lanca()
    {
        var caminho = CriarWav(1);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AudioTrimmer.CortarParaWav(caminho, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void Cortar_Clampa_AmostrasAcimaDeUm()
    {
        var caminho = CriarWav(0.5, taxa: 8000, canais: 1, valor: 0.9f);
        using var reader = new WaveFileReader(caminho);
        var amplificado = new NAudio.Wave.SampleProviders.VolumeSampleProvider(reader.ToSampleProvider()) { Volume = 3f };
        using var destino = new MemoryStream();

        AudioTrimmer.Cortar(amplificado, TimeSpan.Zero, TimeSpan.FromMilliseconds(400), destino, TimeSpan.FromMilliseconds(5));

        var (_, amostras) = Ler(destino.ToArray());
        Assert.All(amostras, a => Assert.InRange(a, -1f, 1f));
        Assert.Equal(1f, amostras[amostras.Length / 2], 3);
    }

    [Fact]
    public void CortarParaWav_Mp3_GeraWavComDuracaoDoTrecho()
    {
        var wavOrigem = CriarWav(2, taxa: 44100, canais: 2, valor: 0.3f);
        var mp3 = Path.Combine(_dir, "origem.mp3");
        try
        {
            MediaFoundationApi.Startup();
            using var reader = new WaveFileReader(wavOrigem);
            MediaFoundationEncoder.EncodeToMp3(reader, mp3, 128000);
        }
        catch (Exception)
        {
            // Sem encoder MP3 do Media Foundation nesta máquina: nada a verificar.
            return;
        }

        var wav = AudioTrimmer.CortarParaWav(mp3, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(1250));

        var (formato, amostras) = Ler(wav);
        Assert.Equal(16, formato.BitsPerSample);
        Assert.Equal(formato.SampleRate * formato.Channels, amostras.Length); // 1 s exato
    }
}
