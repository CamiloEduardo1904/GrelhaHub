using GrelhaHub.Domain.Gerais;
using Microsoft.EntityFrameworkCore;

namespace GrelhaHub.Application.Common.Interfaces;

/// <summary>
/// O banco de <b>arquivo</b> — ADR-0017, ADR-0018. Só bytes.
/// <para>
/// Separado porque cresce por outro motivo: uma foto de produto pesa mais que mil pedidos, e o
/// <c>dump</c> do banco de negócio não deveria carregá-la.
/// </para>
/// <para>
/// ⚠ Nenhuma regra vive aqui — nem filtro de unidade, nem exclusão lógica, nem auditoria. Tudo isso
/// está no metadado, no banco da aplicação, com o mesmo <c>Id</c>.
/// </para>
/// </summary>
public interface IArquivoDb
{
    DbSet<ArquivoBinario> Binarios { get; }

    Task<int> GravarAsync(CancellationToken cancellationToken = default);
}
