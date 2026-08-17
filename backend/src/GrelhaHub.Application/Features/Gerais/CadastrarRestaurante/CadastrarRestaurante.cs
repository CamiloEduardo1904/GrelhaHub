using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Cardapio;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Features.Gerais.CadastrarRestaurante;

/// <summary>
/// Cadastra o restaurante e o seu usuário administrador — <c>SPEC-VEN-001</c>.
/// </summary>
public sealed record CadastrarRestauranteCommand(
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string? Telefone,
    string NomeAdministrador,
    string Email,
    string Senha) : IRequest<CadastrarRestauranteResponse>;

public sealed record CadastrarRestauranteResponse(
    Guid UnidadeId,
    string NomeFantasia,
    string Slug,
    string LinkDoCardapio,
    Guid UsuarioId);

public sealed class CadastrarRestauranteValidator : AbstractValidator<CadastrarRestauranteCommand>
{
    public CadastrarRestauranteValidator()
    {
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NomeFantasia).NotEmpty().MinimumLength(3).MaximumLength(200);

        RuleFor(x => x.Cnpj)
            .NotEmpty()
            .Must(c => c.Count(char.IsDigit) == 14)
            .WithMessage("O CNPJ deve ter 14 dígitos.");

        RuleFor(x => x.NomeAdministrador).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();

        RuleFor(x => x.Senha)
            .NotEmpty()
            .MinimumLength(8).WithMessage("A senha deve ter ao menos 8 caracteres.");
    }
}

public sealed class CadastrarRestauranteHandler(IAppDbContext db, IHashDeSenha hash)
    : IRequestHandler<CadastrarRestauranteCommand, CadastrarRestauranteResponse>
{
    public async Task<CadastrarRestauranteResponse> Handle(
        CadastrarRestauranteCommand request, CancellationToken cancellationToken)
    {
        var cnpj = new string(request.Cnpj.Where(char.IsDigit).ToArray());

        var cnpjExiste = await db.Unidades.AnyAsync(u => u.Cnpj == cnpj, cancellationToken);
        if (cnpjExiste)
        {
            throw new RegraDeNegocioException("Já existe um restaurante com este CNPJ.");
        }

        var slug = await GerarSlugDisponivelAsync(request.NomeFantasia, cancellationToken);

        var unidade = new Unidade(
            request.RazaoSocial, request.NomeFantasia, cnpj, slug, request.Telefone);

        db.Unidades.Add(unidade);

        var admin = new Usuario(
            unidade.Id,
            request.NomeAdministrador,
            request.Email,
            hash.Gerar(request.Senha),
            PerfilUsuario.Administrador);

        db.Usuarios.Add(admin);

        SemearCardapioDeExemplo(db, unidade.Id);

        await db.SaveChangesAsync(cancellationToken);

        return new CadastrarRestauranteResponse(
            unidade.Id, unidade.NomeFantasia, unidade.Slug, $"/c/{unidade.Slug}", admin.Id);
    }

    /// <summary>
    /// <c>RN-VEN-036</c> — o slug é único em todo o sistema. Deriva do nome fantasia e recebe
    /// sufixo numérico quando já existir.
    /// </summary>
    private async Task<string> GerarSlugDisponivelAsync(string nomeFantasia, CancellationToken cancellationToken)
    {
        var baseSlug = Sluguificar(nomeFantasia);
        var candidato = baseSlug;
        var sufixo = 1;

        while (await db.Unidades.AnyAsync(u => u.Slug == candidato, cancellationToken))
        {
            sufixo++;
            candidato = $"{baseSlug}-{sufixo}";
        }

        return candidato;
    }

    /// <summary>
    /// "Brasa &amp; Fogo Burger" → "brasa-fogo-burger".
    /// Remove acento por normalização Unicode, não por tabela de substituição.
    /// </summary>
    internal static string Sluguificar(string texto)
    {
        var semAcento = new string(texto
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray())
            .Normalize(NormalizationForm.FormC);

        var slug = Regex.Replace(semAcento.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

        if (slug.Length > 50)
        {
            slug = slug[..50].Trim('-');
        }

        // O construtor de Unidade exige no mínimo 3 caracteres; um nome só de símbolos
        // resultaria em slug vazio.
        return slug.Length < 3 ? $"restaurante-{Guid.CreateVersion7().ToString()[..8]}" : slug;
    }

    /// <summary>
    /// Cardápio de exemplo, para que o link público já mostre alguma coisa logo após o
    /// cadastro. Sem isso, o primeiro acesso ao cardápio devolve uma tela vazia e parece
    /// defeito.
    /// </summary>
    private static void SemearCardapioDeExemplo(IAppDbContext db, Guid unidadeId)
    {
        var lanches = new Categoria(unidadeId, "Lanches", 1);
        var acompanhamentos = new Categoria(unidadeId, "Acompanhamentos", 2);
        var bebidas = new Categoria(unidadeId, "Bebidas", 3);

        db.Categorias.AddRange(lanches, acompanhamentos, bebidas);

        db.Produtos.AddRange(
            new Produto(unidadeId, lanches.Id, "X-Burger",
                "Pão brioche, hambúrguer 150g, queijo prato, alface e tomate", 28.00m),
            new Produto(unidadeId, lanches.Id, "X-Bacon",
                "Pão brioche, hambúrguer 150g, queijo cheddar e bacon crocante", 34.00m),
            new Produto(unidadeId, lanches.Id, "Duplo Smash",
                "Dois hambúrgueres smash 90g, queijo cheddar e molho da casa", 39.90m),
            new Produto(unidadeId, acompanhamentos.Id, "Batata frita",
                "Porção individual com sal e alecrim", 18.00m),
            new Produto(unidadeId, acompanhamentos.Id, "Onion rings",
                "Anéis de cebola empanados", 22.00m),
            new Produto(unidadeId, bebidas.Id, "Refrigerante lata", "350ml", 7.00m),
            new Produto(unidadeId, bebidas.Id, "Suco natural", "Laranja ou limão, 400ml", 12.00m));
    }
}
