using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrelhaHub.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// SPEC-GER-007 e SPEC-VEN-002 — nome, sobrenome e CPF do usuario; logo da unidade.
    ///
    /// As duas specs previam migrations separadas (`PessoaEDelegacao` e `LogoDaUnidade`). Saiu
    /// uma: o EF gera uma migration por conjunto de mudancas pendentes do modelo, e separar
    /// exigiria gerar, aplicar e so depois editar o modelo de novo — duas migrations pelo
    /// beneficio de um nome. As specs foram corrigidas.
    ///
    /// ⚠ `char(11)` no PostgreSQL PREENCHE com espacos a direita. E seguro aqui somente porque
    /// `ck_usuario_cpf` exige exatamente 11 digitos: um valor curto seria preenchido, o
    /// preenchimento nao casa com `^[0-9]{11}$`, e o INSERT falha. Sem o CHECK, `char` guardaria
    /// '123        ' silenciosamente e toda comparacao passaria a depender de padding.
    /// </summary>
    public partial class CpfSobrenomeELogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cpf",
                table: "usuario",
                type: "char(11)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sobrenome",
                table: "usuario",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "url_logo",
                table: "unidade",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // RN-GER-023 — unico em TODO o sistema. NULL nao conflita com NULL no PostgreSQL,
            // entao o usuario de plataforma e os usuarios anteriores a esta regra convivem com a
            // restricao sem indice parcial.
            migrationBuilder.CreateIndex(
                name: "uq_usuario_cpf",
                table: "usuario",
                column: "cpf",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_usuario_cpf",
                table: "usuario",
                sql: "cpf IS NULL OR cpf ~ '^[0-9]{11}$'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_usuario_cpf",
                table: "usuario");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usuario_cpf",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "cpf",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "sobrenome",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "url_logo",
                table: "unidade");
        }
    }
}
