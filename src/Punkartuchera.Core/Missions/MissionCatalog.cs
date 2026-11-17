namespace Punkartuchera.Core.Missions;

/// <summary>
/// Misión (escenario) del PDF oficial de Escenarios. Solo lleva un resumen propio y la página:
/// las reglas completas se leen en el PDF.
/// </summary>
/// <param name="Id">Identificador estable, el que se guarda en la partida.</param>
/// <param name="Name">Nombre del escenario tal como sale en el PDF.</param>
/// <param name="Page">Página del PDF de Escenarios.</param>
/// <param name="Summary">De qué va, en una o dos frases.</param>
/// <param name="Scoring">Cómo se consiguen los puntos, resumido.</param>
/// <param name="ContaminatedZones">Zonas de Terreno contaminado que se colocan.</param>
public sealed record Mission(string Id, string Name, int Page, string Summary, string Scoring, int ContaminatedZones);

/// <summary>
/// Las 18 misiones del PDF oficial de Escenarios, en el orden del PDF.
/// Todas duran 6 Turnos, usan el despliegue normal y gana quien tenga más puntos.
/// </summary>
public static class MissionCatalog
{
    /// <summary>PDF oficial de Escenarios (el mismo que la fuente "escenarios" de core.json).</summary>
    public const string PdfUrl = "https://punkapocalyptic.com/wp-content/uploads/2025/08/Escenarios_.pdf";

    public static IReadOnlyList<Mission> All { get; } =
    [
        new("abrazame-y-boom", "Abrázame y boom", 5,
            "Cinco punkicornios corretean sueltos por la mesa. Hay que cogerlos con una prueba de Técnica sin que te revienten en los morros.",
            "5 por punkicornio y 1 por cada ascoiris que lleve tu banda.", 2),
        new("agente-toxico", "Agente tóxico", 6,
            "Hay chatarra buena enterrada bajo un vertedero tóxico en el centro. Se excava con Dureza y el que falla se traga el veneno.",
            "5 por la pila central destapada y controlada y 2 por cada una de las otras.", 1),
        new("agua-pura", "Agua pura", 7,
            "Seis pozos automáticos del mundo de antes. Ponlos en marcha con Técnica sin cargártelos y quédate al lado.",
            "Por pozo controlado: 3 si está en marcha, 1 si no y nada si está roto.", 2),
        new("cementerio-de-olvifantes", "Cementerio de olvifantes", 8,
            "A rebuscar perlas entre esqueletos de olvifante y montones de mierda. Cada Acción tira en la tabla de búsqueda y alguna sale muy cara.",
            "1 por perla que lleve tu banda al final.", 1),
        new("control-de-plagas", "Control de plagas", 9,
            "Los mordedores anidan en las alcantarillas. Revienta los nidos con Técnica; si fallas, sale uno y te muerde.",
            "2 por nido destruido que controle tu banda.", 1),
        new("flipabobos", "Flipabobos", 10,
            "Seis alcantarillas con setas que colocan. Cada dosis que sacas despierta más al lombriñón, que se traga al que pilla cerca.",
            "1 por dosis, en el momento de sacarla.", 2),
        new("gasofa-barata", "Gasofa barata", 11,
            "Coches abandonados en el centro con algo de gasofa dentro. Dos de los tuyos cargan bidones y van chupando de los depósitos.",
            "Gana quien tenga más gasofa en sus bidones al acabar.", 1),
        new("gestion-de-estres", "Gestión de estrés", 12,
            "Seis martinejos sueltos que se mueven, evolucionan y acaban reventando. Pilla todos los que puedas antes.",
            "1 por martinejo que lleves y 1 más por cada marcador de evolución que tenga.", 1),
        new("hacerse-fuerte", "Hacerse fuerte", 13,
            "Seis posiciones: dos en cada despliegue y dos en el centro. Demuestra quién manda plantándote en casa del rival.",
            "Cada Turno, por posición controlada: 1 en el centro y 3 en el despliegue rival.", 1),
        new("mundo-vegano", "Mundo vegano", 14,
            "Mesa llena de vegetación con seis matorrales de comida de verdad… y algo dentro que se lleva al que se acerca.",
            "1 por matorral controlado al final de cada Turno.", 1),
        new("no-hay-huevos", "No hay huevos", 15,
            "Cuatro galligartos salen del centro y van poniendo huevos por ahí. A cazar bichos y huevos.",
            "1 por galligarto y 3 por huevo que lleve tu banda.", 2),
        new("rat-pack", "Rat pack", 16,
            "Escolta al viejo Sajarratas, que casi no se tiene en pie, y vigila las cuatro posiciones alrededor del centro.",
            "1 por posición vigilada cada Turno; al final, 5 si tienes a Sajarratas y 3 más si está en tu despliegue.", 1),
        new("sondas-del-espacio-exterior", "Sondas del espacio exterior", 17,
            "Una nave alienígena estrellada en el centro, con supervivientes que disparan. Saquea piezas antes de que alguien pulse el botón rojo.",
            "1 por pieza normal y 3 por pieza chula.", 2),
        new("sueltalo", "Suéltalo", 19,
            "Tres de los tuyos van con un apretón y hay tres truñaderos en la línea central. Llega antes de que pase la desgracia.",
            "5 por usar un truñadero, 2 por aliviarse en otro sitio y −1 por cada accidente.", 1),
        new("sujetame-el-cubata", "Sujétame el cubata", 20,
            "Seis posiciones que colocáis vosotros: cuanto más cerca del rival, más valen y más te la juegas.",
            "Cada Turno, por posición controlada: de 1 en tu despliegue a 5 en el del rival.", 2),
        new("vampirizame", "Vampirízame", 21,
            "Tres ojaldrones revolotean por la mesa. Hay que pegárselos encima aunque te chupen la Dureza.",
            "1 por ojaldrón que lleves y 1 por cada chupetón que haya dado.", 2),
        new("vaya-caos", "Vaya caos", 22,
            "Órdenes cruzadas y dos lameojetes sueltos. Cada Turno un dado decide qué da puntos: objetivos, bajas, lameojetes o despejarse.",
            "Lo que diga la tabla de cada Turno, con un máximo de 3 por Turno.", 2),
        new("viendo-holobasura", "Viendo holobasura", 23,
            "Tres cristales verdes en la línea central que proyectan imágenes del mundo de antes. Actívalos con Técnica y no los sueltes.",
            "Cada Turno, por cristal activo controlado: 1 en los Turnos 1 y 2, 2 en el 3 y 4 y 3 en el 5 y 6.", 1),
    ];

    private static readonly Dictionary<string, Mission> ById = All.ToDictionary(m => m.Id, StringComparer.Ordinal);

    public static Mission? Find(string? id) => id is not null && ById.TryGetValue(id, out var mission) ? mission : null;

    /// <summary>Misión al azar. Si se pasa la actual, nunca repite esa.</summary>
    public static Mission Random(Random random, string? exceptId = null)
    {
        var pool = All.Where(m => m.Id != exceptId).ToList();
        return pool[random.Next(pool.Count)];
    }

    /// <summary>Enlace al PDF abierto por la página de la misión.</summary>
    public static string PdfLink(Mission mission) => $"{PdfUrl}#page={mission.Page}";
}
