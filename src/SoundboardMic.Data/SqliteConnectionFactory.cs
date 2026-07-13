using Microsoft.Data.Sqlite;

namespace SoundboardMic.Data;

/// <summary>
/// Cria conexões SQLite já configuradas (foreign keys habilitadas).
/// </summary>
public class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        }.ToString();
    }

    /// <summary>Caminho padrão do banco: %LOCALAPPDATA%\SoundboardMic\soundboard.db</summary>
    public static string DefaultDatabasePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoundboardMic");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "soundboard.db");
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
