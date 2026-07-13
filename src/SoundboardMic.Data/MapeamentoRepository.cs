using Microsoft.Data.Sqlite;
using SoundboardMic.Core.Models;
using SoundboardMic.Core.Repositories;

namespace SoundboardMic.Data;

public class MapeamentoRepository : IMapeamentoRepository
{
    // Código SQLite para violação de constraint UNIQUE.
    private const int SqliteConstraintUnique = 2067;

    private readonly SqliteConnectionFactory _factory;

    public MapeamentoRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<Mapeamento> AddAsync(Mapeamento mapeamento, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapeamento.Teclas, nameof(mapeamento.Teclas));

        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Mapeamentos (AudioId, Teclas, Ativo, CriadoEm)
            VALUES ($audioId, $teclas, $ativo, $criadoEm)
            RETURNING Id;
            """;
        command.Parameters.AddWithValue("$audioId", mapeamento.AudioId);
        command.Parameters.AddWithValue("$teclas", mapeamento.Teclas);
        command.Parameters.AddWithValue("$ativo", mapeamento.Ativo ? 1 : 0);
        command.Parameters.AddWithValue("$criadoEm", AudioRepository.ToIso(mapeamento.CriadoEm));

        try
        {
            mapeamento.Id = (long)(await command.ExecuteScalarAsync(ct))!;
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteConstraintUnique)
        {
            throw new TeclasDuplicadasException(mapeamento.Teclas, ex);
        }
        return mapeamento;
    }

    public async Task<Mapeamento?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectClause} WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<Mapeamento?> GetByTeclasAsync(string teclas, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectClause} WHERE Teclas = $teclas;";
        command.Parameters.AddWithValue("$teclas", teclas);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<Mapeamento>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectClause} ORDER BY Id;";
        return await ReadListAsync(command, ct);
    }

    public async Task<IReadOnlyList<Mapeamento>> GetByAudioIdAsync(long audioId, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectClause} WHERE AudioId = $audioId ORDER BY Id;";
        command.Parameters.AddWithValue("$audioId", audioId);
        return await ReadListAsync(command, ct);
    }

    public async Task<bool> UpdateAsync(Mapeamento mapeamento, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapeamento.Teclas, nameof(mapeamento.Teclas));

        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Mapeamentos
            SET AudioId = $audioId, Teclas = $teclas, Ativo = $ativo
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$audioId", mapeamento.AudioId);
        command.Parameters.AddWithValue("$teclas", mapeamento.Teclas);
        command.Parameters.AddWithValue("$ativo", mapeamento.Ativo ? 1 : 0);
        command.Parameters.AddWithValue("$id", mapeamento.Id);

        try
        {
            return await command.ExecuteNonQueryAsync(ct) > 0;
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == SqliteConstraintUnique)
        {
            throw new TeclasDuplicadasException(mapeamento.Teclas, ex);
        }
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Mapeamentos WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    private const string SelectClause =
        "SELECT Id, AudioId, Teclas, Ativo, CriadoEm FROM Mapeamentos";

    private static async Task<IReadOnlyList<Mapeamento>> ReadListAsync(SqliteCommand command, CancellationToken ct)
    {
        var result = new List<Mapeamento>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(Map(reader));
        return result;
    }

    private static Mapeamento Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        AudioId = reader.GetInt64(1),
        Teclas = reader.GetString(2),
        Ativo = reader.GetInt64(3) != 0,
        CriadoEm = AudioRepository.FromIso(reader.GetString(4)),
    };
}
