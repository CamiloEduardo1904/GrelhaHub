using GrelhaHub.Application.Common.Interfaces;
using GrelhaHub.Domain.Gerais;

namespace GrelhaHub.Application.Features.Gerais.Acesso;

/// <summary>
/// O modelo do e-mail de acesso — convite e redefinição, ADR-0012 (direção Chapa), ADR-0016.
/// <para>
/// <b>Separado do emissor, e <c>static</c> com parâmetros primitivos, por um motivo prático:</b>
/// é o que permite gerar uma prévia do e-mail sem banco, sem SMTP e sem subir a API — e a prévia
/// passa a usar <b>este</b> código, não uma cópia que envelhece em silêncio. Modelo de e-mail é a
/// superfície que menos se olha e mais se quebra: ninguém abre a caixa de entrada num teste.
/// </para>
/// <para>
/// ⚠ <b>As três restrições que mandam aqui, e que não valem em nenhuma outra tela do produto:</b>
/// </para>
/// <list type="number">
/// <item>
/// <b>Nada de folha de estilo.</b> Gmail e Outlook descartam <c>&lt;style&gt;</c>, classe e
/// seletor. Tudo é atributo <c>style</c> na própria tag — e é por isso que os tokens da Chapa
/// aparecem como constante de cor aqui em cima, em vez de variável CSS.
/// </item>
/// <item>
/// <b>Layout em tabela.</b> O motor do Outlook para desktop é o do Word: <c>flex</c> e
/// <c>grid</c> não existem, e <c>div</c> com largura máxima vira largura da janela.
/// </item>
/// <item>
/// <b>Nenhuma imagem.</b> A maioria dos clientes bloqueia imagem por padrão, e marca desenhada em
/// imagem some junto — o que sobra é uma moldura vazia. A listra e a wordmark são <b>tabela e
/// texto</b>, então chegam inteiras mesmo com o bloqueio ligado.
/// </item>
/// </list>
/// <para>
/// <c>box-shadow</c> é a única declaração que aceito ver ignorada: Gmail desenha, Outlook não. A
/// borda de 2px continua nos dois — o cartão degrada para "sem sombra", nunca para "sem moldura".
/// É a razão de a sombra ser acréscimo e jamais o que separa uma superfície da outra
/// (<c>CHAPA-01</c>).
/// </para>
/// </summary>
public static class ModeloDeEmailDeAcesso
{
    // Os tokens da §2.1 do design system, resolvidos. Valor literal porque não há CSS onde
    // declarar variável — e é justamente por isso que ficam nomeados num lugar só.
    private const string Carvao = "#14100c";
    private const string Papel = "#ebe2d6";
    private const string PapelAlto = "#f8f3ec";
    private const string Creme = "#ffe3d3";
    private const string CinzaQuente = "#9c8e7e";
    private const string Brasa = "#ff6b2c";
    private const string Ambar = "#ffb020";
    private const string Tinta = "#14100c";
    private const string TintaFraca = "#544a40";

    /// <summary>
    /// A barra ainda não percorrida. Na aplicação ela é <c>--text</c> com <c>opacity: 0.28</c>;
    /// aqui é o <b>resultado composto</b> dessa mistura sobre o carvão, calculado uma vez — cliente
    /// de e-mail não é confiável com <c>opacity</c>, e a listra sumiria em alguns deles.
    /// </summary>
    private const string BarraApagada = "#564b44";

    private const string Peso = "'Arial Black','Helvetica Neue',system-ui,sans-serif";
    private const string Mono = "ui-monospace,'Cascadia Mono','Segoe UI Mono','SF Mono',Menlo,Consolas,monospace";
    private const string Sans = "system-ui,-apple-system,'Segoe UI',Roboto,Arial,sans-serif";

    /// <summary>A largura de leitura: 600px é o que o painel de visualização do Outlook mostra sem cortar.</summary>
    private const int Largura = 600;

    /// <summary>
    /// O alvo de toque do botão — <c>CHAPA-04</c>, 48px.
    /// <para>
    /// ⚠ Somado de <c>padding</c> e <c>line-height</c>, e <b>não</b> declarado em
    /// <c>min-height</c>: `min-height` é altura da CAIXA DE CONTEÚDO, então
    /// `min-height:48px` com 15px de padding em cima e embaixo dá um botão de 78px — grande
    /// demais, e sem que nenhum número no código diga 78. 15 + 18 + 15 fecha 48 exatos.
    /// </para>
    /// </summary>
    private const string AlturaDoBotao = "padding:15px 26px;line-height:18px";

    /// <summary>
    /// Monta a mensagem pronta. Recebe tudo por parâmetro — nenhum relógio, nenhuma consulta: a
    /// mesma entrada dá sempre a mesma saída, que é o que torna a prévia honesta.
    /// </summary>
    /// <param name="validade">Já formatado — "72 horas", "60 minutos". Quem sabe o prazo é o emissor.</param>
    public static MensagemDeEmail Montar(
        string para,
        string nome,
        TipoDeToken tipo,
        string link,
        string validade,
        string cnpj)
    {
        var convite = tipo == TipoDeToken.Convite;

        var assunto = convite ? "O seu acesso ao GrelhaHub" : "Redefinir a sua senha do GrelhaHub";
        var rotulo = convite ? "Primeiro acesso" : "Redefinir senha";
        var acao = convite ? "Definir a minha senha" : "Escolher uma senha nova";

        var chamada = convite
            ? "Uma conta foi criada para você no GrelhaHub. Falta só escolher a sua senha para entrar."
            : "Recebemos um pedido para redefinir a sua senha. Escolha uma nova para voltar a entrar.";

        // O pré-cabeçalho: o trecho que a lista de mensagens mostra ao lado do assunto, ANTES de
        // alguém abrir. Sem ele o cliente pega a primeira frase visível — que aqui seria "Olá,
        // Fulano." — e a linha da caixa de entrada deixa de dizer o que a mensagem quer.
        var previa = $"O link vale por {validade} e só pode ser usado uma vez.";

        var html = $"""
            <div style="display:none;max-height:0;overflow:hidden;mso-hide:all">{previa}</div>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="{Papel}" style="background:{Papel};width:100%;margin:0;padding:24px 12px">
              <tr>
                <td align="center" style="padding:0">
                  <table role="presentation" width="{Largura}" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:{Largura}px;text-align:left">

                    <tr>
                      <td bgcolor="{Carvao}" style="background:{Carvao};padding:26px 28px 22px">
                        {Listra(convite ? 2 : 3)}
                        <div style="margin:16px 0 0;font-family:{Peso};font-size:27px;line-height:1.1;letter-spacing:-0.04em;color:{Creme}">Grelha<span style="color:{Brasa}">Hub</span></div>
                        <div style="margin:8px 0 0;font-family:{Mono};font-size:11px;letter-spacing:0.16em;text-transform:uppercase;color:{CinzaQuente}">{rotulo}</div>
                      </td>
                    </tr>

                    <tr>
                      <td style="padding:22px 0 0">
                        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="{PapelAlto}" style="width:100%;background:{PapelAlto};border:2px solid {Carvao};box-shadow:5px 5px 0 {Carvao}">
                          <tr>
                            <td style="padding:26px 28px">
                              <p style="margin:0;font-family:{Sans};font-size:16px;line-height:1.55;color:{Tinta}">Olá, {nome}.</p>
                              <p style="margin:12px 0 0;font-family:{Sans};font-size:16px;line-height:1.55;color:{Tinta}">{chamada}</p>

                              <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:22px 0 0">
                                <tr>
                                  <td bgcolor="{Brasa}" style="background:{Brasa};border:2px solid {Carvao};box-shadow:3px 3px 0 {Carvao}">
                                    <a href="{link}" style="display:block;{AlturaDoBotao};font-family:{Mono};font-size:13px;font-weight:bold;letter-spacing:0.1em;text-transform:uppercase;color:#ffffff;text-decoration:none">{acao}</a>
                                  </td>
                                </tr>
                              </table>

                              <p style="margin:22px 0 0;font-family:{Sans};font-size:15px;line-height:1.55;color:{Tinta}">O link vale por <span style="font-family:{Mono};font-weight:bold">{validade}</span> e só pode ser usado uma vez.</p>
                              <p style="margin:10px 0 0;font-family:{Sans};font-size:15px;line-height:1.55;color:{Tinta}">Se não foi você quem pediu, ignore este e-mail: nada muda até o link ser usado.</p>

                              <div style="margin:22px 0 0;border-top:2px dashed {Carvao};font-size:0;line-height:0">&nbsp;</div>

                              <div style="margin:18px 0 0;font-family:{Mono};font-size:11px;letter-spacing:0.12em;text-transform:uppercase;color:{TintaFraca}">Se o botão não abrir</div>
                              <div style="margin:6px 0 0;font-family:{Mono};font-size:12px;line-height:1.5;color:{Tinta};word-break:break-all"><a href="{link}" style="color:{Tinta}">{link}</a></div>
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>

                    <tr>
                      <td style="padding:24px 6px 0">
                        <p style="margin:0;font-family:{Sans};font-size:13px;line-height:1.55;font-weight:bold;color:{Tinta}">Esta mensagem é automática e foi enviada por um sistema. Não responda a este endereço.</p>
                        <div style="margin:14px 0 0;font-family:{Mono};font-size:11px;line-height:1.7;letter-spacing:0.08em;text-transform:uppercase;color:{TintaFraca}">{cnpj} - GrelhaHub &middot; Todos os direitos reservados</div>
                      </td>
                    </tr>

                  </table>
                </td>
              </tr>
            </table>
            """;

        var texto = $"""
            GRELHAHUB — {rotulo.ToUpperInvariant()}

            Olá, {nome}.

            {chamada}

            {acao}:
            {link}

            O link vale por {validade} e só pode ser usado uma vez.
            Se não foi você quem pediu, ignore este e-mail: nada muda até o link ser usado.

            --
            Esta mensagem é automática e foi enviada por um sistema. Não responda a este endereço.

            {cnpj} - GrelhaHub - Todos os direitos reservados.
            """;

        return new MensagemDeEmail(para, assunto, html, texto);
    }

    /// <summary>
    /// A listra da marca — <c>CHAPA-02</c>: cinco barras, e <b>exatamente uma</b> é âmbar.
    /// <para>
    /// É a mesma régua que a capa da casa usa no lugar da logo, e a mesma que o cliente vê
    /// acompanhando o pedido. Aqui ela diz em que ponto do acesso a pessoa está: o convite é mais
    /// cedo na régua que a redefinição, que já é de quem tem conta.
    /// </para>
    /// <para>
    /// Célula de tabela e não <c>&lt;div&gt;</c>: barra de 6px feita com <c>div</c> herda a altura
    /// de linha no Outlook e vira um bloco. <c>font-size:0</c> com o <c>&amp;nbsp;</c> é o par que
    /// impede a célula de colapsar.
    /// </para>
    /// </summary>
    private static string Listra(int etapa)
    {
        var celulas = Enumerable.Range(0, 5).Select(i =>
        {
            var cor = i < etapa ? Brasa : i == etapa ? Ambar : BarraApagada;

            var espaco = i == 0
                ? string.Empty
                : """<td width="4" style="width:4px;font-size:0;line-height:0">&nbsp;</td>""";

            return $"""{espaco}<td width="6" height="30" bgcolor="{cor}" style="width:6px;height:30px;background:{cor};font-size:0;line-height:0">&nbsp;</td>""";
        });

        return $"""<table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>{string.Concat(celulas)}</tr></table>""";
    }
}
