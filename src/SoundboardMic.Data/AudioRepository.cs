using System.Globalization;
using Microsoft.Data.Sqlite;
using SoundboardMic.Core.Models;
using SoundboardMic.Core.Repositories;

namespace SoundboardMic.Data;

public class AudioRepository : IAudioRepository
{
    private readonly SqliteConnectionFactory _factory;

    public AudioRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<Audio> AddAsync(Audio audio, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audio.Nome, nameof(audio.Nome));
        ArgumentException.ThrowIfNullOrWhiteSpace(audio.CaminhoArquivo, nameof(audio.CaminhoArquivo));

        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Audios (Nome, CaminhoArquivo, DuracaoMs, VolumePadrao, Icone, Cor, CriadoEm)
            VALUES ($nome, $caminho, $duracao, $volume, $icone, $cor, $criadoEm)
            RETURNING Id;
            """;
        command.Parameters.AddWithValue("$nome", audio.Nome);
        command.Parameters.AddWithValue("$caminho", audio.CaminhoArquivo);
        command.Parameters.AddWithValue("$duracao", audio.DuracaoMs);
        command.Parameters.AddWithValue("$volume", audio.VolumePadrao);
        command.Parameters.AddWithValue("$icone", (object?)audio.Icone ?? DBNull.Value);
        command.Parameters.AddWithValue("$cor", (object?)audio.Cor ?? DBNull.Value);
        command.Parameters.AddWithValue("$criadoEm", ToIso(audio.CriadoEm));

        audio.Id = (long)(await command.ExecuteScalarAsync(ct))!;
        return audio;
    }

    public async Task<Audio?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectClause} WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<Audio>> GetAllAsync(CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"{SelectClause} ORDER BY Nome COLLATE NOCASE;";

        var result = new List<Audio>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(Map(reader));
        return result;
    }

    public async Task<bool> UpdateAsync(Audio audio, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audio.Nome, nameof(audio.Nome));
        ArgumentException.ThrowIfNullOrWhiteSpace(audio.CaminhoArquivo, nameof(audio.CaminhoArquivo));

        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Audios
            SET Nome = $nome, CaminhoArquivo = $caminho, DuracaoMs = $duracao,
                VolumePadrao = $volume, Icone = $icone, Cor = $cor
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$nome", audio.Nome);
        command.Parameters.AddWithValue("$caminho", audio.CaminhoArquivo);
        command.Parameters.AddWithValue("$duracao", audio.DuracaoMs);
        command.Parameters.AddWithValue("$volume", audio.VolumePadrao);
        command.Parameters.AddWithValue("$icone", (object?)audio.Icone ?? DBNull.Value);
        command.Parameters.AddWithValue("$cor", (object?)audio.Cor ?? DBNull.Value);
        command.Parameters.AddWithValue("$id", audio.Id);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Audios WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    private const string SelectClause =
        "SELECT Id, Nome, CaminhoArquivo, DuracaoMs, VolumePadrao, Icone, Cor, CriadoEm FROM Audios";

    private static Audio Map(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Nome = reader.GetString(1),
        CaminhoArquivo = reader.GetString(2),
        DuracaoMs = reader.GetInt64(3),
        VolumePadrao = reader.GetDouble(4),
        Icone = reader.IsDBNull(5) ? null : reader.GetString(5),
        Cor = reader.IsDBNull(6) ? null : reader.GetString(6),
        CriadoEm = FromIso(reader.GetString(7)),
    };

    internal static string ToIso(DateTime value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    internal static DateTime FromIso(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
