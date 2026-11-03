using System.Data.Common;
using MySql.Data.MySqlClient;
using SistemaDeChaves.Models;

namespace SistemaDeChaves.DAO;

public sealed class UsuariosDAO
{
    private readonly DatabaseConnection _database;

    public UsuariosDAO(DatabaseConnection database) => _database = database;

    public async Task<List<Usuario>> ListarAsync(CancellationToken ct = default)
    {
        var usuarios = new List<Usuario>();
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            SELECT u.id, u.codigo, u.nome, u.email, u.senha_hash, u.categoria_id,
                   c.nome AS categoria, u.nivel_acesso, u.ativo, u.criado_em
            FROM usuarios u
            JOIN categorias_usuario c ON c.id = u.categoria_id
            ORDER BY u.nome
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            usuarios.Add(Ler(reader));
        return usuarios;
    }

    public async Task<Usuario?> ObterPorCodigoAsync(string codigo, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            SELECT u.id, u.codigo, u.nome, u.email, u.senha_hash, u.categoria_id,
                   c.nome AS categoria, u.nivel_acesso, u.ativo, u.criado_em
            FROM usuarios u
            JOIN categorias_usuario c ON c.id = u.categoria_id
            WHERE u.codigo=@codigo AND u.ativo=1 LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("@codigo", codigo.Trim());
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Ler(reader) : null;
    }

    public async Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            SELECT u.id, u.codigo, u.nome, u.email, u.senha_hash, u.categoria_id,
                   c.nome AS categoria, u.nivel_acesso, u.ativo, u.criado_em
            FROM usuarios u
            JOIN categorias_usuario c ON c.id = u.categoria_id
            WHERE u.id=@id LIMIT 1
            """, connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Ler(reader) : null;
    }

    public async Task CriarAsync(Usuario usuario, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            INSERT INTO usuarios (codigo, nome, email, senha_hash, categoria_id, nivel_acesso, ativo, criado_em)
            VALUES (@codigo, @nome, @email, @senha, @categoria, @nivel, 1, NOW())
            """, connection);
        command.Parameters.AddWithValue("@codigo", usuario.Codigo.Trim());
        command.Parameters.AddWithValue("@nome", usuario.Nome.Trim());
        command.Parameters.AddWithValue("@email", usuario.Email.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("@senha", usuario.SenhaHash);
        command.Parameters.AddWithValue("@categoria", usuario.CategoriaId);
        command.Parameters.AddWithValue("@nivel", NormalizarNivel(usuario.NivelAcesso));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task AtualizarPerfilAsync(int id, string nome, string email, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand(
            "UPDATE usuarios SET nome=@nome, email=@email WHERE id=@id", connection);
        command.Parameters.AddWithValue("@nome", nome.Trim());
        command.Parameters.AddWithValue("@email", email.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task AlterarAcessoAsync(int id, string nivel, bool ativo, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand(
            "UPDATE usuarios SET nivel_acesso=@nivel, ativo=@ativo WHERE id=@id", connection);
        command.Parameters.AddWithValue("@nivel", NormalizarNivel(nivel));
        command.Parameters.AddWithValue("@ativo", ativo ? 1 : 0);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<bool> AlterarSenhaAsync(string codigo, string email, string senhaHash, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("""
            UPDATE usuarios SET senha_hash=@senha
            WHERE codigo=@codigo AND LOWER(email)=LOWER(@email) AND ativo=1
            """, connection);
        command.Parameters.AddWithValue("@senha", senhaHash);
        command.Parameters.AddWithValue("@codigo", codigo.Trim());
        command.Parameters.AddWithValue("@email", email.Trim());
        return await command.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task AlterarSenhaPorIdAsync(int id, string senhaHash, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand("UPDATE usuarios SET senha_hash=@senha WHERE id=@id", connection);
        command.Parameters.AddWithValue("@senha", senhaHash);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static Usuario Ler(DbDataReader reader) => new()
    {
        Id = reader.Number("id"),
        Codigo = reader.Text("codigo"),
        Nome = reader.Text("nome"),
        Email = reader.Text("email"),
        SenhaHash = reader.Text("senha_hash"),
        CategoriaId = reader.Number("categoria_id"),
        Categoria = reader.Text("categoria"),
        NivelAcesso = reader.Text("nivel_acesso"),
        Ativo = reader.Flag("ativo"),
        CriadoEm = reader.GetDateTime(reader.GetOrdinal("criado_em"))
    };

    private static string NormalizarNivel(string nivel) =>
        nivel.Trim().ToLowerInvariant() switch
        {
            "administrador" => "administrador",
            "operador" => "operador",
            _ => "consulta"
        };
}
