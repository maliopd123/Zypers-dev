using MySql.Data.MySqlClient;
using SistemaDeChaves.Models;

namespace SistemaDeChaves.DAO;

public sealed class AlertasDAO
{
    private readonly DatabaseConnection _database;
    public AlertasDAO(DatabaseConnection database) => _database = database;

    public async Task<int> CriarAlertasDeAtrasoAsync(int minutos, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            INSERT INTO alertas_atraso (movimentacao_id, criado_em, lido_em)
            SELECT m.id, NOW(), NULL
            FROM movimentacoes m
            LEFT JOIN alertas_atraso a ON a.movimentacao_id=m.id
            WHERE m.devolvida_em IS NULL
              AND m.prevista_devolucao <= DATE_SUB(NOW(), INTERVAL @minutos MINUTE)
              AND a.id IS NULL
            """, connection);
        command.Parameters.AddWithValue("@minutos", Math.Clamp(minutos, 0, 10080));
        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<AlertaAtraso>> ListarAsync(CancellationToken ct = default)
    {
        var alertas = new List<AlertaAtraso>();
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            SELECT a.id, m.id AS movimentacao_id, k.codigo AS codigo_chave,
                   u.codigo AS codigo_usuario, u.nome AS nome_usuario,
                   m.prevista_devolucao, a.criado_em, a.lido_em,
                   GREATEST(TIMESTAMPDIFF(MINUTE, m.prevista_devolucao, NOW()), 0) AS minutos_atraso
            FROM alertas_atraso a
            JOIN movimentacoes m ON m.id=a.movimentacao_id
            JOIN chaves k ON k.id=m.chave_id
            JOIN usuarios u ON u.id=m.usuario_id
            WHERE m.devolvida_em IS NULL
            ORDER BY a.lido_em IS NULL DESC, a.criado_em DESC
            LIMIT 50
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            alertas.Add(new AlertaAtraso
            {
                Id = reader.Number("id"),
                MovimentacaoId = reader.Number("movimentacao_id"),
                CodigoChave = reader.Text("codigo_chave"),
                CodigoUsuario = reader.Text("codigo_usuario"),
                NomeUsuario = reader.Text("nome_usuario"),
                PrevistaDevolucao = reader.GetDateTime(reader.GetOrdinal("prevista_devolucao")),
                CriadoEm = reader.GetDateTime(reader.GetOrdinal("criado_em")),
                LidoEm = reader.NullableDate("lido_em"),
                MinutosAtraso = reader.Number("minutos_atraso")
            });
        }
        return alertas;
    }

    public async Task MarcarComoLidoAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand(
            "UPDATE alertas_atraso SET lido_em=NOW() WHERE id=@id AND lido_em IS NULL", connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(ct);
    }
}
