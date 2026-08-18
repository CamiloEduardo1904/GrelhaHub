using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <c>RN-GER-038</c> — a permissão do painel de operação.
    /// <para>
    /// ⚠ <b>Migration de DADOS, e é ela que faz a permissão alcançar quem já existe.</b> Permissão
    /// nova no <c>CatalogoDeAcesso</c> só chega a unidade criada <b>depois</b> dela:
    /// <c>CadastrarRestaurante</c> semeia perfil e permissão no ato da criação. Sem esta migration,
    /// o gerente de uma casa que já operava veria a aba e tomaria 403 — botão que responde "não
    /// pode" para quem pode é pior do que funcionalidade ausente.
    /// </para>
    /// <para>
    /// UUID <b>fixo e escrito à mão</b>, seguindo as migrations anteriores: gerar aqui faria a
    /// mesma migration produzir ids diferentes em cada banco, e comparar ambientes deixaria de ser
    /// possível.
    /// </para>
    /// </summary>
    public partial class PermissaoDoPainelDeOperacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO permissao (id, codigo, descricao, modulo) VALUES
                  ('0193c000-0001-7000-8000-000000000010',
                   'operacao.visualizar', 'Ver o painel de operação e o faturamento', 'VEN')
                """);

            // Administrador e Gerente, e mais ninguém — RN-GER-038.
            //
            // ⚠ O Atendente NÃO entra, embora tenha `pedido.visualizar`. É a separação inteira da
            // regra: ver o pedido da mesa 4 é a rotina dele; o faturamento da semana é a pergunta
            // de quem responde pelo resultado. Agregado de venda é o passo anterior a margem, e
            // conceder aqui abriria por uma porta lateral o que `RN-GER-012` fecha na da frente.
            migrationBuilder.Sql("""
                INSERT INTO perfil_permissao (perfil_id, permissao_id)
                SELECT pf.id, pm.id
                  FROM perfil pf
                  JOIN permissao pm ON pm.codigo = 'operacao.visualizar'
                 WHERE pf.nome IN ('Administrador', 'Gerente')
                   AND NOT EXISTS (
                        SELECT 1 FROM perfil_permissao pp
                         WHERE pp.perfil_id = pf.id AND pp.permissao_id = pm.id)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A ordem importa: o vínculo referencia a permissão.
            migrationBuilder.Sql("""
                DELETE FROM perfil_permissao
                 WHERE permissao_id IN (SELECT id FROM permissao WHERE codigo = 'operacao.visualizar')
                """);

            migrationBuilder.Sql("DELETE FROM permissao WHERE codigo = 'operacao.visualizar'");
        }
    }
}
