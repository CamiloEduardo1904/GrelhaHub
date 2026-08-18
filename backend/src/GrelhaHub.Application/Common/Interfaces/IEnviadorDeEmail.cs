namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>Uma mensagem pronta para sair — <c>RN-GER-032</c>, ADR-0016.</summary>
/// <param name="Para">Destinatário.</param>
/// <param name="Assunto">Assunto.</param>
/// <param name="CorpoHtml">Corpo em HTML.</param>
/// <param name="CorpoTexto">
/// O mesmo conteúdo em texto puro. <b>Não é opcional por acessibilidade apenas:</b> mensagem só em
/// HTML é sinal clássico de spam para os filtros, e o link de recuperação que cai na lixeira é
/// indistinguível, para o usuário, de um link que nunca foi enviado.
/// </param>
public sealed record MensagemDeEmail(string Para, string Assunto, string CorpoHtml, string CorpoTexto);

/// <summary>
/// Enfileira e-mail transacional — <c>RN-GER-032</c>, ADR-0016.
/// <para>
/// ⚠ <b>O método não é <c>Async</c> e não devolve sucesso, de propósito.</b> Ele entrega a mensagem
/// a uma fila e retorna; quem chama <b>não pode</b> esperar o correio nem decidir com base nele.
/// Assinatura que devolvesse `Task&lt;bool&gt;` convidaria exatamente o que a regra proíbe: um
/// `await` dentro do handler, transformando a caixa de e-mail em dependência da criação de usuário.
/// </para>
/// <para>
/// O resultado do envio vive no <b>log</b>. Para o usuário, o caminho quando o e-mail não chega é
/// pedir outro link — e é por isso que todo e-mail nosso é reemitível (ADR-0016).
/// </para>
/// </summary>
public interface IEnviadorDeEmail
{
    void Enfileirar(MensagemDeEmail mensagem);

    /// <summary>
    /// <c>false</c> quando não há SMTP configurado — estado válido em desenvolvimento
    /// (<c>RN-GER-032</c>), e o que permite à tela dizer a verdade em vez de prometer um e-mail que
    /// nunca vai sair.
    /// </summary>
    bool Configurado { get; }

    /// <summary>
    /// Base pública das telas, para montar o link do e-mail.
    /// <para>
    /// ⚠ Vem de configuração, <b>nunca</b> do cabeçalho <c>Host</c> da requisição: montar link com o
    /// que o cliente mandou é o furo clássico da troca de senha — um <c>Host</c> forjado devolve um
    /// e-mail legítimo, do nosso domínio, com um link que entrega o token a outro servidor.
    /// </para>
    /// </summary>
    string BaseDoSite { get; }

    /// <summary>
    /// O CNPJ de quem assina o produto, para o rodapé legal do e-mail.
    /// <para>
    /// ⚠ Vem de configuração e <b>não</b> de constante no modelo, pelo mesmo motivo que os dados da
    /// vitrine vivem em <c>conteudo.ts</c>: identidade jurídica muda por decisão societária, não
    /// por deploy de funcionalidade — e quem a atualiza não deveria precisar recompilar. É o mesmo
    /// número do rodapé da vitrine; divergir entre a página e o e-mail é o tipo de defeito que só
    /// aparece quando alguém confere um contrato.
    /// </para>
    /// </summary>
    string Cnpj { get; }
}
