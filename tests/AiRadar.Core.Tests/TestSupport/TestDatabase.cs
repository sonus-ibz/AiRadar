using AiRadar.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AiRadar.Core.Tests.TestSupport;

/// <summary>A real SQLite database in memory, created through the app's migrations.</summary>
internal sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public TestDatabase()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.Migrate();
    }

    public AiRadarDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AiRadarDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
