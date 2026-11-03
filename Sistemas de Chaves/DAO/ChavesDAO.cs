using MySql.Data.MySqlClient;
using SistemaDeChaves.Models;

namespace SistemaDeChaves.DAO;

public sealed class ChavesDAO
{
    private readonly DatabaseConnection _database;

    public ChavesDAO(DatabaseConnection database) => _database = database;

    public async Task<List<Chave>> ListarAsync(
        string situacao = "todas",
        string busca = "",
        int? categoriaId = null,
        CancellationToken ct = default)
    {
        var chaves = new List<Chave>();
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            SELECT k.id, k.codigo, k.descricao, k.local, k.categoria_id,
                   c.nome AS categoria, k.data_cadastro, COALESCE(k.observacoes, '') AS observacoes,
                   CASE
                     WHEN k.situacao='em_uso' AND m.prevista_devolucao < NOW() THEN 'atrasada'
                     ELSE k.situacao
                   END AS situacao_atual,
                   u.codigo AS codigo_usuario, u.nome AS nome_usuario,
                   m.prevista_devolucao,
                   CASE
                     WHEN k.situacao='em_uso' AND m.prevista_devolucao < NOW()
                     THEN GREATEST(TIMESTAMPDIFF(MINUTE, m.prevista_devolucao, NOW()), 0)
                     ELSE 0
                   END AS minutos_atraso
            FROM chaves k
            JOIN categorias_chave c ON c.id=k.categoria_id
            LEFT JOIN movimentacoes m ON m.chave_id=k.id AND m.devolvida_em IS NULL
            LEFT JOIN usuarios u ON u.id=m.usuario_id
            WHERE
              (@busca='' OR k.codigo LIKE @termo OR k.descricao LIKE @termo OR
               k.local LIKE @termo OR u.nome LIKE @termo OR u.codigo LIKE @termo)
              AND (@categoria=0 OR k.categoria_id=@categoria)
              AND (
                @situacao='todas'
                OR (@situacao='disponivel' AND k.situacao='disponivel')
                OR (@situacao='em_uso' AND k.situacao='em_uso' AND m.prevista_devolucao>=NOW())
                OR (@situacao='atrasada' AND k.situacao='em_uso' AND m.prevista_devolucao<NOW())
                OR (@situacao='indisponivel' AND k.situacao='indisponivel')
              )
            ORDER BY k.codigo
            """, connection);
        command.Parameters.AddWithValue("@busca", busca.Trim());
        command.Parameters.AddWithValue("@termo", $"%{busca.Trim()}%");
        command.Parameters.AddWithValue("@categoria", categoriaId ?? 0);
        command.Parameters.AddWithValue("@situacao", situacao.Trim().ToLowerInvariant());
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            chaves.Add(new Chave
            {
                Id = reader.Number("id"),
                Codigo = reader.Text("codigo"),
                Descricao = reader.Text("descricao"),
                Local = reader.Text("local"),
                CategoriaId = reader.Number("categoria_id"),
                Categoria = reader.Text("categoria"),
                DataCadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                Observacoes = reader.Text("observacoes"),
                Situacao = reader.Text("situacao_atual"),
                CodigoUsuario = reader.IsDBNull(reader.GetOrdinal("codigo_usuario")) ? null : reader.Text("codigo_usuario"),
                NomeUsuario = reader.IsDBNull(reader.GetOrdinal("nome_usuario")) ? null : reader.Text("nome_usuario"),
                PrevistaDevolucao = reader.NullableDate("prevista_devolucao"),
                MinutosAtraso = reader.Number("minutos_atraso")
            });
        }
        return chaves;
    }

    public async Task CriarAsync(Chave chave, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            INSERT INTO chaves (codigo, descricao, local, categoria_id, data_cadastro, observacoes, situacao)
            VALUES (@codigo, @descricao, @local, @categoria, @data, @observacoes, 'disponivel')
            """, connection);
        command.Parameters.AddWithValue("@codigo", chave.Codigo.Trim());
        command.Parameters.AddWithValue("@descricao", chave.Descricao.Trim());
        command.Parameters.AddWithValue("@local", chave.Local.Trim());
        command.Parameters.AddWithValue("@categoria", chave.CategoriaId);
        command.Parameters.AddWithValue("@data", chave.DataCadastro == default ? DateTime.Now : chave.DataCadastro);
        command.Parameters.AddWithValue("@observacoes", string.IsNullOrWhiteSpace(chave.Observacoes) ? DBNull.Value : chave.Observacoes.Trim());
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task AtualizarAsync(Chave chave, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            UPDATE chaves
            SET codigo=@codigo, descricao=@descricao, local=@local, categoria_id=@categoria, observacoes=@observacoes
            WHERE id=@id
            """, connection);
        command.Parameters.AddWithValue("@codigo", chave.Codigo.Trim());
        command.Parameters.AddWithValue("@descricao", chave.Descricao.Trim());
        command.Parameters.AddWithValue("@local", chave.Local.Trim());
        command.Parameters.AddWithValue("@categoria", chave.CategoriaId);
        command.Parameters.AddWithValue("@observacoes", string.IsNullOrWhiteSpace(chave.Observacoes) ? DBNull.Value : chave.Observacoes.Trim());
        command.Parameters.AddWithValue("@id", chave.Id);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task InativarAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            UPDATE chaves SET situacao='indisponivel'
            WHERE id=@id AND NOT EXISTS
              (SELECT 1 FROM movimentacoes WHERE chave_id=@id AND devolvida_em IS NULL)
            """, connection);
        command.Parameters.AddWithValue("@id", id);
        if (await command.ExecuteNonQueryAsync(ct) != 1)
            throw new InvalidOperationException("A chave está em uso ou não foi encontrada.");
    }
}
