using System.Text.Json;
using Punkartuchera.Core.Matches;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;
using Punkartuchera.Core.Services;
using Punkartuchera.Core.Sharing;
using Punkartuchera.Core.Storage;
using Punkartuchera.Core.Users;

namespace Punkartuchera.Core.Backup;

/// <summary>
/// Hace la copia de seguridad de todo lo guardado en el dispositivo y la restaura.
/// Al restaurar se añade lo de la copia: las bandas y partidas que ya existían (mismo identificador)
/// se sustituyen por las de la copia y el resto se queda como estaba.
/// </summary>
public sealed class BackupService(
    IBandRepository bands,
    IPhotoStore photos,
    ISavedMatchStore matches,
    IUserProfileStore profiles,
    IRulesCatalog rules)
{
    public const string FileExtension = ".json";
    public const string ContentType = "application/json";

    /// <summary>Tamaño máximo aceptado al restaurar: da para muchas bandas y partidas con fotos de la mesa.</summary>
    public const long MaxFileBytes = 150 * 1024 * 1024;

    public const int MaxBands = 500;
    public const int MaxMatches = 500;

    /// <summary>Máximo de contadores por bando en una partida.</summary>
    public const int MaxCounters = 50;

    private const int MaxCounterKeyLength = 60;

    public async Task<BackupResult> CreateAsync(CancellationToken cancellationToken = default)
    {
        var profile = (await profiles.GetAsync(cancellationToken)).Normalize();
        var backup = new BackupFile
        {
            CreatedAt = DateTimeOffset.UtcNow,
            Nickname = profile.DisplayName,
            Settings = new BackupSettings { FontScale = profile.FontScale, Font = profile.Font },
            Bands = (await bands.GetAllAsync(cancellationToken)).OrderBy(b => b.CreatedAt).ToList(),
            Matches = (await matches.GetAllAsync(cancellationToken)).OrderBy(m => m.StartedAt).ToList(),
        };

        var packaged = new Dictionary<string, PackagedPhoto?>();

        async Task<bool> PackAsync(string key)
        {
            if (!packaged.TryGetValue(key, out var photo))
            {
                var stored = await photos.GetAsync(key, cancellationToken);
                photo = stored is null ? null : new PackagedPhoto { Key = key, ContentType = stored.ContentType, Data = stored.Data };
                packaged[key] = photo;
            }

            return photo is not null;
        }

        foreach (var member in backup.Bands.SelectMany(b => b.Members))
        {
            if (member.Photo is { Source: PhotoSource.Stored } photo && !await PackAsync(photo.Key))
            {
                member.Photo = null;
            }
        }

        foreach (var match in backup.Matches)
        {
            var kept = new List<MatchPhoto>();
            foreach (var photo in match.Photos)
            {
                if (await PackAsync(photo.Key))
                {
                    kept.Add(photo);
                }
            }

            match.Photos = kept;
        }

        backup.Photos = packaged.Values.OfType<PackagedPhoto>().ToList();

        var content = JsonSerializer.SerializeToUtf8Bytes(backup, BackupJsonContext.Default.BackupFile);
        return new BackupResult(
            FileNameFor(backup.Nickname, backup.CreatedAt),
            content,
            BackupSummary.Of(backup));
    }

    /// <summary>Lee y comprueba una copia sin guardar nada, para enseñar qué trae antes de restaurarla.</summary>
    /// <exception cref="BackupException">Si no es una copia de Punkartuchera o no se puede leer.</exception>
    public async Task<BackupFile> ReadAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = await ReadLimitedAsync(content, MaxFileBytes, cancellationToken);

        BackupFile? backup;
        try
        {
            backup = await JsonSerializer.DeserializeAsync(buffer, BackupJsonContext.Default.BackupFile, cancellationToken);
        }
        catch (JsonException)
        {
            throw new BackupException("Eso no es una copia de Punkartuchera. Busca el archivo .json que te guardó la app.");
        }

        if (backup is null || backup.Format != BackupFile.FormatName)
        {
            throw new BackupException("Eso no es una copia de Punkartuchera. Busca el archivo .json que te guardó la app.");
        }

        if (backup.Version > BackupFile.CurrentVersion)
        {
            throw new BackupException("La copia viene de una versión más nueva de la app. Actualiza y vuelve a probar.");
        }

        if ((backup.Bands?.Count ?? 0) > MaxBands || (backup.Matches?.Count ?? 0) > MaxMatches)
        {
            throw new BackupException("La copia trae más bandas o partidas de las que la app puede guardar.");
        }

        backup.Nickname = UserProfile.CleanNickname(backup.Nickname) ?? UserProfile.DefaultNickname;
        backup.Settings ??= new BackupSettings();
        backup.Bands = (backup.Bands ?? []).OfType<Band>().ToList();
        backup.Matches = (backup.Matches ?? []).OfType<ActiveMatch>().ToList();
        backup.Photos = (backup.Photos ?? []).OfType<PackagedPhoto>().ToList();

        foreach (var band in backup.Bands)
        {
            Tidy(band);
        }

        foreach (var match in backup.Matches)
        {
            Tidy(match);
        }

        return backup;
    }

    /// <summary>Guarda en el dispositivo lo que trae la copia. Devuelve cuánto se ha restaurado.</summary>
    public async Task<BackupSummary> RestoreAsync(BackupFile backup, CancellationToken cancellationToken = default)
    {
        var packaged = backup.Photos
            .Where(p => !string.IsNullOrEmpty(p.Key) && RivalBandSanitizer.IsImage(p.Data, p.ContentType))
            .GroupBy(p => p.Key)
            .ToDictionary(g => g.Key, g => g.First());

        // Cada foto se guarda una sola vez aunque la usen varias bandas o partidas.
        var savedKeys = new Dictionary<string, string>();

        async Task<string?> SavePhotoAsync(string key)
        {
            if (savedKeys.TryGetValue(key, out var saved))
            {
                return saved;
            }

            if (!packaged.TryGetValue(key, out var photo))
            {
                return null;
            }

            saved = await photos.SaveAsync(photo.Data, photo.ContentType, cancellationToken);
            savedKeys[key] = saved;
            return saved;
        }

        foreach (var band in backup.Bands)
        {
            foreach (var member in band.Members)
            {
                if (member.Photo is { Source: PhotoSource.Stored } photo)
                {
                    member.Photo = await SavePhotoAsync(photo.Key) is { } key ? PhotoRef.Stored(key) : null;
                }
            }

            var previous = await bands.GetAsync(band.Id, cancellationToken);
            await bands.SaveAsync(band, cancellationToken);
            if (previous is not null)
            {
                await DeletePhotosAsync(previous.Members.Select(m => m.Photo), cancellationToken);
            }
        }

        foreach (var match in backup.Matches)
        {
            var kept = new List<MatchPhoto>();
            foreach (var photo in match.Photos)
            {
                if (await SavePhotoAsync(photo.Key) is { } key)
                {
                    photo.Key = key;
                    kept.Add(photo);
                }
            }

            match.Photos = kept;

            var previous = await matches.GetAsync(match.Id, cancellationToken);
            await matches.SaveAsync(match, cancellationToken);
            if (previous is not null)
            {
                foreach (var photo in previous.Photos)
                {
                    await photos.DeleteAsync(photo.Key, cancellationToken);
                }
            }
        }

        var profile = await profiles.GetAsync(cancellationToken);
        if (backup.Nickname != UserProfile.DefaultNickname)
        {
            profile.Nickname = backup.Nickname;
        }

        profile.FontScale = backup.Settings.FontScale;
        profile.Font = backup.Settings.Font;
        await profiles.SaveAsync(profile.Normalize(), cancellationToken);

        return BackupSummary.Of(backup) with { Photos = savedKeys.Count };
    }

    /// <summary>
    /// Arregla lo que podría venir roto o tocado a mano: listas nulas y fotos que no apuntan
    /// a algo que la app sepa enseñar. Las bandas propias conservan sus balas.
    /// </summary>
    private void Tidy(Band band)
    {
        RivalBandSanitizer.LimitContent(band, rules);

        if (band.IsReadOnly)
        {
            // Una rival nunca guarda balas ni coste por miniatura, venga de donde venga.
            foreach (var member in band.Members)
            {
                member.Ammo = 0;
            }

            band.KeepOnlyTotalCost();
        }
    }

    private static void Tidy(ActiveMatch match)
    {
        match.Round = Math.Clamp(match.Round, 1, ActiveMatch.MaxRound);
        match.OwnScore = Math.Clamp(match.OwnScore, 0, ActiveMatch.MaxCount);
        match.RivalScore = Math.Clamp(match.RivalScore, 0, ActiveMatch.MaxCount);
        match.OwnBandName = RivalBandSanitizer.CleanText(match.OwnBandName, BandService.MaxNameLength);
        match.RivalBandName = match.RivalBandName is null ? null : RivalBandSanitizer.CleanText(match.RivalBandName, BandService.MaxNameLength);
        match.Own = TidyMembers(match.Own);
        match.Rival = TidyMembers(match.Rival);
        match.OwnCounters = TidyCounters(match.OwnCounters);
        match.RivalCounters = TidyCounters(match.RivalCounters);

        // El enlace del mapa se vuelve a construir con lo que entiende el generador.
        match.Map = match.Map is null ? null : MatchMap.FromQuery(match.Map.Query);

        match.Photos = (match.Photos ?? [])
            .OfType<MatchPhoto>()
            .Where(p => !string.IsNullOrEmpty(p.Key))
            .Take(ActiveMatch.MaxPhotos)
            .ToList();
        if (match.Notes is { Length: > ActiveMatch.MaxNotes } notes)
        {
            match.Notes = notes[..ActiveMatch.MaxNotes];
        }
    }

    private static List<MatchMember> TidyMembers(List<MatchMember>? members) =>
        (members ?? [])
            .OfType<MatchMember>()
            .Take(RivalBandSanitizer.MaxMembers)
            .Select(m =>
            {
                m.Name = RivalBandSanitizer.CleanText(m.Name, BandService.MaxNameLength);
                m.Role = RivalBandSanitizer.CleanText(m.Role, RivalBandSanitizer.MaxTextLength);
                return m;
            })
            .ToList();

    private static Dictionary<string, int> TidyCounters(Dictionary<string, int>? counters) =>
        (counters ?? [])
            .Where(c => c.Key.Length is > 0 and <= MaxCounterKeyLength)
            .Take(MaxCounters)
            .ToDictionary(c => c.Key, c => Math.Clamp(c.Value, 0, ActiveMatch.MaxCount));

    private async Task DeletePhotosAsync(IEnumerable<PhotoRef?> refs, CancellationToken cancellationToken)
    {
        foreach (var key in refs.OfType<PhotoRef>().Where(p => p.Source == PhotoSource.Stored).Select(p => p.Key).Distinct())
        {
            await photos.DeleteAsync(key, cancellationToken);
        }
    }

    private static async Task<MemoryStream> ReadLimitedAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                await buffer.DisposeAsync();
                throw new BackupException("El archivo pesa demasiado para ser una copia de Punkartuchera.");
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <summary>Por ejemplo <c>punkartuchera-PringaoSinNickname-2026-11-08-1135.json</c>.</summary>
    public static string FileNameFor(string nickname, DateTimeOffset createdAt)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|', ' ']).ToHashSet();
        var safe = new string(nickname.Select(c => invalid.Contains(c) || char.IsControl(c) ? '-' : c).ToArray()).Trim('.', '-');
        return $"punkartuchera-{(safe.Length == 0 ? UserProfile.DefaultNickname : safe)}-{createdAt.ToLocalTime():yyyy-MM-dd-HHmm}{FileExtension}";
    }
}

public sealed record BackupResult(string FileName, byte[] Content, BackupSummary Summary);

/// <summary>Resumen de lo que lleva una copia.</summary>
public sealed record BackupSummary(string Nickname, DateTimeOffset CreatedAt, int OwnBands, int RivalBands, int Matches, int Photos, int Ammo)
{
    public static BackupSummary Of(BackupFile backup) => new(
        backup.Nickname,
        backup.CreatedAt,
        backup.Bands.Count(b => !b.IsReadOnly),
        backup.Bands.Count(b => b.IsReadOnly),
        backup.Matches.Count,
        backup.Photos.Count,
        backup.Bands.Where(b => !b.IsReadOnly).SelectMany(b => b.Members).Sum(m => m.Ammo));
}

public sealed class BackupException(string message) : Exception(message);
