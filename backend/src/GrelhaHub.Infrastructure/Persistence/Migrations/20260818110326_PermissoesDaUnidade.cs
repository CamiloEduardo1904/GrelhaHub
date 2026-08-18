using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <c>RN-GER-036</c> / <c>RN-GER-037</c> — as duas permissões do cadastro da própria casa.
    /// <para>
    /// ⚠ <b>Migration de DADOS, e ela é o ponto todo.</b> Permissão nova no <c>CatalogoDeAcesso</c>
    /// só alcança unidade criada <b>depois</b> dela: <c>CadastrarRestaurante</c> semeia perfil e
    /// permissão no ato da criação. Sem esta migration, o administrador de uma casa que já existia
    /// veria a tela de horário e tomaria 403 — botão que responde "não pode" para quem pode é pior
    /// do que funcionalidade ausente.
    /// </para>
    /// <para>
    /// Os UUIDs são <b>fixos e escritos à mão</b>, seguindo o padrão das migrations anteriores:
    /// gerar aqui faria a mesma migration produzir ids diferentes em cada banco, e a comparação
    /// entre ambientes deixaria de ser possível.
    /// </para>
    /// </summary>
    public partial class PermissoesDaUnidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO permissao (id, codigo, descricao, modulo) VALUES
                  ('0193c000-0001-7000-8000-00000000000e',
                   'unidade.visualizar', 'Ver endereço e horário de funcionamento', 'GER'),
                  ('0193c000-0001-7000-8000-00000000000f',
                   'unidade.gerenciar', 'Alterar endereço e horário de funcionamento', 'GER')
                """);

            // Quem VÊ: Administrador, Gerente e Atendente — quem atende o telefone precisa
            // responder "a gente abre às 18h".
            migrationBuilder.Sql("""
                INSERT INTO perfil_permissao (perfil_id, permissao_id)
                SELECT pf.id, pm.id
                  FROM perfil pf
                  JOIN permissao pm ON pm.codigo = 'unidade.visualizar'
                 WHERE pf.nome IN ('Administrador', 'Gerente', 'Atendente')
                   AND NOT EXISTS (
                        SELECT 1 FROM perfil_permissao pp
                         WHERE pp.perfil_id = pf.id AND pp.permissao_id = pm.id)
                """);

            // Quem ALTERA: Administrador e Gerente. O horário move a virada do dia operacional
            // (RN-GER-006), e isso é decisão de quem responde pelo faturamento do dia — não de
            // quem está no balcão.
            migrationBuilder.Sql("""
                INSERT INTO perfil_permissao (perfil_id, permissao_id)
                SELECT pf.id, pm.id
                  FROM perfil pf
                  JOIN permissao pm ON pm.codigo = 'unidade.gerenciar'
                 WHERE pf.nome IN ('Administrador', 'Gerente')
                   AND NOT EXISTS (
                        SELECT 1 FROM perfil_permissao pp
                         WHERE pp.perfil_id = pf.id AND pp.permissao_id = pm.id)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM perfil_permissao WHERE permissao_id IN
                  (SELECT id FROM permissao
                    WHERE codigo IN ('unidade.visualizar', 'unidade.gerenciar'))
                """);

            migrationBuilder.Sql("""
                DELETE FROM permissao
                 WHERE codigo IN ('unidade.visualizar', 'unidade.gerenciar')
                """);
        }
    }
}
