using System.Text.Json.Serialization;
using Punkartuchera.Core.Matches;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Sharing;
using Punkartuchera.Core.Users;

namespace Punkartuchera.Core.Backup;

/// <summary>
/// Copia de seguridad de todo lo que hay en el dispositivo: bandas propias (con sus balas),
/// bandas rivales, partidas guardadas con sus fotos y notas, y el perfil del jugador.
/// Es de la misma familia que el archivo <c>.punkband</c>, pero sin quitar nada.
/// </summary>
public sealed class BackupFile
{
    public const string FormatName = "punkartuchera-backup";
    public const int CurrentVersion = 1;

    public string Format { get; set; } = FormatName;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Cuándo se hizo la copia.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Nickname de quien hizo la copia (<see cref="UserProfile.DefaultNickname"/> si no tenía).</summary>
    public string Nickname { get; set; } = UserProfile.DefaultNickname;

    /// <summary>Tamaño de letra y tipografía elegidos.</summary>
    public BackupSettings Settings { get; set; } = new();

    public List<Band> Bands { get; set; } = [];

    public List<ActiveMatch> Matches { get; set; } = [];

    /// <summary>Fotos de miniaturas y de la mesa, indexadas por la clave con la que las nombran bandas y partidas.</summary>
    public List<PackagedPhoto> Photos { get; set; } = [];
}

public sealed class BackupSettings
{
    public int FontScale { get; set; } = UserProfile.DefaultFontScale;

    public ReadingFont Font { get; set; } = ReadingFont.Punk;
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(BackupFile))]
public sealed partial class BackupJsonContext : JsonSerializerContext;
