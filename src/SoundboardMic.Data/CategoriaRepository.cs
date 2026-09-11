using Microsoft.Data.Sqlite;
using SoundboardMic.Core.Models;
using SoundboardMic.Core.Repositories;

namespace SoundboardMic.Data;

public class CategoriaRepository : ICategoriaRepository
{
    private readonly SqliteConnectionFactory _factory;

    public CategoriaRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<Categoria> AddAsync(Categoria categoria, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoria.Nome, nameof(categoria.Nome));

        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Categorias (Nome, Ordem)
            VALUES ($nome, (SELECT COALESCE(MAX(Ordem), -1) + 1 FROM Categorias))
            RETURNING Id, Ordem;
            """;
        command.Parameters.AddWithValue("$nome", categoria.Nome.Trim());

        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        categoria.Id = reader.GetInt64(0);
        categoria.Ordem = reader.GetInt32(1);
        return categoria;
    }

    public async Task<IReadOnlyList<Categoria>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Nome, Ordem FROM Categorias ORDER BY Ordem, Nome COLLATE NOCASE;";

        var result = new List<Categoria>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new Categoria
            {
                Id = reader.GetInt64(0),
                Nome = reader.GetString(1),
                Ordem = reader.GetInt32(2),
            });
        }
        return result;
    }

    public async Task<bool> UpdateAsync(Categoria categoria, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(categoria.Nome, nameof(categoria.Nome));

        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Categorias SET Nome = $nome, Ordem = $ordem WHERE Id = $id;";
        command.Parameters.AddWithValue("$nome", categoria.Nome.Trim());
        command.Parameters.AddWithValue("$ordem", categoria.Ordem);
        command.Parameters.AddWithValue("$id", categoria.Id);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Categorias WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }
}
