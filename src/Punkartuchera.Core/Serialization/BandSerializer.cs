using System.Text.Json;
using System.Text.Json.Serialization;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Sharing;

namespace Punkartuchera.Core.Serialization;

/// <summary>
/// Formato JSON de las bandas, compartido por el almacenamiento local y la exportación.
/// Usa generación de código para que funcione con el recortado de WebAssembly.
/// </summary>
public static class BandSerializer
{
    public static string Serialize(Band band) => JsonSerializer.Serialize(band, PunkJsonContext.Default.Band);

    public static Band Deserialize(string json) =>
        JsonSerializer.Deserialize(json, PunkJsonContext.Default.Band)
        ?? throw new JsonException("El contenido no es una banda válida.");

    /// <summary>Copia profunda, útil para editar sin tocar el original.</summary>
    public static Band Clone(Band band) => Deserialize(Serialize(band));
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Band))]
[JsonSerializable(typeof(List<Band>))]
[JsonSerializable(typeof(BandPackage))]
public sealed partial class PunkJsonContext : JsonSerializerContext;
