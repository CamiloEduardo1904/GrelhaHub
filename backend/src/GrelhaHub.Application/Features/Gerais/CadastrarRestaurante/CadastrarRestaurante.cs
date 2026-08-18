using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using GrelhaHub.Application.Common.Exceptions;
using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Common;
using GrelhaHub.Domain.Cardapio;
using GrelhaHub.Domain.Gerais;
using MediatR;
using Microsoft.EntityFrameworkCore;
using GrelhaHub.Application.Common.Modulos;

namespace GrelhaHub.Application.Features.Gerais.CadastrarRestaurante;

/// <summary>
/// Cadastra o restaurante e o seu usuário administrador — <c>SPEC-VEN-001</c>.
/// </summary>
[SemModulo("cria a própria unidade — não há unidade cujos módulos consultar antes dela existir")]
[SemPermissao("cria a unidade e o seu primeiro administrador — não existe perfil antes dela")]
public sealed record CadastrarRestauranteCommand(
    string RazaoSocial,
    string NomeFantasia,
    string Cnpj,
    string? Telefone,
    string NomeAdministrador,

    // RN-GER-023 — a pessoa passa a ser identificada, e não só nomeada.
    string SobrenomeAdministrador,
    string CpfAdministrador,

    string Email,
    string Senha,

    /// <summary>
    /// <c>RN-GER-017</c> — módulos a habilitar. **Só a rota de plataforma informa**: quem
    /// cadastra pelo telefone já sabe o que foi vendido. Pela rota anônima vem nulo e a unidade
    /// nasce com o padrão (`SPEC-GER-007` §4).
    /// </summary>
    IReadOnlyList<CodigoDeModulo>? Modulos = null) : IRequest<CadastrarRestauranteResponse>;

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
        RuleFor(x => x.SobrenomeAdministrador).NotEmpty().MaximumLength(200);

        // RN-GER-023 — dígito verificador conferido pelo value object. Aqui só a mensagem.
        RuleFor(x => x.CpfAdministrador)
            .NotEmpty()
            .Must(cpf => Cpf.TentarDe(cpf, out _))
            .WithMessage("O CPF informado não é válido.");

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

        // RN-GER-023 — o CPF do fundador. Único em todo o sistema: a mesma pessoa não abre dois
        // restaurantes com dois cadastros dela mesma. `IgnoreQueryFilters` não é necessário aqui
        // porque `usuario` está fora do filtro global (não implementa IPertenceAUnidade), mas o
        // e-mail abaixo segue a mesma checagem — as duas colidiriam na constraint sem mensagem.
        var cpf = Cpf.De(request.CpfAdministrador);

        var cpfEmUso = await db.Usuarios.AnyAsync(u => u.Cpf == cpf, cancellationToken);
        if (cpfEmUso)
        {
            throw new RegraDeNegocioException("Já existe um usuário com este CPF.");
        }

        // Normalizado FORA da expressão da consulta: `ToLowerInvariant` dentro do `Where` viraria
        // `lower()` no SQL e descartaria o índice único de e-mail.
        var emailNormalizado = request.Email.Trim().ToLowerInvariant();

        var emailEmUso = await db.Usuarios
            .AnyAsync(u => u.Email == emailNormalizado, cancellationToken);
        if (emailEmUso)
        {
            throw new RegraDeNegocioException("Já existe um usuário com este e-mail.");
        }

        var slug = await GerarSlugDisponivelAsync(request.NomeFantasia, cancellationToken);

        var unidade = new Unidade(
            request.RazaoSocial, request.NomeFantasia, cnpj, slug, request.Telefone);

        db.Unidades.Add(unidade);

        // RN-GER-010 — os perfis da unidade e as suas permissoes, ANTES do usuario: nao existe
        // usuario sem perfil, e o administrador precisa do dele para ser criado.
        var perfis = await SemearPerfisAsync(db, unidade.Id, cancellationToken);

        var admin = new Usuario(
            unidade.Id,
            request.NomeAdministrador,
            request.Email,
            hash.Gerar(request.Senha),
            perfis[nameof(PerfilUsuario.Administrador)],

            // O administrador fundador NAO precisa trocar a senha: foi ele quem a escolheu no
            // cadastro. RF-SEG-005 vale para usuario criado POR outra pessoa, que e o caso em
            // que a senha inicial e conhecida por quem nao vai usa-la.
            deveTrocarSenha: false,
            sobrenome: request.SobrenomeAdministrador,
            cpf: cpf);

        db.Usuarios.Add(admin);

        await SemearParametrosAsync(db, unidade.Id, cancellationToken);
        await SemearModulosAsync(db, unidade.Id, request.Modulos, cancellationToken);

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
    /// Valores iniciais dos parâmetros para a unidade nova — <c>SPEC-GER-003</c> CA-7.
    /// <para>
    /// Sem isto, o primeiro cálculo que precisasse de taxa de serviço ou de teto de desconto
    /// falharia no meio do serviço, com um parâmetro que existe no catálogo e não tem valor.
    /// A vigência começa em <c>DateTimeOffset.MinValue</c> de propósito: qualquer consulta em
    /// qualquer data encontra valor, inclusive apuração de dado importado de antes do cadastro.
    /// </para>
    /// </summary>
    private static async Task SemearParametrosAsync(
        IAppDbContext db, Guid unidadeId, CancellationToken cancellationToken)
    {
        var definicoes = CatalogoDeParametros.Todos;
        var codigos = definicoes.Select(definicao => definicao.Codigo).ToList();

        var doCatalogo = await db.Parametros
            .AsNoTracking()
            .Where(parametro => codigos.Contains(parametro.Codigo))
            .ToDictionaryAsync(parametro => parametro.Codigo, cancellationToken);

        foreach (var definicao in definicoes)
        {
            if (!doCatalogo.TryGetValue(definicao.Codigo, out var parametro))
            {
                // Catálogo do C# e tabela do banco divergiram: a migration não semeou este
                // código. Falhar aqui é melhor que criar um restaurante pela metade.
                throw new InvalidOperationException(
                    $"O parâmetro {definicao.Codigo} está em CatalogoDeParametros e não existe " +
                    "na tabela parametro. Falta a migration que o semeia.");
            }

            db.ValoresDeParametro.Add(new ParametroValor(
                parametro.Id,
                unidadeId,
                definicao.ValorInicial,
                DateTimeOffset.MinValue,
                "Valor inicial da carga, conforme docs/04-parametros-do-sistema.md"));
        }
    }

    /// <summary>
    /// <summary>
    /// Perfis da unidade e as suas permissoes — <c>RN-GER-010</c>, <c>RN-GER-022</c>.
    /// <para>
    /// Os perfis sao <b>por unidade</b>: cada restaurante tem os seus, e um dia pode criar mais.
    /// As permissoes sao <b>globais</b> — o catalogo do que existe no sistema, semeado por
    /// migration.
    /// </para>
    /// <para>
    /// Devolve o mapa nome -> id, porque quem chama precisa do id do Administrador para criar o
    /// usuario fundador.
    /// </para>
    /// </summary>
    private static async Task<Dictionary<string, Guid>> SemearPerfisAsync(
        IAppDbContext db, Guid unidadeId, CancellationToken cancellationToken)
    {
        var permissoes = await db.Permissoes
            .AsNoTracking()
            .ToDictionaryAsync(permissao => permissao.Codigo, permissao => permissao.Id, cancellationToken);

        if (permissoes.Count == 0)
        {
            throw new InvalidOperationException(
                "SPEC-GER-004 — a tabela permissao esta vazia. Falta a migration que a semeia.");
        }

        var criados = new Dictionary<string, Guid>(StringComparer.Ordinal);

        foreach (var padrao in CatalogoDeAcesso.Perfis)
        {
            var perfil = new Perfil(unidadeId, padrao.Nome, padrao.Descricao, sistema: true);

            foreach (var codigo in padrao.Permissoes)
            {
                if (!permissoes.TryGetValue(codigo, out var permissaoId))
                {
                    // Catalogo do C# e tabela do banco divergiram. Falhar aqui e melhor que criar
                    // um perfil silenciosamente sem uma das permissoes que ele deveria ter.
                    throw new InvalidOperationException(
                        $"SPEC-GER-004 — a permissao {codigo}, exigida pelo perfil {padrao.Nome}, " +
                        "nao existe na tabela permissao. Falta a migration que a semeia.");
                }

                perfil.Conceder(permissaoId);
            }

            db.Perfis.Add(perfil);
            criados[padrao.Nome] = perfil.Id;
        }

        return criados;
    }

    /// Módulos com que a unidade nasce — <c>RN-GER-017</c>, <c>SPEC-GER-006</c>.
    /// <para>
    /// <b>GER e VEN</b>: o mínimo para vender e operar. GER é núcleo (<c>RN-GER-019</c>) e
    /// consta aqui de propósito, para que a linha exista e a cobrança saiba desde quando —
    /// <c>IModulos</c> o considera habilitado mesmo sem linha, mas o histórico é do comercial.
    /// </para>
    /// <para>
    /// O resto é venda: PRO, EST, FIN, CRM, LOG, RH e FIS são habilitados pela plataforma
    /// quando contratados (<c>RN-GER-014</c>).
    /// </para>
    /// </summary>
    private static async Task SemearModulosAsync(
        IAppDbContext db,
        Guid unidadeId,
        IReadOnlyList<CodigoDeModulo>? pedidos,
        CancellationToken cancellationToken)
    {
        // O padrão do auto-serviço. RN-GER-019 — GER é núcleo e entra sempre.
        CodigoDeModulo[] padrao = [CodigoDeModulo.Gerais, CodigoDeModulo.Vendas];

        // SPEC-GER-007 §4 — a rota de plataforma informa o que foi vendido. GER é acrescentado de
        // qualquer forma (RN-GER-019): uma unidade sem GER não autentica, não tem parâmetro e não
        // audita, e vender isso seria vender um sistema que não liga.
        var inclusos = pedidos is { Count: > 0 }
            ? pedidos.Append(CodigoDeModulo.Gerais).Distinct().ToArray()
            : padrao;

        var doCatalogo = await db.Modulos
            .AsNoTracking()
            .Where(modulo => inclusos.Contains(modulo.Codigo))
            .ToListAsync(cancellationToken);

        if (doCatalogo.Count != inclusos.Length)
        {
            var ausentesNoBanco = inclusos
                .Except(doCatalogo.Select(m => m.Codigo))
                .Select(Modulo.Sigla);

            throw new InvalidOperationException(
                $"SPEC-GER-006 — a tabela modulo não tem: {string.Join(", ", ausentesNoBanco)}. " +
                "Falta a migration que os semeia.");
        }

        // RN-GER-020 — o conjunto pedido precisa ser FECHADO sob dependência.
        //
        // Mesma função que `AlterarModuloDaUnidadeCommand` usa. Recusar em vez de completar a
        // lista sozinho é decisão: acrescentar PRO porque alguém pediu EST seria o sistema
        // **vendendo um módulo que ninguém contratou** — a fatura não fecharia com o contrato, e o
        // erro apareceria na cobrança, não aqui.
        //
        // Exceção: o núcleo (RN-GER-019) já é considerado presente pelo fecho, então VEN nunca
        // reclama de GER.
        var arestas = await db.DependenciasDeModulo.AsNoTracking().ToListAsync(cancellationToken);

        var faltando = FechoDeDependencia.Faltando(
            await db.Modulos.AsNoTracking().ToListAsync(cancellationToken),
            arestas,
            doCatalogo.Select(m => m.Id).ToList());

        if (faltando.Count > 0)
        {
            throw new RegraDeNegocioException(
                $"Faltam módulos de que os escolhidos dependem: {string.Join(", ", faltando)}. " +
                "Acrescente-os ao contrato ou remova quem depende deles.",
                "RN-GER-020");
        }

        foreach (var modulo in doCatalogo)
        {
            db.ModulosDaUnidade.Add(new UnidadeModulo(
                unidadeId,
                modulo.Id,
                DateTimeOffset.UtcNow,
                pedidos is { Count: > 0 }
                    ? "Habilitado na abertura do restaurante, conforme o contrato"
                    : "Habilitado na abertura do restaurante"));
        }
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
                "Pão brioche, hambúrguer 150g, queijo prato, alface e tomate", Dinheiro.De(28.00m)),
            new Produto(unidadeId, lanches.Id, "X-Bacon",
                "Pão brioche, hambúrguer 150g, queijo cheddar e bacon crocante", Dinheiro.De(34.00m)),
            new Produto(unidadeId, lanches.Id, "Duplo Smash",
                "Dois hambúrgueres smash 90g, queijo cheddar e molho da casa", Dinheiro.De(39.90m)),
            new Produto(unidadeId, acompanhamentos.Id, "Batata frita",
                "Porção individual com sal e alecrim", Dinheiro.De(18.00m)),
            new Produto(unidadeId, acompanhamentos.Id, "Onion rings",
                "Anéis de cebola empanados", Dinheiro.De(22.00m)),
            new Produto(unidadeId, bebidas.Id, "Refrigerante lata", "350ml", Dinheiro.De(7.00m)),
            new Produto(unidadeId, bebidas.Id, "Suco natural", "Laranja ou limão, 400ml", Dinheiro.De(12.00m)));
    }
}
