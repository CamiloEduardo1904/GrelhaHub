using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Gerais.Autenticar;

public sealed record AutenticarCommand(string Email, string Senha) : IRequest<AutenticarResponse>;

public sealed record AutenticarResponse(
    string Token,
    DateTimeOffset ExpiraEm,
    Guid UsuarioId,
    string Nome,
    string Perfil,
    Guid UnidadeId,
    string Restaurante,
    string Slug);

public sealed class AutenticarValidator : AbstractValidator<AutenticarCommand>
{
    public AutenticarValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("Informe um e-mail válido.");
        RuleFor(x => x.Senha).NotEmpty().WithMessage("Informe a senha.");
    }
}

public sealed class AutenticarHandler(IAppDbContext db, IHashDeSenha hash, IGeradorDeToken tokens)
    : IRequestHandler<AutenticarCommand, AutenticarResponse>
{
    public async Task<AutenticarResponse> Handle(AutenticarCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // IgnoreQueryFilters: no login ainda não há unidade no contexto — é justamente o
        // token que vai defini-la. O e-mail é único por unidade, então o filtro global
        // recusaria tudo antes de haver o que filtrar.
        var usuario = await db.Usuarios
            .IgnoreQueryFilters()
            .Include(u => u.Unidade)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // RN-RH-006 — usuário inativo não registra operação no sistema.
        // A mensagem é a MESMA de credencial errada, de propósito: diferenciá-las
        // confirmaria a existência da conta para quem está tentando adivinhar.
        if (usuario is null || !usuario.Ativo || !hash.Confere(request.Senha, usuario.SenhaHash))
        {
            throw new NaoAutorizadoException("E-mail ou senha inválidos.");
        }

        if (usuario.Unidade is null || !usuario.Unidade.Ativa)
        {
            throw new NaoAutorizadoException("E-mail ou senha inválidos.");
        }

        var (token, expiraEm) = tokens.Gerar(usuario);

        return new AutenticarResponse(
            token, expiraEm, usuario.Id, usuario.Nome, usuario.Perfil.ToString(),
            usuario.UnidadeId, usuario.Unidade.NomeFantasia, usuario.Unidade.Slug);
    }
}
