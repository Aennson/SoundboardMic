using Microsoft.Data.Sqlite;
using SoundboardMic.Data;

namespace SoundboardMic.Tests;

/// <summary>
/// Banco SQLite temporário em arquivo, isolado por teste, apagado no Dispose.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _path;

    public SqliteConnectionFactory Factory { get; }

    public TestDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"soundboardmic-tests-{Guid.NewGuid():N}.db");
        Factory = new SqliteConnectionFactory(_path);
        new DatabaseBootstrapper(Factory).InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
