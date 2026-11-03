using MySql.Data.MySqlClient;

namespace SistemaDeChaves.DAO;

public sealed class DatabaseConnection
{
    private readonly string _connectionString;

    public DatabaseConnection(IConfiguration configuration)
    {
        var connectionText = configuration.GetConnectionString("MySqlConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:MySqlConnection não foi configurada.");
        var builder = new MySqlConnectionStringBuilder(connectionText)
        {
            Database = configuration["DatabaseName"] ?? "sistema_chaves"
        };
        _connectionString = builder.ConnectionString;
    }

    public async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
