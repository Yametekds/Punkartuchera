using Punkartuchera.Core.Missions;

namespace Punkartuchera.UI.Components.Missions;

/// <summary>
/// Imágenes de cada misión sacadas del PDF oficial de Escenarios: la foto de cabecera y el plano
/// de colocación (despliegue, objetivos y direcciones). Están en <c>img/misiones/</c>.
/// </summary>
public static class MissionImages
{
    private const string Folder = "_content/Punkartuchera.UI/img/misiones";

    public static string Photo(Mission mission) => $"{Folder}/{mission.Id}.webp";

    public static string Map(Mission mission) => $"{Folder}/{mission.Id}-mapa.webp";
}
