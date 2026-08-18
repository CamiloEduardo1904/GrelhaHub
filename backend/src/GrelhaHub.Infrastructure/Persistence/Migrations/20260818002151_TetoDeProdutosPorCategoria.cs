using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// <c>RN-VEN-046</c>, <c>PAR-VEN-004</c> — o décimo parâmetro: teto de produtos ativos por
    /// categoria, 50 por padrão.
    /// <para>
    /// <b>Migration só de dado, sem mudança de modelo.</b> A definição de parâmetro é uma linha em
    /// <c>parametro</c>, e ela precisa existir no banco de toda unidade — inclusive nas que já
    /// estão em produção. O <c>Up</c> vazio que o <c>dotnet ef</c> gerou foi preenchido a mão.
    /// </para>
    /// <para>
    /// <b>Por que não editar a migration <c>ParametrosComVigencia</c>.</b> Ela já rodou; migration
    /// é história e não muda de conteúdo. O comentário dela diz exatamente isto — que referenciar
    /// <c>CatalogoDeParametros</c> a faria semear dez linhas no dia em que alguém acrescentasse o
    /// décimo, reescrevendo o passado. Este arquivo é esse décimo.
    /// </para>
    /// </summary>
    public partial class TetoDeProdutosPorCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Id fixo e escrito à mão, seguindo a sequência da migration de origem: `...0010` é o
            // décimo. Id gerado em tempo de execução tornaria a migration irrepetível e o mesmo
            // parâmetro teria chaves diferentes em cada ambiente.
            //
            // `ON CONFLICT DO NOTHING` para o caso de um banco já ter recebido a linha à mão
            // durante o desenvolvimento — o "ajustável via banco" convida a isso.
            migrationBuilder.Sql("""
                INSERT INTO parametro (id, codigo, descricao, tipo_valor, dominio) VALUES
                ('0193a000-0001-7000-8000-000000000010', 'PAR-VEN-004', 'Máximo de produtos ativos por categoria', 'inteiro', '> 0')
                ON CONFLICT (codigo) DO NOTHING
                """);

            // ⚠ E O VALOR PARA AS UNIDADES QUE JÁ EXISTEM.
            //
            // `SemearParametrosAsync` semeia os valores no CADASTRO da unidade, então quem já foi
            // cadastrado antes desta migration ficaria com a definição sem valor nenhum. O primeiro
            // efeito seria "O parâmetro PAR-VEN-004 não tem valor vigente" ao salvar um produto —
            // uma falha ruidosa (de propósito, `SPEC-GER-003`) num restaurante que não fez nada
            // errado.
            //
            // `-infinity` na vigência, igual à carga inicial: qualquer consulta em qualquer data
            // encontra valor, inclusive apuração de dado anterior ao cadastro.
            //
            // `NOT EXISTS` em vez de `ON CONFLICT`: a EXCLUDE de vigência não é chave única, então
            // não há conflito para o Postgres capturar — e sem o guarda, rodar a migration duas
            // vezes criaria vigências sobrepostas.
            migrationBuilder.Sql("""
                INSERT INTO parametro_valor
                    (id, parametro_id, unidade_id, valor, vigencia_inicio, justificativa, dh_criacao)
                SELECT gen_random_uuid(), p.id, u.id, '50', '-infinity',
                       'Valor inicial da carga, conforme docs/04-parametros-do-sistema.md', now()
                  FROM unidade u
                 CROSS JOIN parametro p
                 WHERE p.codigo = 'PAR-VEN-004'
                   AND NOT EXISTS (
                       SELECT 1 FROM parametro_valor v
                        WHERE v.unidade_id = u.id AND v.parametro_id = p.id)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Apaga o VALOR antes da DEFINIÇÃO: `parametro_valor` referencia `parametro`, e a FK é
            // `ON DELETE RESTRICT` em todo o modelo (RN-GER-002). Sem esta primeira linha, o
            // `Down` falha em qualquer unidade que tenha ajustado o teto.
            migrationBuilder.Sql("""
                DELETE FROM parametro_valor
                 WHERE parametro_id = (SELECT id FROM parametro WHERE codigo = 'PAR-VEN-004')
                """);

            migrationBuilder.Sql("DELETE FROM parametro WHERE codigo = 'PAR-VEN-004'");
        }
    }
}
