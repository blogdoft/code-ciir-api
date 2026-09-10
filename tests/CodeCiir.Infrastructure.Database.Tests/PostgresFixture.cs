using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CodeCiir.Infrastructure.Database.Tests;

/// <summary>
/// Spins up a disposable Postgres/pgvector container per test collection and seeds it with the
/// DDL in <see cref="Schema"/>. Real, isolated database - not the shared code3rag instance.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();

    public NpgsqlDataSource DataSource { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // pgvector's Npgsql plugin resolves the "vector" type OID once, the first time the data
        // source is used - if that happens before CREATE EXTENSION has run, the resolution fails
        // permanently for this data source instance. A plain connection (no UseVector()) creates
        // the extension first; only then is the vector-aware DataSource built/used.
        await using (var setupConnection = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await setupConnection.OpenAsync();
            await using var extensionCommand = setupConnection.CreateCommand();
            extensionCommand.CommandText = Schema.CreateExtensionVector;
            await extensionCommand.ExecuteNonQueryAsync();
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(_container.GetConnectionString());
        dataSourceBuilder.UseVector();
        DataSource = dataSourceBuilder.Build();

        await using var connection = await DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = Schema.CreateTables;
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await _container.DisposeAsync();
    }
}
