using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Serialization;
using Punkartuchera.Core.Storage;

namespace Punkartuchera.Core.Services;

/// <summary>
/// Operaciones sobre bandas propias: crear, editar miembros, duplicar y borrar
/// (incluidas sus fotos). Las bandas rivales son de solo lectura.
/// </summary>
public sealed class BandService(IBandRepository bands, IPhotoStore photos, IRulesCatalog rules)
{
    public const int MaxNameLength = 60;

    public async Task<IReadOnlyList<Band>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var all = await bands.GetAllAsync(cancellationToken);
        return all
            .OrderBy(b => b.Ownership)
            .ThenByDescending(b => b.UpdatedAt)
            .ToList();
    }

    public Task<Band?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        bands.GetAsync(id, cancellationToken);

    public async Task<Band> CreateAsync(string name, string factionId, CancellationToken cancellationToken = default)
    {
        var band = new Band { Name = NormalizeName(name), FactionId = factionId };
        Validate(band);
        await bands.SaveAsync(band, cancellationToken);
        return band;
    }

    public async Task SaveAsync(Band band, CancellationToken cancellationToken = default)
    {
        EnsureEditable(band);
        band.Name = NormalizeName(band.Name);
        Validate(band);
        band.UpdatedAt = DateTimeOffset.UtcNow;
        await bands.SaveAsync(band, cancellationToken);
    }

    /// <summary>Crea un miembro con los valores por defecto de cada atributo.</summary>
    public Miniature NewMember(string role = "")
    {
        var member = new Miniature { Role = role };
        foreach (var stat in rules.Stats)
        {
            member.Stats[stat.Key] = stat.Default;
        }

        return member;
    }

    /// <summary>Añade o reemplaza un miembro y guarda la banda.</summary>
    public async Task SaveMemberAsync(Band band, Miniature member, CancellationToken cancellationToken = default)
    {
        EnsureEditable(band);
        if (string.IsNullOrWhiteSpace(member.Name))
        {
            throw new BandValidationException("El miembro necesita un nombre.");
        }

        member.Name = member.Name.Trim();
        member.Ammo = Math.Max(0, member.Ammo);
        ClampStats(member);

        // La foto anterior se lee de lo guardado: el objeto en memoria puede ser el mismo que se está editando.
        var persisted = await bands.GetAsync(band.Id, cancellationToken);
        var previousPhoto = persisted?.FindMember(member.Id)?.Photo;

        var index = band.Members.FindIndex(m => m.Id == member.Id);
        if (index >= 0)
        {
            band.Members[index] = member;
        }
        else
        {
            band.Members.Add(member);
        }

        await SaveAsync(band, cancellationToken);
        await DeleteStoredPhotoIfReplacedAsync(previousPhoto, member.Photo, cancellationToken);
    }

    public async Task RemoveMemberAsync(Band band, Guid memberId, CancellationToken cancellationToken = default)
    {
        EnsureEditable(band);
        var member = band.FindMember(memberId);
        if (member is null)
        {
            return;
        }

        band.Members.Remove(member);
        await SaveAsync(band, cancellationToken);
        await DeleteStoredPhotoIfReplacedAsync(member.Photo, null, cancellationToken);
    }

    /// <summary>Copia una banda con nuevos identificadores; las fotos se duplican para que sean independientes.</summary>
    public async Task<Band> DuplicateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var source = await bands.GetAsync(id, cancellationToken)
            ?? throw new BandValidationException("La banda no existe.");

        var copy = BandSerializer.Clone(source);
        copy.Id = Guid.NewGuid();
        copy.Name = NormalizeName($"{source.Name} (copia)");
        copy.Ownership = BandOwnership.Own;
        copy.CreatedAt = copy.UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var member in copy.Members)
        {
            member.Id = Guid.NewGuid();
            if (member.Photo is { Source: PhotoSource.Stored } photo)
            {
                var stored = await photos.GetAsync(photo.Key, cancellationToken);
                member.Photo = stored is null
                    ? null
                    : PhotoRef.Stored(await photos.SaveAsync(stored.Data, stored.ContentType, cancellationToken));
            }
        }

        await bands.SaveAsync(copy, cancellationToken);
        return copy;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var band = await bands.GetAsync(id, cancellationToken);
        if (band is null)
        {
            return;
        }

        await bands.DeleteAsync(id, cancellationToken);
        foreach (var member in band.Members)
        {
            await DeleteStoredPhotoIfReplacedAsync(member.Photo, null, cancellationToken);
        }
    }

    private void Validate(Band band)
    {
        if (string.IsNullOrWhiteSpace(band.Name))
        {
            throw new BandValidationException("La banda necesita un nombre.");
        }

        if (rules.FindFaction(band.FactionId) is null)
        {
            throw new BandValidationException("Elige una facción.");
        }
    }

    private void ClampStats(Miniature member)
    {
        foreach (var stat in rules.Stats)
        {
            if (member.Stats.TryGetValue(stat.Key, out var value))
            {
                member.Stats[stat.Key] = Math.Clamp(value, stat.Min, stat.Max);
            }
        }
    }

    private async Task DeleteStoredPhotoIfReplacedAsync(PhotoRef? previous, PhotoRef? current, CancellationToken cancellationToken)
    {
        if (previous is { Source: PhotoSource.Stored } && previous != current)
        {
            await photos.DeleteAsync(previous.Key, cancellationToken);
        }
    }

    private static void EnsureEditable(Band band)
    {
        if (band.IsReadOnly)
        {
            throw new BandValidationException("Las bandas rivales no se pueden modificar.");
        }
    }

    private static string NormalizeName(string name)
    {
        var trimmed = name.Trim();
        return trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
    }
}

public sealed class BandValidationException(string message) : Exception(message);
