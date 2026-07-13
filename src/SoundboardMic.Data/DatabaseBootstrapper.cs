namespace SoundboardMic.Data;

/// <summary>
/// Cria o schema do banco na primeira execução (idempotente).
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
    }
}
