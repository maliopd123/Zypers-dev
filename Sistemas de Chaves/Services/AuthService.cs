using Microsoft.AspNetCore.Identity;
using SistemaDeChaves.DAO;
using SistemaDeChaves.Models;

namespace SistemaDeChaves.Services;

public sealed class AuthService
{
    private readonly UsuariosDAO _usuarios;
    private readonly IPasswordHasher<Usuario> _passwordHasher;

    public AuthService(UsuariosDAO usuarios, IPasswordHasher<Usuario> passwordHasher)
    {
        _usuarios = usuarios;
        _passwordHasher = passwordHasher;
    }

    public async Task<Usuario?> EntrarAsync(string codigo, string senha, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(senha))
            return null;

        var usuario = await _usuarios.ObterPorCodigoAsync(codigo, ct);
        if (usuario is null || !usuario.Ativo)
            return null;

        var resultado = _passwordHasher.VerifyHashedPassword(usuario, usuario.SenhaHash, senha);
        return resultado == PasswordVerificationResult.Failed ? null : usuario;
    }

    public string CriarHash(Usuario usuario, string senha) =>
        _passwordHasher.HashPassword(usuario, senha);
}
