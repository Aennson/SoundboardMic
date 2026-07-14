using Microsoft.Data.Sqlite;
using SoundboardMic.Data;

namespace SoundboardMic.Tests;

/// <summary>
/// Valida a migração incremental: um banco criado por versão antiga (sem as
/// colunas Icone/Cor) ganha as colunas ao rodar o bootstrapper de novo,
/// preservando as linhas existentes.
/// </summary>
public sealed class DatabaseBootstrapperTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"soundboardmic-migracao-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private void CriarBancoAntigo()
    {
        using var connection = new SqliteConnection($"Data Source={_path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE Audios (
                Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                Nome           TEXT    NOT NULL,
                CaminhoArquivo TEXT    NOT NULL,
                DuracaoMs      INTEGER NOT NULL DEFAULT 0,
                VolumePadrao   REAL    NOT NULL DEFAULT 1.0,
                CriadoEm       TEXT    NOT NULL
            );
            INSERT INTO Audios (Nome, CaminhoArquivo, DuracaoMs, VolumePadrao, CriadoEm)
            VALUES ('Antigo', 'C:\antigo.mp3', 1000, 1.0, '2026-01-01T00:00:00.0000000Z');
            """;
        command.ExecuteNonQuery();
    }

    [Fact]
    public async Task BancoAntigo_GanhaColunasIconeECor_EPreservaLinhas()
    {
        CriarBancoAntigo();
        var factory = new SqliteConnectionFactory(_path);
        var bootstrapper = new DatabaseBootstrapper(factory);

        // Duas vezes: a migração precisa ser idempotente.
        await bootstrapper.InitializeAsync();
        await bootstrapper.InitializeAsync();

        // As colunas existem.
        await using (var connection = await factory.OpenAsync())
        {
            await using var pragma = connection.CreateCommand();
            pragma.CommandText = "PRAGMA table_info(Audios);";
            var colunas = new List<string>();
            await using var reader = await pragma.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                colunas.Add(reader.GetString(1));

            Assert.Contains("Icone", colunas);
            Assert.Contains("Cor", colunas);
        }

        // A linha antiga sobreviveu e lê com Icone/Cor nulos.
        var repo = new AudioRepository(factory);
        var todos = await repo.GetAllAsync();
        var antigo = Assert.Single(todos);
        Assert.Equal("Antigo", antigo.Nome);
        Assert.Null(antigo.Icone);
        Assert.Null(antigo.Cor);

        // E aceita gravar ícone/cor depois da migração.
        antigo.Icone = "E8D6";
        antigo.Cor = "#7C5CFF";
        Assert.True(await repo.UpdateAsync(antigo));

        var relido = await repo.GetByIdAsync(antigo.Id);
        Assert.Equal("E8D6", relido!.Icone);
        Assert.Equal("#7C5CFF", relido.Cor);
    }
}
