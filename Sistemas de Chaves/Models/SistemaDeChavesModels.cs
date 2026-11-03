using System.ComponentModel.DataAnnotations;

namespace SistemaDeChaves.Models;

public sealed class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public bool Ativa { get; set; } = true;
}

public sealed class Usuario
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Informe o código único.")]
    public string Codigo { get; set; } = "";
    [Required(ErrorMessage = "Informe o nome.")]
    public string Nome { get; set; } = "";
    [Required, EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    public string Email { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public int CategoriaId { get; set; }
    public string Categoria { get; set; } = "";
    public string NivelAcesso { get; set; } = "consulta";
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
}

public sealed class Chave
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Informe o código da chave.")]
    public string Codigo { get; set; } = "";
    [Required(ErrorMessage = "Informe a descrição.")]
    public string Descricao { get; set; } = "";
    [Required(ErrorMessage = "Informe o local.")]
    public string Local { get; set; } = "";
    [Range(1, int.MaxValue, ErrorMessage = "Selecione uma categoria.")]
    public int CategoriaId { get; set; }
    public string Categoria { get; set; } = "";
    public DateTime DataCadastro { get; set; }
    public string Observacoes { get; set; } = "";
    public string Situacao { get; set; } = "disponivel";
    public string? CodigoUsuario { get; set; }
    public string? NomeUsuario { get; set; }
    public DateTime? PrevistaDevolucao { get; set; }
    public int MinutosAtraso { get; set; }
}

public sealed class Movimentacao
{
    public int Id { get; set; }
    public int ChaveId { get; set; }
    public string CodigoChave { get; set; } = "";
    public string DescricaoChave { get; set; } = "";
    public string LocalChave { get; set; } = "";
    public string CodigoUsuario { get; set; } = "";
    public string NomeUsuario { get; set; } = "";
    public DateTime RetiradaEm { get; set; }
    public DateTime PrevistaDevolucao { get; set; }
    public DateTime? DevolvidaEm { get; set; }
    public string Observacoes { get; set; } = "";
    public string ObservacoesDevolucao { get; set; } = "";
    public string Situacao { get; set; } = "";
}

public sealed class AlertaAtraso
{
    public int Id { get; set; }
    public int MovimentacaoId { get; set; }
    public string CodigoChave { get; set; } = "";
    public string CodigoUsuario { get; set; } = "";
    public string NomeUsuario { get; set; } = "";
    public DateTime PrevistaDevolucao { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime? LidoEm { get; set; }
    public int MinutosAtraso { get; set; }
}

public sealed class ConfiguracaoSistema
{
    public string NomeSistema { get; set; } = "Sistema de Chaves";
    public int MinutosParaAlerta { get; set; } = 10;
}

public sealed class ResumoPainel
{
    public int TotalChaves { get; set; }
    public int Disponiveis { get; set; }
    public int EmUso { get; set; }
    public int EmAtraso { get; set; }
    public IReadOnlyList<Movimentacao> MovimentacoesRecentes { get; set; } = [];
    public IReadOnlyList<AlertaAtraso> Alertas { get; set; } = [];
}
