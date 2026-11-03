using SistemaDeChaves.Models;

namespace SistemaDeChaves.DAO;

public sealed class RelatoriosDAO
{
    private readonly MovimentacoesDAO _movimentacoes;

    public RelatoriosDAO(MovimentacoesDAO movimentacoes) => _movimentacoes = movimentacoes;

    public Task<List<Movimentacao>> GerarMovimentacoesAsync(
        DateTime? inicio,
        DateTime? fim,
        string busca = "",
        CancellationToken ct = default) =>
        _movimentacoes.ListarAsync("historico", busca, inicio, fim, ct);
}
