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
/// <param name="Pdf">PDF propio cuando el escenario no viene en el de Escenarios (los sueltos de la web).</param>
/// <param name="HasArt">Si hay foto y plano en <c>img/misiones/</c>.</param>
public sealed record Mission(string Id, string Name, int Page, string Summary, string Scoring, int ContaminatedZones, string? Pdf = null, bool HasArt = true);

/// <summary>
/// Las 18 misiones del PDF oficial de Escenarios, en el orden del PDF, y detrás los escenarios
/// sueltos de la zona de descargas. Todas duran 6 Turnos, usan el despliegue normal y gana quien tenga más puntos.
/// </summary>
public static class MissionCatalog
{
    /// <summary>PDF oficial de Escenarios (el mismo que la fuente "escenarios" de core.json).</summary>
    public const string PdfUrl = "https://punkapocalyptic.com/wp-content/uploads/2025/08/Escenarios_.pdf";

    public static IReadOnlyList<Mission> All { get; } =
    [
        new("abrazame-y-boom", "Abrázame y boom", 5,
            "Cinco punkicornios pirados corretean por la mesa. Achúchalos con una prueba de Técnica y reza para que no te revienten en toda la jeta.",
            "5 por cada punkicornio que lleves encima y 1 por cada ascoiris. Si revienta, mala suerte, colega.", 2),
        new("agente-toxico", "Agente tóxico", 6,
            "Debajo de un vertedero que apesta a muerte hay chatarra de la buena. A cavar con la Dureza por delante, y el que falle, a tragar veneno.",
            "5 por la pila del centro destapada y en tus manos, 2 por cada una de las otras.", 1),
        new("agua-pura", "Agua pura", 7,
            "Seis pozos del mundo de antes que todavía echan agua sin mierda. Arráncalos con Técnica, no te los cargues como un melón y plántate al lado.",
            "Por pozo que controles: 3 si funciona, 1 si sigue parado y ni las gracias si lo has roto.", 2),
        new("cementerio-de-olvifantes", "Cementerio de olvifantes", 8,
            "A meter la zarpa entre huesos de olvifante y montañas de mierda buscando perlas. Cada Acción es una tirada en la tabla, y alguna te deja tieso.",
            "1 por cada perla que lleve tu peña al acabar.", 1),
        new("control-de-plagas", "Control de plagas", 9,
            "Los mordedores han montado su chabola en las alcantarillas. Revienta los nidos con Técnica; como la cagues, sale uno con ganas de cena.",
            "2 por cada nido reventado que controle tu banda.", 1),
        new("flipabobos", "Flipabobos", 10,
            "Seis alcantarillas llenas de setas que te ponen fino. Cuantas más dosis pillas, más se despierta el lombriñón, que se zampa al primer pringao que pille.",
            "1 por dosis, en el acto. Colocarse cuenta.", 2),
        new("gasofa-barata", "Gasofa barata", 11,
            "Chatarras con ruedas tiradas en el centro y algo de gasofa dentro. Dos de los tuyos cargan con los bidones y van chupando de los depósitos como garrapatas.",
            "Gana quien acabe con más gasofa en los bidones. Ni una gota más que el rival y a llorar.", 1),
        new("gestion-de-estres", "Gestión de estrés", 12,
            "Seis martinejos sueltos que van a su bola, evolucionan y acaban petando. Pilla todos los que puedas antes de que te estallen en las manos.",
            "1 por martinejo que lleves y 1 más por cada marcador de evolución que tenga.", 1),
        new("hacerse-fuerte", "Hacerse fuerte", 13,
            "Seis posiciones: dos en cada despliegue y dos en el centro. Toca demostrar quién manda en el barrio plantándote en el salón del rival.",
            "Cada Turno, por posición que controles: 1 en el centro y 3 en casa del rival. La tuya no cuenta, cobarde.", 1),
        new("mundo-vegano", "Mundo vegano", 14,
            "Mesa hasta arriba de matojos y seis matorrales con comida de verdad, de la que no brilla. Lo malo es que algo vive dentro y se lleva al que se arrima.",
            "1 por matorral controlado al final de cada Turno.", 1),
        new("no-hay-huevos", "No hay huevos", 15,
            "Cuatro galligartos salen del centro cagando huevos por todas partes. A cazar bichos y huevos, que la tortilla no se hace sola.",
            "1 por galligarto y 3 por cada huevo que lleve tu banda.", 2),
        new("rat-pack", "Rat pack", 16,
            "Hay que llevar a cuestas al viejo Sajarratas, que no se tiene en pie ni borracho, y de paso vigilar las cuatro posiciones alrededor del centro.",
            "1 por posición vigilada cada Turno; al final, 5 si el viejo es tuyo y 3 más si lo tienes en tu despliegue.", 1),
        new("sondas-del-espacio-exterior", "Sondas del espacio exterior", 17,
            "Se ha estrellado un platillo en mitad de la mesa y los marcianos que quedan disparan a todo lo que se mueve. Desguázalo antes de que algún listo apriete el botón rojo.",
            "1 por pieza normal y 3 por pieza chula.", 2),
        new("sueltalo", "Suéltalo", 19,
            "Tres de los tuyos van con un apretón de los que hacen historia y solo hay tres truñaderos en el centro. Corre, que no llegas.",
            "5 por soltarlo en un truñadero, 2 por hacerlo donde pille y −1 por cada accidente en los pantalones.", 1),
        new("sujetame-el-cubata", "Sujétame el cubata", 20,
            "Seis posiciones que plantáis vosotros mismos: cuanto más cerca del rival, más valen y más fácil es que te partan la cara por ellas.",
            "Cada Turno, por posición controlada: de 1 en tu despliegue a 5 en las narices del rival.", 2),
        new("vampirizame", "Vampirízame", 21,
            "Tres ojaldrones revolotean buscando cuello. Hay que pegárselos encima aunque te dejen seco de Dureza, que se venden caros.",
            "1 por cada ojaldrón que lleves y 1 por cada chupetón que haya dado.", 2),
        new("vaya-caos", "Vaya caos", 22,
            "Nadie se entera de las órdenes y hay dos lameojetes sueltos liándola. Cada Turno un dado decide qué puntúa: objetivos, bajas, lameojetes o despejarse la cabeza.",
            "Lo que diga la tabla de cada Turno, con un tope de 3 por Turno. Ni más ni menos.", 2),
        new("viendo-holobasura", "Viendo holobasura", 23,
            "Tres cristales verdes en la línea central que escupen la tele del mundo de antes. Enciéndelos con Técnica y no sueltes el mando.",
            "Cada Turno, por cristal encendido que controles: 1 en los Turnos 1 y 2, 2 en el 3 y 4 y 3 en el 5 y 6.", 1),
        new("punkalabazas", "El retorno de las Punkalabazas asesinas", 1,
            "Unos granjeros de Puentechatarra abonaron el huerto con un bidón de «complejo vitamínico» y ahora las calabazas muerden. Dos Punkalabazas se pasean a su bola, escupen pinchos y te atrapan con raíces: trinca una y que no se te escape, que la élite las paga a precio de oro.",
            "4 por cada Punkalabaza que tenga tu peña al acabar; 6 si quien la lleva está en tu zona de despliegue.", 2,
            PunkalabazasPdf, HasArt: false),
    ];

    /// <summary>Escenario suelto Punkalabazas (el mismo que la fuente "punkalabazas" de core.json).</summary>
    public const string PunkalabazasPdf = "https://punkapocalyptic.com/descargasweb/Punkalabazas.pdf";

    private static readonly Dictionary<string, Mission> ById = All.ToDictionary(m => m.Id, StringComparer.Ordinal);

    public static Mission? Find(string? id) => id is not null && ById.TryGetValue(id, out var mission) ? mission : null;

    /// <summary>Misión al azar. Si se pasa la actual, nunca repite esa.</summary>
    public static Mission Random(Random random, string? exceptId = null)
    {
        var pool = All.Where(m => m.Id != exceptId).ToList();
        return pool[random.Next(pool.Count)];
    }

    /// <summary>Enlace al PDF abierto por la página de la misión.</summary>
    public static string PdfLink(Mission mission) => $"{mission.Pdf ?? PdfUrl}#page={mission.Page}";
}
