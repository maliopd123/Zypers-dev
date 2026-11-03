using MySql.Data.MySqlClient;
using SistemaDeChaves.Models;

namespace SistemaDeChaves.DAO;

public sealed class PainelDAO
{
    private readonly DatabaseConnection _database;
    public PainelDAO(DatabaseConnection database) => _database = database;

    public async Task<ResumoPainel> ObterResumoAsync(CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            SELECT COUNT(*) AS total,
                   COALESCE(SUM(k.situacao='disponivel'), 0) AS disponiveis,
                   COALESCE(SUM(k.situacao='em_uso' AND m.prevista_devolucao>=NOW()), 0) AS em_uso,
                   COALESCE(SUM(k.situacao='em_uso' AND m.prevista_devolucao<NOW()), 0) AS em_atraso
            FROM chaves k
            LEFT JOIN movimentacoes m ON m.chave_id=k.id AND m.devolvida_em IS NULL
            WHERE k.situacao <> 'indisponivel'
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new ResumoPainel();
        return new ResumoPainel
        {
            TotalChaves = reader.Number("total"),
            Disponiveis = reader.Number("disponiveis"),
            EmUso = reader.Number("em_uso"),
            EmAtraso = reader.Number("em_atraso")
        };
    }
}
