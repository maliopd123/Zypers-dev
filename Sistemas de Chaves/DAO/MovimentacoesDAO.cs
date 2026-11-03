using MySql.Data.MySqlClient;
using SistemaDeChaves.Models;

namespace SistemaDeChaves.DAO;

public sealed class MovimentacoesDAO
{
    private readonly DatabaseConnection _database;

    public MovimentacoesDAO(DatabaseConnection database) => _database = database;

    public async Task RegistrarUsoAsync(
        string codigoChave,
        string codigoUsuario,
        int registradaPorId,
        DateTime retiradaEm,
        DateTime previstaDevolucao,
        string? observacoes,
        CancellationToken ct = default)
    {
        if (previstaDevolucao <= retiradaEm)
            throw new InvalidOperationException("A previsão de devolução deve ser posterior à retirada.");

        await using var connection = await _database.OpenAsync(ct);
        using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            int chaveId;
            await using (var command = new MySqlCommand(
                "SELECT id, situacao FROM chaves WHERE codigo=@codigo", connection, (MySqlTransaction)transaction))
            {
                command.Parameters.AddWithValue("@codigo", codigoChave.Trim());
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Não encontramos essa chave.");
                if (reader.Text("situacao") != "disponivel")
                    throw new InvalidOperationException("Essa chave não está disponível para retirada.");
                chaveId = reader.Number("id");
            }

            int usuarioId;
            await using (var command = new MySqlCommand(
                "SELECT id FROM usuarios WHERE codigo=@codigo AND ativo=1 LIMIT 1", connection, (MySqlTransaction)transaction))
            {
                command.Parameters.AddWithValue("@codigo", codigoUsuario.Trim());
                var resultado = await command.ExecuteScalarAsync(ct);
                if (resultado is null or DBNull)
                    throw new InvalidOperationException("Não encontramos um usuário ativo com esse código.");
                usuarioId = Convert.ToInt32(resultado);
            }

            await using (var command = new MySqlCommand("""
                INSERT INTO movimentacoes
                  (chave_id, usuario_id, registrada_por_id, retirada_em, prevista_devolucao, observacoes,
                   devolvida_em, observacoes_devolucao)
                VALUES (@chave, @usuario, @registradaPor, @retirada, @prevista, @observacoes, NULL, NULL)
                """, connection, (MySqlTransaction)transaction))
            {
                command.Parameters.AddWithValue("@chave", chaveId);
                command.Parameters.AddWithValue("@usuario", usuarioId);
                command.Parameters.AddWithValue("@registradaPor", registradaPorId);
                command.Parameters.AddWithValue("@retirada", retiradaEm);
                command.Parameters.AddWithValue("@prevista", previstaDevolucao);
                command.Parameters.AddWithValue("@observacoes", string.IsNullOrWhiteSpace(observacoes) ? DBNull.Value : observacoes.Trim());
                await command.ExecuteNonQueryAsync(ct);
            }

            await using (var command = new MySqlCommand(
                "UPDATE chaves SET situacao='em_uso' WHERE id=@id AND situacao='disponivel'", connection, (MySqlTransaction)transaction))
            {
                command.Parameters.AddWithValue("@id", chaveId);
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("A chave acabou de ser retirada por outra pessoa. Atualize a tela e tente novamente.");
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task RegistrarDevolucaoAsync(
        string codigoChave,
        string codigoUsuario,
        DateTime devolvidaEm,
        string? observacoes,
        CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            int chaveId;
            int movimentacaoId;
            DateTime retiradaEm;
            await using (var command = new MySqlCommand("""
                SELECT m.id, m.chave_id, m.retirada_em
                FROM movimentacoes m
                JOIN chaves k ON k.id=m.chave_id
                JOIN usuarios u ON u.id=m.usuario_id
                WHERE k.codigo=@chave AND u.codigo=@usuario AND m.devolvida_em IS NULL
                """, connection, (MySqlTransaction)transaction))
            {
                command.Parameters.AddWithValue("@chave", codigoChave.Trim());
                command.Parameters.AddWithValue("@usuario", codigoUsuario.Trim());
                await using var reader = await command.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    throw new InvalidOperationException("Não há uma retirada em aberto para essa chave e esse usuário.");
                chaveId = reader.Number("chave_id");
                movimentacaoId = reader.Number("id");
                retiradaEm = reader.GetDateTime(reader.GetOrdinal("retirada_em"));
            }

            if (devolvidaEm < retiradaEm)
                throw new InvalidOperationException("A data de devolução não pode ser anterior à retirada.");

            await using (var command = new MySqlCommand("""
                UPDATE movimentacoes
                SET devolvida_em=@devolvida, observacoes_devolucao=@observacoes
                WHERE id=@id AND devolvida_em IS NULL
                """, connection, (MySqlTransaction)transaction))
            {
                command.Parameters.AddWithValue("@devolvida", devolvidaEm);
                command.Parameters.AddWithValue("@observacoes", string.IsNullOrWhiteSpace(observacoes) ? DBNull.Value : observacoes.Trim());
                command.Parameters.AddWithValue("@id", movimentacaoId);
                if (await command.ExecuteNonQueryAsync(ct) != 1)
                    throw new InvalidOperationException("Essa devolução já foi registrada.");
            }

            await using (var command = new MySqlCommand(
                "UPDATE chaves SET situacao='disponivel' WHERE id=@id", connection, (MySqlTransaction)transaction))
            {
                command.Parameters.AddWithValue("@id", chaveId);
                await command.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<List<Movimentacao>> ListarAsync(
        string tipo = "historico",
        string busca = "",
        DateTime? inicio = null,
        DateTime? fim = null,
        CancellationToken ct = default)
    {
        var itens = new List<Movimentacao>();
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            SELECT m.id, m.chave_id, k.codigo AS codigo_chave, k.descricao AS descricao_chave,
                   k.local AS local_chave, u.codigo AS codigo_usuario, u.nome AS nome_usuario,
                   m.retirada_em, m.prevista_devolucao, m.devolvida_em,
                   COALESCE(m.observacoes, '') AS observacoes,
                   COALESCE(m.observacoes_devolucao, '') AS observacoes_devolucao,
                   CASE WHEN m.devolvida_em IS NOT NULL THEN 'devolvida'
                        WHEN m.prevista_devolucao < NOW() THEN 'atrasada'
                        ELSE 'em_uso' END AS situacao
            FROM movimentacoes m
            JOIN chaves k ON k.id=m.chave_id
            JOIN usuarios u ON u.id=m.usuario_id
            WHERE
              (@busca='' OR k.codigo LIKE @termo OR k.descricao LIKE @termo OR u.codigo LIKE @termo OR u.nome LIKE @termo)
              AND (@inicio IS NULL OR m.retirada_em >= @inicio)
              AND (@fim IS NULL OR m.retirada_em < DATE_ADD(@fim, INTERVAL 1 DAY))
              AND (
                @tipo='historico'
                OR (@tipo='em_uso' AND m.devolvida_em IS NULL AND m.prevista_devolucao >= NOW())
                OR (@tipo='atrasada' AND m.devolvida_em IS NULL AND m.prevista_devolucao < NOW())
              )
            ORDER BY m.retirada_em DESC
            """, connection);
        command.Parameters.AddWithValue("@busca", busca.Trim());
        command.Parameters.AddWithValue("@termo", $"%{busca.Trim()}%");
        command.Parameters.AddWithValue("@inicio", inicio.HasValue ? inicio.Value.Date : DBNull.Value);
        command.Parameters.AddWithValue("@fim", fim.HasValue ? fim.Value.Date : DBNull.Value);
        command.Parameters.AddWithValue("@tipo", tipo.Trim().ToLowerInvariant());
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            itens.Add(new Movimentacao
            {
                Id = reader.Number("id"),
                ChaveId = reader.Number("chave_id"),
                CodigoChave = reader.Text("codigo_chave"),
                DescricaoChave = reader.Text("descricao_chave"),
                LocalChave = reader.Text("local_chave"),
                CodigoUsuario = reader.Text("codigo_usuario"),
                NomeUsuario = reader.Text("nome_usuario"),
                RetiradaEm = reader.GetDateTime(reader.GetOrdinal("retirada_em")),
                PrevistaDevolucao = reader.GetDateTime(reader.GetOrdinal("prevista_devolucao")),
                DevolvidaEm = reader.NullableDate("devolvida_em"),
                Observacoes = reader.Text("observacoes"),
                ObservacoesDevolucao = reader.Text("observacoes_devolucao"),
                Situacao = reader.Text("situacao")
            });
        }
        return itens;
    }
}
