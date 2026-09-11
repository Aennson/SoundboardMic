using SoundboardMic.Core.Models;
using SoundboardMic.Data;

namespace SoundboardMic.Tests;

public class CategoriaRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly CategoriaRepository _repo;

    public CategoriaRepositoryTests() => _repo = new CategoriaRepository(_db.Factory);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Add_AtribuiIdEOrdemSequencial()
    {
        var meme = await _repo.AddAsync(new Categoria { Nome = "Meme" });
        var trilha = await _repo.AddAsync(new Categoria { Nome = "Trilha sonora" });

        Assert.True(meme.Id > 0);
        Assert.Equal(0, meme.Ordem);
        Assert.Equal(1, trilha.Ordem);
    }

    [Fact]
    public async Task GetAll_OrdenaPorOrdem()
    {
        await _repo.AddAsync(new Categoria { Nome = "Zebra" });
        await _repo.AddAsync(new Categoria { Nome = "Abelha" });

        var todas = await _repo.GetAllAsync();

        // Ordem de inserção (Ordem sequencial), não alfabética.
        Assert.Equal(new[] { "Zebra", "Abelha" }, todas.Select(c => c.Nome));
    }

    [Fact]
    public async Task Update_RenomeiaEReordena()
    {
        var categoria = await _repo.AddAsync(new Categoria { Nome = "Antigo" });

        categoria.Nome = "Novo nome";
        categoria.Ordem = 5;
        var ok = await _repo.UpdateAsync(categoria);

        Assert.True(ok);
        var todas = await _repo.GetAllAsync();
        var atualizada = Assert.Single(todas);
        Assert.Equal("Novo nome", atualizada.Nome);
        Assert.Equal(5, atualizada.Ordem);
    }

    [Fact]
    public async Task Delete_RemoveCategoria()
    {
        var categoria = await _repo.AddAsync(new Categoria { Nome = "Descartável" });

        var ok = await _repo.DeleteAsync(categoria.Id);

        Assert.True(ok);
        Assert.Empty(await _repo.GetAllAsync());
    }

    [Fact]
    public async Task Delete_Inexistente_RetornaFalse()
    {
        Assert.False(await _repo.DeleteAsync(9999));
    }
}
