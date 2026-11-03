using Microsoft.AspNetCore.Identity;
using MySql.Data.MySqlClient;
using SistemaDeChaves.Models;

namespace SistemaDeChaves.Services;

public sealed class DatabaseInitializer
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IPasswordHasher<Usuario> _passwordHasher;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IPasswordHasher<Usuario> passwordHasher,
        ILogger<DatabaseInitializer> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var connectionText = _configuration.GetConnectionString("MySqlConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:MySqlConnection não foi configurada.");
        var builder = new MySqlConnectionStringBuilder(connectionText)
        {
            Database = _configuration["DatabaseName"] ?? "sistema_chaves"
        };
        var databaseName = builder.Database;
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("A conexão do MySQL precisa indicar o banco sistema_chaves.");

        builder.Database = "";
        await using (var server = new MySqlConnection(builder.ConnectionString))
        {
            await server.OpenAsync(ct);
            var safeName = databaseName.Replace("\u0060", "\u0060\u0060", StringComparison.Ordinal);
            await using var createDatabase = new MySqlCommand(
                $"CREATE DATABASE IF NOT EXISTS \u0060{safeName}\u0060", server);
            await createDatabase.ExecuteNonQueryAsync(ct);
        }

        builder.Database = databaseName;
        await using (var connection = new MySqlConnection(builder.ConnectionString))
        {
            await connection.OpenAsync(ct);
            var scriptPath = Path.Combine(_environment.ContentRootPath, "DAO", "Chaves.sql");
            var script = await File.ReadAllTextAsync(scriptPath, ct);
            foreach (var statement in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await using var command = new MySqlCommand(statement, connection);
                await command.ExecuteNonQueryAsync(ct);
            }

            await CriarDadosIniciaisAsync(connection, ct);
        }

        await CriarAdministradorInicialAsync(builder.ConnectionString, ct);
        _logger.LogInformation("Estrutura do Sistema de Chaves pronta no banco {Database}.", databaseName);
    }

    private static async Task CriarDadosIniciaisAsync(MySqlConnection connection, CancellationToken ct)
    {
        foreach (var nome in new[] { "Servidor", "Terceirizado", "Estagi?rio", "Bolsista", "Monitor" })
            await CriarCategoriaAsync(connection, "categorias_usuario", nome, ct);

        await CriarCategoriaAsync(connection, "categorias_chave", "Geral", ct);
        await CriarConfiguracaoAsync(connection, "nome_sistema", "Sistema de Chaves", ct);
        await CriarConfiguracaoAsync(connection, "minutos_para_alerta", "10", ct);
    }

    private static async Task CriarCategoriaAsync(
        MySqlConnection connection,
        string tabela,
        string nome,
        CancellationToken ct)
    {
        await using var consulta = new MySqlCommand(
            $"SELECT COUNT(*) FROM {tabela} WHERE nome=@nome", connection);
        consulta.Parameters.AddWithValue("@nome", nome);
        if (Convert.ToInt32(await consulta.ExecuteScalarAsync(ct)) > 0)
            return;

        await using var inserir = new MySqlCommand(
            $"INSERT INTO {tabela} (nome, ativa) VALUES (@nome, 1)", connection);
        inserir.Parameters.AddWithValue("@nome", nome);
        await inserir.ExecuteNonQueryAsync(ct);
    }

    private static async Task CriarConfiguracaoAsync(
        MySqlConnection connection,
        string chave,
        string valor,
        CancellationToken ct)
    {
        await using var consulta = new MySqlCommand(
            "SELECT COUNT(*) FROM configuracoes WHERE chave=@chave", connection);
        consulta.Parameters.AddWithValue("@chave", chave);
        if (Convert.ToInt32(await consulta.ExecuteScalarAsync(ct)) > 0)
            return;

        await using var inserir = new MySqlCommand(
            "INSERT INTO configuracoes (chave, valor) VALUES (@chave, @valor)", connection);
        inserir.Parameters.AddWithValue("@chave", chave);
        inserir.Parameters.AddWithValue("@valor", valor);
        await inserir.ExecuteNonQueryAsync(ct);
    }

    private async Task CriarAdministradorInicialAsync(string connectionString, CancellationToken ct)
    {
        var codigo = _configuration["BootstrapAdmin:Code"] ?? "ADMIN001";
        var email = _configuration["BootstrapAdmin:Email"] ?? "admin@localhost";
        var nome = _configuration["BootstrapAdmin:Name"] ?? "Administrador";
        var senha = _configuration["BootstrapAdmin:Password"] ?? "Chaves@123";

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var countCommand = new MySqlCommand(
            "SELECT COUNT(*) FROM usuarios WHERE nivel_acesso='administrador'", connection);
        if (Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct)) > 0)
            return;

        await using var categoryCommand = new MySqlCommand(
            "SELECT id FROM categorias_usuario WHERE nome='Servidor' LIMIT 1", connection);
        var categoriaId = Convert.ToInt32(await categoryCommand.ExecuteScalarAsync(ct));
        var usuario = new Usuario { Codigo = codigo, Nome = nome, Email = email, CategoriaId = categoriaId };
        var hash = _passwordHasher.HashPassword(usuario, senha);

        await using var insert = new MySqlCommand("""
            INSERT INTO usuarios (codigo, nome, email, senha_hash, categoria_id, nivel_acesso, ativo, criado_em)
            VALUES (@codigo, @nome, @email, @senha, @categoria, 'administrador', 1, NOW())
            """, connection);
        insert.Parameters.AddWithValue("@codigo", codigo);
        insert.Parameters.AddWithValue("@nome", nome);
        insert.Parameters.AddWithValue("@email", email);
        insert.Parameters.AddWithValue("@senha", hash);
        insert.Parameters.AddWithValue("@categoria", categoriaId);
        await insert.ExecuteNonQueryAsync(ct);
        _logger.LogWarning("Usuário administrador inicial criado com o código configurado em BootstrapAdmin:Code.");
    }
}
