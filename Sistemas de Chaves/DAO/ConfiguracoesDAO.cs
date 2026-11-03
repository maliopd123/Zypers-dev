using MySql.Data.MySqlClient;
using SistemaDeChaves.Models;

namespace SistemaDeChaves.DAO;

public sealed class ConfiguracoesDAO
{
    private readonly DatabaseConnection _database;
    public ConfiguracoesDAO(DatabaseConnection database) => _database = database;

    public async Task<ConfiguracaoSistema> ObterAsync(CancellationToken ct = default)
    {
        var config = new ConfiguracaoSistema();
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand(
            "SELECT chave, valor FROM configuracoes WHERE chave IN ('nome_sistema','minutos_para_alerta')", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var value = reader.Text("valor");
            if (reader.Text("chave") == "nome_sistema")
                config.NomeSistema = value;
            else if (int.TryParse(value, out var minutos))
                config.MinutosParaAlerta = Math.Clamp(minutos, 0, 10080);
        }
        return config;
    }

    public async Task SalvarAsync(ConfiguracaoSistema config, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await SalvarValorAsync(connection, (MySqlTransaction)transaction,
                "nome_sistema", config.NomeSistema.Trim(), ct);
            await SalvarValorAsync(connection, (MySqlTransaction)transaction,
                "minutos_para_alerta", Math.Clamp(config.MinutosParaAlerta, 0, 10080).ToString(), ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static async Task SalvarValorAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string chave,
        string valor,
        CancellationToken ct)
    {
        await using var command = new MySqlCommand(
            "UPDATE configuracoes SET valor=@valor WHERE chave=@chave", connection, transaction);
        command.Parameters.AddWithValue("@chave", chave);
        command.Parameters.AddWithValue("@valor", valor);
        await command.ExecuteNonQueryAsync(ct);
    }
}
