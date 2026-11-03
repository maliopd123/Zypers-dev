using MySql.Data.MySqlClient;
using SistemaDeChaves.Models;

namespace SistemaDeChaves.DAO;

public sealed class CategoriasDAO
{
    private readonly DatabaseConnection _database;

    public CategoriasDAO(DatabaseConnection database) => _database = database;

    public async Task<List<Categoria>> ListarUsuariosAsync(CancellationToken ct = default)
    {
        var categorias = new List<Categoria>();
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand(
            "SELECT id, nome, ativa FROM categorias_usuario WHERE ativa=1 ORDER BY nome", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            categorias.Add(new Categoria { Id = reader.Number("id"), Nome = reader.Text("nome"), Ativa = reader.Flag("ativa") });
        return categorias;
    }

    public async Task<List<Categoria>> ListarChavesAsync(CancellationToken ct = default)
    {
        var categorias = new List<Categoria>();
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand(
            "SELECT id, nome, ativa FROM categorias_chave WHERE ativa=1 ORDER BY nome", connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            categorias.Add(new Categoria { Id = reader.Number("id"), Nome = reader.Text("nome"), Ativa = reader.Flag("ativa") });
        return categorias;
    }

    public async Task SalvarUsuarioAsync(string nome, int? id = null, CancellationToken ct = default)
    {
        ValidarNome(nome);
        await using var connection = await _database.OpenAsync(ct);
        var sql = id.HasValue
            ? "UPDATE categorias_usuario SET nome=@nome, ativa=1 WHERE id=@id"
            : "INSERT INTO categorias_usuario (nome, ativa) VALUES (@nome, 1)";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@nome", nome.Trim());
        if (id.HasValue)
            command.Parameters.AddWithValue("@id", id.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task SalvarChaveAsync(string nome, int? id = null, CancellationToken ct = default)
    {
        ValidarNome(nome);
        await using var connection = await _database.OpenAsync(ct);
        var sql = id.HasValue
            ? "UPDATE categorias_chave SET nome=@nome, ativa=1 WHERE id=@id"
            : "INSERT INTO categorias_chave (nome, ativa) VALUES (@nome, 1)";
        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@nome", nome.Trim());
        if (id.HasValue)
            command.Parameters.AddWithValue("@id", id.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task InativarUsuarioAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand(
            "UPDATE categorias_usuario SET ativa=0 WHERE id=@id", connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task InativarChaveAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await _database.OpenAsync(ct);
        await using var command = new MySqlCommand(
            "UPDATE categorias_chave SET ativa=0 WHERE id=@id", connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static void ValidarNome(string nome)
    {
        if (nome.Trim().Length is < 2 or > 80)
            throw new ArgumentException("O nome deve ter entre 2 e 80 caracteres.", nameof(nome));
    }
}
