using Microsoft.Data.Sqlite;

namespace SoundboardMic.Data;

/// <summary>
/// Cria o schema do banco na primeira execução e aplica migrações
/// incrementais em bancos existentes (tudo idempotente).
/// </summary>
public class DatabaseBootstrapper
{
    private readonly SqliteConnectionFactory _factory;

    public DatabaseBootstrapper(SqliteConnectionFactory factory) => _factory = factory;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Audios (
                Id             INTEGER PRIMARY KEY AUTOINCREMENT,
                Nome           TEXT    NOT NULL,
                CaminhoArquivo TEXT    NOT NULL,
                DuracaoMs      INTEGER NOT NULL DEFAULT 0,
                VolumePadrao   REAL    NOT NULL DEFAULT 1.0,
                CriadoEm       TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Mapeamentos (
                Id       INTEGER PRIMARY KEY AUTOINCREMENT,
                AudioId  INTEGER NOT NULL REFERENCES Audios(Id) ON DELETE CASCADE,
                Teclas   TEXT    NOT NULL UNIQUE,
                Ativo    INTEGER NOT NULL DEFAULT 1,
                CriadoEm TEXT    NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Mapeamentos_AudioId ON Mapeamentos(AudioId);
            """;
        await command.ExecuteNonQueryAsync(ct);

        // Migrações para bancos criados por versões anteriores.
        await EnsureColumnAsync(connection, "Audios", "Icone", "TEXT NULL", ct);
        await EnsureColumnAsync(connection, "Audios", "Cor", "TEXT NULL", ct);
        // Conteúdo do arquivo passa a viver no próprio banco (BLOB), em vez de depender
        // só do caminho externo escolhido pelo usuário — ver AudioFileCache no App.
        await EnsureColumnAsync(connection, "Audios", "ArquivoConteudo", "BLOB NULL", ct);
        await EnsureColumnAsync(connection, "Audios", "ArquivoNomeOriginal", "TEXT NULL", ct);
    }

    /// <summary>
    /// Adiciona a coluna se ainda não existir. Tabela/coluna/definição são
    /// constantes internas (PRAGMA não aceita parâmetros).
    /// </summary>
    private static async Task EnsureColumnAsync(
        SqliteConnection connection, string table, string column, string definition, CancellationToken ct)
    {
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = $"PRAGMA table_info({table});";
            await using var reader = await pragma.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        await alter.ExecuteNonQueryAsync(ct);
    }
}
