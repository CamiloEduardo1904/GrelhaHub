using GrelhaHub.Domain.Common;

namespace GrelhaHub.Domain.Gerais;

/// <summary>
/// <c>RN-GER-010</c> — todo usuário pertence a um perfil, e o perfil determina quais
/// operações ele pode realizar.
/// <para>
/// <b>Simplificação declarada</b> (SPEC-VEN-001 §8): nesta fatia o perfil é um <c>enum</c>.
/// O modelo definitivo tem tabela de perfis com permissões granulares — ver
/// <c>SPEC-GER-004</c>. Verificar o nome do perfil espalhado pelo código é justamente o que
/// a versão final elimina.
/// </para>
/// </summary>
public enum PerfilUsuario
{
    Administrador = 1,
    Gerente = 2,
    Atendente = 3,
    Cozinha = 4,
}

public sealed class Usuario : Entidade, IPertenceAUnidade
{
    private Usuario() { }

    public Usuario(Guid unidadeId, string nome, string email, string senhaHash, PerfilUsuario perfil)
    {
        UnidadeId = unidadeId;
        Nome = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome é obrigatório.", nameof(nome))
            : nome.Trim();
        Email = string.IsNullOrWhiteSpace(email)
            ? throw new ArgumentException("O e-mail é obrigatório.", nameof(email))
            : email.Trim().ToLowerInvariant();
        SenhaHash = senhaHash;
        Perfil = perfil;
        CriadoEm = DateTimeOffset.UtcNow;
    }

    public Guid UnidadeId { get; private set; }
    public string Nome { get; private set; } = null!;
    public string Email { get; private set; } = null!;

    /// <summary><c>RNF-005</c> — hash com salt. Nunca a senha em texto claro.</summary>
    public string SenhaHash { get; private set; } = null!;

    public PerfilUsuario Perfil { get; private set; }
    public bool Ativo { get; private set; } = true;
    public DateTimeOffset CriadoEm { get; private set; }

    public Unidade? Unidade { get; private set; }

    /// <summary><c>RN-GER-002</c> — não se exclui; inativa-se.</summary>
    public void Inativar() => Ativo = false;

    public void TrocarSenha(string novoHash) => SenhaHash = novoHash;

    /// <summary>
    /// <c>RN-GER-012</c> — custo, CMV e margem são visíveis apenas a perfis de gerência.
    /// A checagem vive aqui para que a projeção da query não repita a lista de perfis.
    /// </summary>
    public bool PodeVerCusto => Perfil is PerfilUsuario.Administrador or PerfilUsuario.Gerente;

    public bool EhGerencia => PodeVerCusto;
}
