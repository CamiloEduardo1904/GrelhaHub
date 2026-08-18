using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.MigrationsLog
{
    /// <summary>
    /// A imutabilidade da trilha, no banco de log — <c>SPEC-GER-005</c>, ADR-0017.
    /// <para>
    /// ⚠⚠ <b>O gatilho ATRAVESSOU a mudança de banco, e precisava atravessar.</b> Ele existia no
    /// banco da aplicação e foi embora com a tabela; sem recriá-lo aqui, a trilha passaria a aceitar
    /// <c>UPDATE</c> e <c>DELETE</c> — e uma trilha que o próprio sistema pode reescrever não é
    /// trilha.
    /// </para>
    /// <para>
    /// ⚠ <b>Dois gatilhos, e o segundo não é redundância.</b> <c>TRUNCATE</c> não dispara gatilho
    /// <c>FOR EACH ROW</c> — e é exatamente o comando que apagaria a trilha inteira de uma vez.
    /// </para>
    /// <para>
    /// Foi a suíte que cobrou: dois testes de <c>CA-7</c> caíram no instante em que a tabela mudou
    /// de banco sem o gatilho junto.
    /// </para>
    /// </summary>
    public partial class ImutabilidadeDaTrilha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION fn_log_auditoria_imutavel()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION
                        'log_auditoria e imutavel — % recusado. '
                        'A trilha admite apenas INSERT e SELECT.', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_log_auditoria_imutavel
                BEFORE UPDATE OR DELETE ON log_auditoria
                FOR EACH ROW
                EXECUTE FUNCTION fn_log_auditoria_imutavel()
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_log_auditoria_sem_truncate
                BEFORE TRUNCATE ON log_auditoria
                FOR EACH STATEMENT
                EXECUTE FUNCTION fn_log_auditoria_imutavel()
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_log_auditoria_sem_truncate ON log_auditoria");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_log_auditoria_imutavel ON log_auditoria");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS fn_log_auditoria_imutavel()");
        }
    }
}
