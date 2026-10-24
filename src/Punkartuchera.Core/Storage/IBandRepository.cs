using Punkartuchera.Core.Models;

namespace Punkartuchera.Core.Storage;

/// <summary>
/// Persistencia de bandas. Hoy es local (IndexedDB); una versión en la nube
/// solo tiene que implementar esta interfaz.
/// </summary>
public interface IBandRepository
{
    Task<IReadOnlyList<Band>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Band?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(Band band, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
