using System.Text.Json.Serialization;

namespace Punkartuchera.Core.Users;

/// <summary>
/// Lo que el jugador ajusta en la sección Usuario: su nickname y cómo quiere leer la app.
/// Se guarda solo en el dispositivo y viaja en la copia de seguridad.
/// </summary>
public sealed class UserProfile
{
    /// <summary>Nombre que se usa cuando el jugador no ha puesto ninguno.</summary>
    public const string DefaultNickname = "PringaoSinNickname";

    public const int MaxNicknameLength = 40;

    /// <summary>Tamaño de letra en porcentaje sobre el normal.</summary>
    public const int MinFontScale = 90;
    public const int MaxFontScale = 150;
    public const int FontScaleStep = 10;
    public const int DefaultFontScale = 100;

    public string? Nickname { get; set; }

    public int FontScale { get; set; } = DefaultFontScale;

    public ReadingFont Font { get; set; } = ReadingFont.Punk;

    /// <summary>El nickname puesto, o <see cref="DefaultNickname"/> si no hay.</summary>
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Nickname) ? DefaultNickname : Nickname;

    /// <summary>Deja los valores dentro de lo que la app sabe pintar (por si vienen de una copia tocada a mano).</summary>
    public UserProfile Normalize()
    {
        Nickname = CleanNickname(Nickname);
        FontScale = Math.Clamp(
            (int)Math.Round(FontScale / (double)FontScaleStep) * FontScaleStep,
            MinFontScale,
            MaxFontScale);
        if (!Enum.IsDefined(Font))
        {
            Font = ReadingFont.Punk;
        }

        return this;
    }

    /// <summary>Quita espacios sobrantes y caracteres de control; vacío = sin nickname.</summary>
    public static string? CleanNickname(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var clean = string.Join(' ', new string(value.Where(c => !char.IsControl(c)).ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length > MaxNicknameLength)
        {
            clean = clean[..MaxNicknameLength].TrimEnd();
        }

        return clean.Length == 0 ? null : clean;
    }
}

/// <summary>Tipografía con la que se lee la app.</summary>
public enum ReadingFont
{
    /// <summary>Las letras de siempre, las de la web oficial.</summary>
    Punk,

    /// <summary>OpenDyslexic: letra con la base marcada, pensada para lectores con dislexia.</summary>
    OpenDyslexic,

    /// <summary>Atkinson Hyperlegible: letra muy clara, con cada carácter fácil de distinguir.</summary>
    Hyperlegible
}

/// <summary>Formato compacto del perfil tal como se guarda en el navegador.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UserProfile))]
public sealed partial class UserJsonContext : JsonSerializerContext;
