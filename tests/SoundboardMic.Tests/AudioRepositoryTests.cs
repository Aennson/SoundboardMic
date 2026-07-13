using SoundboardMic.Core.Models;
using SoundboardMic.Data;

namespace SoundboardMic.Tests;

public class AudioRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly AudioRepository _repo;

    public AudioRepositoryTests() => _repo = new AudioRepository(_db.Factory);

    public void Dispose() => _db.Dispose();

    private static Audio NovoAudio(string nome = "Air Horn") => new()
    {
        Nome = nome,
        CaminhoArquivo = @"C:\sons\airhorn.mp3",
        DuracaoMs = 2500,
        VolumePadrao = 0.8,
    };

    [Fact]
    public async Task Add_AtribuiIdEPersiste()
    {
        var audio = await _repo.AddAsync(NovoAudio());

        Assert.True(audio.Id > 0);

        var lido = await _repo.GetByIdAsync(audio.Id);
        Assert.NotNull(lido);
        Assert.Equal("Air Horn", lido.Nome);
        Assert.Equal(@"C:\sons\airhorn.mp3", lido.CaminhoArquivo);
        Assert.Equal(2500, lido.DuracaoMs);
        Assert.Equal(0.8, lido.VolumePadrao, precision: 6);
    }

    [Fact]
    public async Task Add_SemNome_Lanca()
    {
        var audio = NovoAudio();
        audio.Nome = "  ";
        await Assert.ThrowsAsync<ArgumentException>(() => _repo.AddAsync(audio));
    }

    [Fact]
    public async Task Add_SemCaminho_Lanca()
    {
        var audio = NovoAudio();
        audio.CaminhoArquivo = "";
        await Assert.ThrowsAsync<ArgumentException>(() => _repo.AddAsync(audio));
    }

    [Fact]
    public async Task Add_PreservaCriadoEmComoUtcIso()
    {
        var criadoEm = new DateTime(2026, 7, 13, 10, 30, 0, DateTimeKind.Utc);
        var audio = NovoAudio();
        audio.CriadoEm = criadoEm;

        await _repo.AddAsync(audio);
        var lido = await _repo.GetByIdAsync(audio.Id);

        Assert.Equal(criadoEm, lido!.CriadoEm);
        Assert.Equal(DateTimeKind.Utc, lido.CriadoEm.Kind);
    }

    [Fact]
    public async Task GetById_Inexistente_RetornaNull()
    {
        Assert.Null(await _repo.GetByIdAsync(9999));
    }

    [Fact]
    public async Task GetAll_OrdenaPorNome()
    {
        await _repo.AddAsync(NovoAudio("zebra"));
        await _repo.AddAsync(NovoAudio("Abelha"));
        await _repo.AddAsync(NovoAudio("mosca"));

        var todos = await _repo.GetAllAsync();

        Assert.Equal(new[] { "Abelha", "mosca", "zebra" }, todos.Select(a => a.Nome));
    }

    [Fact]
    public async Task Update_AlteraCampos()
    {
        var audio = await _repo.AddAsync(NovoAudio());
        audio.Nome = "Buzina";
        audio.VolumePadrao = 1.5;
        audio.DuracaoMs = 3000;

        var ok = await _repo.UpdateAsync(audio);

        Assert.True(ok);
        var lido = await _repo.GetByIdAsync(audio.Id);
        Assert.Equal("Buzina", lido!.Nome);
        Assert.Equal(1.5, lido.VolumePadrao, precision: 6);
        Assert.Equal(3000, lido.DuracaoMs);
    }

    [Fact]
    public async Task Update_Inexistente_RetornaFalse()
    {
        var fantasma = NovoAudio();
        fantasma.Id = 12345;
        Assert.False(await _repo.UpdateAsync(fantasma));
    }

    [Fact]
    public async Task Delete_RemoveRegistro()
    {
        var audio = await _repo.AddAsync(NovoAudio());

        Assert.True(await _repo.DeleteAsync(audio.Id));
        Assert.Null(await _repo.GetByIdAsync(audio.Id));
    }

    [Fact]
    public async Task Delete_Inexistente_RetornaFalse()
    {
        Assert.False(await _repo.DeleteAsync(9999));
    }
}
