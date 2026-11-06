using System.Text.RegularExpressions;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Matches;

public enum AidKind
{
    /// <summary>Un número que sube y baja: dosis, cargas, marcadores…</summary>
    Counter,

    /// <summary>Regla de una vez por partida: se marca al usarla.</summary>
    Once,

    /// <summary>Solo un recordatorio que se calcula con lo que pasa en la mesa (no se guarda nada).</summary>
    Info,
}

/// <summary>Lo que hace falta para escribir la nota de una ayuda con el estado de la partida.</summary>
public sealed record AidState(int Value, int Round, int DownAllies, int StandingAllies);

/// <summary>
/// Ayuda para la partida sacada de las reglas de una facción, subfacción o tropa:
/// un contador (la Ambrosía de los Idos, el Mojo unami, las cargas de Combustible…),
/// una regla de un solo uso para tachar, o un recordatorio.
/// </summary>
public sealed class MatchAid
{
    /// <summary>Clave estable con la que se guarda su valor en la partida.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>Qué dice la regla, en corto.</summary>
    public string? Hint { get; init; }

    /// <summary>Nombre de la regla del reglamento, para abrirla entera.</summary>
    public string? Rule { get; init; }

    public AidKind Kind { get; init; } = AidKind.Counter;

    /// <summary>Valor al empezar la partida.</summary>
    public int Start { get; init; }

    public int Max { get; init; } = ActiveMatch.MaxCount;

    /// <summary>En un contador de miembro: cada punto que sube lo gasta de este contador de la banda (dar una dosis).</summary>
    public string? Pool { get; init; }

    /// <summary>En un contador de banda: lo que suma al empezar cada ronda por cada miembro en pie (Mojo unami).</summary>
    public int PerStandingEachRound { get; init; }

    /// <summary>Línea que cambia con el valor: "La próxima tira 1d10 −2".</summary>
    public Func<AidState, string?>? Note { get; init; }

    /// <summary>Clave de un miembro en los contadores de la partida.</summary>
    public static string MemberKey(string key, int index) => $"{key}@{index}";

    /// <summary>Clave donde se apunta la última ronda en la que se sumó <see cref="PerStandingEachRound"/>.</summary>
    public static string RefillKey(string key) => $"{key}#ronda";
}

/// <summary>Contador de la banda que sube cuando cae un miembro concreto (Hasta el coño me tenéis).</summary>
public sealed record DownFeed(string Key, int Amount);

/// <summary>Ayudas de una banda en una partida: las de toda la banda y las de cada miembro, por su orden.</summary>
public sealed class BandAids
{
    public static readonly BandAids None = new([], [], []);

    public BandAids(IReadOnlyList<MatchAid> band, IReadOnlyList<IReadOnlyList<MatchAid>> members, IReadOnlyList<DownFeed?> downFeeds)
    {
        Band = band;
        Members = members;
        DownFeeds = downFeeds;
    }

    public IReadOnlyList<MatchAid> Band { get; }

    public IReadOnlyList<IReadOnlyList<MatchAid>> Members { get; }

    public IReadOnlyList<DownFeed?> DownFeeds { get; }

    public IReadOnlyList<MatchAid> Of(int index) => index >= 0 && index < Members.Count ? Members[index] : [];

    public DownFeed? DownFeedOf(int index) => index >= 0 && index < DownFeeds.Count ? DownFeeds[index] : null;

    public MatchAid? FindBand(string key) => Band.FirstOrDefault(a => a.Key == key);

    public bool IsEmpty => Band.Count == 0 && Members.All(m => m.Count == 0);

    /// <summary>Valor de una ayuda: lo apuntado en la partida o, si no se ha tocado, el de partida.</summary>
    public static int ValueOf(IReadOnlyDictionary<string, int> counters, MatchAid aid, int? index = null) =>
        counters.TryGetValue(index is { } i ? MatchAid.MemberKey(aid.Key, i) : aid.Key, out var value) ? value : aid.Start;
}

/// <summary>
/// Saca de las reglas de cada facción, subfacción y tropa las ayudas prácticas para llevar la partida.
/// Cada ayuda nombra la tropa o la regla del reglamento de la que sale; los tests comprueban que existen.
/// </summary>
public sealed partial class MatchAidCatalog(Rulebook book, UnitCostCalculator costs, GearLookup gear)
{
    // Tropas con nombre fijo en las reglas que se repiten.
    private const string Despojo = "Despojo";
    private const string Madre = "Madre";
    private const string Aberracion = "Aberración";
    private const string Abominacion = "Abominación";
    private const string Grumete = "Grumete";
    private const string Renegado = "Renegado";

    /// <summary>Tropas y reglas que usa el catálogo, para que los tests comprueben que siguen en el reglamento.</summary>
    public static IReadOnlyList<(string FactionId, string? SubfactionId, string Troop)> ReferencedTroops { get; } =
    [
        ("idos", null, Despojo), ("idos", null, Madre), ("idos", null, Aberracion), ("idos", null, "Glotón"),
        ("idos", "hermandad-de-la-bestia", Abominacion), ("idos", "slashers", "El Abuelo de la Cabaña"),
        ("idos", "devoradores", "Necrófago"), ("idos", null, "Desgarrador"),
        ("unami", "descastados", Renegado), ("unami", null, "Elaichik"),
        ("craabianos", null, Grumete), ("craabianos", null, "Davinnie Jones"),
        ("irradiados", null, "Cardenal Nuclear"), ("irradiados", null, "El Guardián de las Llaves"),
        ("irradiados", "inquisicion-atomica", "Inquisidor"), ("irradiados", "inquisicion-atomica", "La Gran Inquisidora"),
        ("irradiados", "rebelion-isotopica", "Científico"), ("irradiados", "herederos-mecanicos", "Robotrón"),
        ("mutardos", null, "Magmador"), ("mutardos", null, "Mutante"),
        ("pandilleros", null, "Clint Tirofijo"), ("pandilleros", "fraus", "Amacizona"), ("pandilleros", "fraus", "Callisto"),
        ("sangre-negra", null, "Chispa"), ("sangre-negra", null, "Cunnilingus Igni"),
        ("sangre-negra", "rugientes", "Chispa rugiente"), ("sangre-negra", "rugientes", "Flammeus Asinum"),
        ("sangre-negra", "crematorio", "Pirómano"), ("sangre-negra", "crematorio", "Chamusquina"),
        ("troupe", null, "Forzudo"), ("troupe", null, "Jefe de pista"), ("troupe", null, "Madame Destino"),
        ("troupe", "ilusionistas", "Artista"),
        ("v-reich", null, "Sanitario"), ("v-reich", "organizacion-kraken", "Casco Rojo"),
        ("v-reich", "division-cientifica", "Doktor Eustace Hanneman"),
        ("chatarreros", "innovadores", "El Profesor"), ("chatarreros", "pioneros", "Lucky Lucy"),
        ("banda-del-comic", null, "Nicole"), ("banda-del-comic", null, "Dan"), ("banda-del-comic", null, "Dan novato"),
        ("mercenarios", null, "Ochodedos"), ("mercenarios", null, "Devin Crow"), ("mercenarios", null, "Crazy Mel"),
        ("mercenarios", null, "Lizzy DeVille"),
    ];

    /// <summary>Ayudas para una banda. <paramref name="roles"/> son los tipos de tropa de los miembros en el orden de la partida.</summary>
    public BandAids For(Band? band, IReadOnlyList<string> roles)
    {
        if (band is null)
        {
            return BandAids.None;
        }

        var factionId = band.FactionId;
        var subfactionId = band.SubfactionId;
        var troops = roles.Select(r => costs.FindTroop(r, factionId, subfactionId)?.Name ?? r.Trim()).ToList();
        var gearByIndex = roles.Select((_, i) => GearOf(band, i)).ToList();
        var has = new HashSet<string>(troops, StringComparer.OrdinalIgnoreCase);

        var bandAids = new List<MatchAid>();
        var memberAids = troops.Select(_ => new List<MatchAid>()).ToList();
        var downFeeds = new DownFeed?[troops.Count];

        void ForMembers(Func<string, int, bool> applies, MatchAid aid)
        {
            for (var i = 0; i < troops.Count; i++)
            {
                if (applies(troops[i], i))
                {
                    memberAids[i].Add(aid);
                }
            }
        }

        void ForTroop(string troop, MatchAid aid) => ForMembers((t, _) => Is(t, troop), aid);

        var cost = band.Cost ?? 0;

        switch (factionId)
        {
            case "idos":
                AddIdos(subfactionId, cost, troops, has, bandAids, memberAids, ForMembers, ForTroop);
                break;

            case "unami":
                AddUnami(subfactionId, troops, bandAids);
                break;

            case "craabianos":
                if (subfactionId == "resacados")
                {
                    ForMembers((_, _) => true, new MatchAid
                    {
                        Key = "bebida", Name = "Ya ha bebido", Kind = AidKind.Once, Rule = "¿Cocido o con resacón?",
                        Hint = "Una vez por partida, al empezar un Turno: quita la resaca y, si compró bebida, gana su efecto 2 Turnos.",
                    });
                }
                else
                {
                    ForMembers((t, _) => !Is(t, Grumete), new MatchAid
                    {
                        Key = "mar", Name = "El mar siempre reclama", Kind = AidKind.Once, Rule = "El mar siempre reclama",
                        Hint = "Repite una tirada de Atributo y después pierde 1 punto en ese Atributo.",
                    });
                }

                ForTroop("Davinnie Jones", new MatchAid
                {
                    Key = "mala-hostia", Name = "Mala hostia", Rule = "Cargado de mala hostia",
                    Hint = "Un marcador cada vez que un aliado falla una tirada.",
                    Note = s => s.Value >= 3 ? "Puede gastar 3: ignorar Sangre salada en un Asalto o recargar gratis." : null,
                });
                break;

            case "irradiados":
                ForTroop("Cardenal Nuclear", Once("sermon", "Sermón", "Sermón", "Elige uno tras desplegar y úsalo una vez."));
                ForTroop("El Guardián de las Llaves", Once("maletin", "El Maletín", "El Maletín", "Radiación con −2 hasta su siguiente activación."));
                ForMembers((t, _) => Is(t, "Inquisidor") || Is(t, "La Gran Inquisidora"),
                    Once("colera", "Parábola de la cólera nuclear", "Sermón", "Repite una tirada de Fuerza contra Dureza."));
                ForTroop("Científico", Once("eso-es-todo", "Eso es todo, amigos", "Eso es todo, amigos", "Detona un arma con Fusión."));
                if (subfactionId == "herederos-mecanicos")
                {
                    ForMembers((t, _) => Is(t, "Cardenal Nuclear") || Is(t, "Penitente"),
                        Once("volvere", "Ya tiró Volveré", "Volveré", "Al caer, 1d10: con 9-10 era un Cyborg."));
                }

                break;

            case "mutardos":
                ForTroop("Magmador", Once("charco", "Charco de magma", "Charco de magma", "2 Acciones seguidas: 10×10 cm de Terreno impasable."));
                if (subfactionId == "fuerza-m")
                {
                    ForTroop("Mutante", Once("sidekick", "Sidekick caído", "Para que no me disparen", "Si lleva Sidekick, cae él en su lugar una vez."));
                }

                break;

            case "pandilleros":
                ForTroop("Clint Tirofijo", Once("alegrame", "Alégrame el día", "Alégrame el día", "Sin balas, dispara igual: par le quedaba una."));
                if (subfactionId == "flipaos-de-los-ninjas")
                {
                    ForMembers((_, _) => true, Once("ahora-me-ves", "Ahora me ves…", "Ahora me ves…", "Cuando le van a Asaltar o Disparar, se recoloca hasta 10 cm."));
                }

                if (subfactionId == "fraus")
                {
                    bandAids.Add(new MatchAid
                    {
                        Key = "hasta-el-cono", Name = "Hasta el coño", Rule = "Hasta el coño me tenéis",
                        Hint = "Cada Frau Abatida da 1 marcador (Amacizona 2, Callisto 3). Se suman solos al marcarlas como baja.",
                        Note = s => s.Value > 0 ? "Al Asaltar, una Frau gasta hasta 3: +5 cm cada uno a su último Movimiento." : null,
                    });
                    for (var i = 0; i < troops.Count; i++)
                    {
                        downFeeds[i] = new DownFeed("hasta-el-cono", Is(troops[i], "Callisto") ? 3 : Is(troops[i], "Amacizona") ? 2 : 1);
                    }
                }

                break;

            case "sangre-negra":
                ForMembers((t, _) => Is(t, "Chispa") || Is(t, "Chispa rugiente"), new MatchAid
                {
                    Key = "moto", Name = "Combustible de la moto", Start = 2, Rule = "Darle caña",
                    Hint = "1 carga: turbo, potencia bruta o nube de polvo.",
                });
                ForTroop("Flammeus Asinum", new MatchAid { Key = "moto", Name = "Combustible del quad", Start = 3, Rule = "Intruder" });
                ForTroop("Chispa", Once("mirada-extra", "Carga extra", "Bajo la mirada llameante", "Una vez, con su Guardián de la Llama mirando."));
                ForTroop("Cunnilingus Igni", new MatchAid { Key = "chorrazo", Name = "Cargas del Chorrazo", Start = 5, Rule = "Chorrazo", Hint = "Sin cargas es un filo pequeño." });
                if (subfactionId == "crematorio")
                {
                    bandAids.Add(new MatchAid
                    {
                        Key = "churruscado", Name = "Churruscado", Rule = "El poder del fuego",
                        Hint = "Cada baja por llamas da 1. Al empezar el Turno, cada uno gastado sube una Ronda de Agilidad.",
                    });
                    ForTroop("Pirómano", new MatchAid
                    {
                        Key = "dale-calor", Name = "Dale calor", Rule = "Dale calor",
                        Note = s => s.Value > 0 ? $"+{s.Value} a Combate y −{s.Value} a Dureza." : null,
                    });
                }

                if (subfactionId == "nuevos-hijos")
                {
                    bandAids.Add(new MatchAid
                    {
                        Key = "perres", Name = "Perres reservados", Rule = "Será por perres",
                        Hint = "Puntos sin gastar (hasta 25 por cada fracción de 200). Se gastan al activar una miniatura.",
                    });
                }

                break;

            case "troupe":
                ForTroop("Forzudo", Once("lucimiento", "Lucimiento", "Lucimiento", "Al empezar un Turno: +2 a Fuerza hasta el final del Turno."));
                foreach (var effect in Bullets(TroopRule("troupe", "Jefe de pista", "El mayor espectáculo del mundo")))
                {
                    ForTroop("Jefe de pista", Once($"espectaculo-{RulesText.Slug(effect)}", effect, "El mayor espectáculo del mundo"));
                }

                ForTroop("Madame Destino", new MatchAid
                {
                    Key = "cartas", Name = "Cartas echadas", Max = 3, Rule = "Vidente",
                    Note = s => s.Value < CardsAllowed(s.Round) ? "Le toca una carta en estos Turnos." : null,
                });
                AddJokes(gearByIndex, ForMembers);
                if (subfactionId == "ilusionistas")
                {
                    bandAids.Add(new MatchAid
                    {
                        Key = "truco", Name = "Puntos de Truco", Start = 3 * troops.Count(t => Is(t, "Artista")), Rule = "La Senda de la Ilusión",
                        Hint = "Cada Artista da 3 al empezar y 1 por cada Acción gastada.",
                    });
                }

                break;

            case "v-reich":
                if (has.Contains("Sanitario"))
                {
                    ForMembers((_, _) => true, Once("parcheado", "Ya parcheado", "Primeros auxilios", "Cada miniatura solo se parchea una vez."));
                }

                ForTroop("Casco Rojo", Once("kraken", "Liberad al Kraken", "Liberad al Kraken", "Los Gólems que actúen ese Turno: +1 a Combate y Precisión."));
                ForTroop("Doktor Eustace Hanneman", new MatchAid
                {
                    Key = "wunderpincho", Name = "Wunderpincho", Rule = "Wunderpincho",
                    Note = s => s.Value > 0 ? $"+{s.Value} a Fuerza y Penetración al usarlo en Combate." : null,
                });
                break;

            case "chatarreros":
                ForTroop("El Profesor", new MatchAid
                {
                    Key = "voy-pedo", Name = "Repeticiones", Rule = "Voy pedo, no idiota",
                    Note = s => s.Value > 0 ? $"−{s.Value} a todos sus Atributos salvo Acciones y Técnica." : null,
                });
                ForTroop("Lucky Lucy", new MatchAid { Key = "sammy", Name = "Combustible de Sammy", Start = 6, Rule = "Sammy" });
                break;

            case "banda-del-comic":
                ForTroop("Nicole", new MatchAid
                {
                    Key = "voluntariosa", Name = "Voluntariosa", Max = 3, Rule = "Voluntariosa",
                    Hint = "+1 a Dureza por Acción gastada. Se pierde tras la primera tirada de Dureza contra un ataque.",
                    Note = s => s.Value > 0 ? $"+{s.Value} a Dureza." : null,
                });
                ForMembers((t, _) => Is(t, "Dan") || Is(t, "Dan novato"), new MatchAid
                {
                    Key = "companerismo", Name = "Compañerismo", Kind = AidKind.Info, Rule = "Compañerismo",
                    Note = s => Companerismo(s.DownAllies),
                });
                break;
        }

        // Mercenarios: pueden ir en cualquier banda.
        ForTroop("Ochodedos", Once("san-vito", "El baile de San Vito", "El baile de San Vito", "La primera vez que cae lo ignora y gana +2 a Agilidad, Fuerza y Dureza."));
        ForTroop("Crazy Mel", Once("escopeta-rota", "Escopeta rota", "En el momento menos oportuno", "Con un par en Precisión se estropea para el resto de la partida."));
        ForTroop("Devin Crow", new MatchAid
        {
            Key = "deseos", Name = "Deseos pedidos", Max = 3, Rule = "Monkeys Paw",
            Note = s => s.Value >= 3 ? "Ya no le quedan deseos." : s.Value > 0 ? $"El siguiente tiene −{s.Value} a Técnica." : null,
        });
        ForTroop("Lizzy DeVille", Once("charlatana", "Charlatana", "Charlatana", "Una vez por enemigo: se separa como si fuera Escurridiza."));

        AddFuel(factionId, troops, gearByIndex, memberAids);

        return new BandAids(bandAids, memberAids.Select(m => (IReadOnlyList<MatchAid>)m).ToList(), downFeeds);
    }

    private static void AddIdos(
        string? subfactionId, int cost, List<string> troops, HashSet<string> has, List<MatchAid> bandAids,
        List<List<MatchAid>> memberAids, Action<Func<string, int, bool>, MatchAid> forMembers, Action<string, MatchAid> forTroop)
    {
        if (subfactionId == "devoradores")
        {
            // Canibalismo: no usan Ambrosía, devoran casi todos.
            forMembers((t, _) => Is(t, "Glotón") || Is(t, Aberracion) || Is(t, "Desgarrador") || Is(t, "Necrófago"), new MatchAid
            {
                Key = "devorados", Name = "Devorados", Rule = "Canibalismo",
                Hint = "1 Acción sobre un Abatido en contacto y tira en la tabla de Canibalismo.",
            });
            return;
        }

        var madre = has.Contains(Madre);
        bandAids.Add(new MatchAid
        {
            Key = "ambrosia", Name = "Ambrosía", Start = Fractions(cost, 100), Rule = "Ambrosía",
            Hint = "1 dosis por cada fracción de 100 puntos. Suma aquí las que saquen los Cosechadores.",
        });

        if (has.Contains("El Abuelo de la Cabaña"))
        {
            bandAids.Add(new MatchAid
            {
                Key = "ambrosia-nina", Name = "Ambrosía de la Niña", Start = 1, Rule = "Toma y que no se entere tu madre",
                Hint = "Una carga extra solo para la Niña.",
            });
        }

        // Escoria: los Despojos no toman Ambrosía; Madre nunca la toma; la Abominación no puede.
        for (var i = 0; i < troops.Count; i++)
        {
            var troop = troops[i];
            if (Is(troop, Despojo) || Is(troop, Madre) || Is(troop, Abominacion))
            {
                continue;
            }

            memberAids[i].Add(new MatchAid
            {
                Key = "dosis", Name = "Dosis tomadas", Pool = "ambrosia", Rule = "Ambrosía",
                Hint = "Darle una dosis la gasta de la Ambrosía de la banda.",
                Note = s => DoseNote(troop, s.Value, madre),
            });
        }

        forTroop("Glotón", new MatchAid
        {
            Key = "devorados", Name = "Devorados", Rule = "Devorador",
            Note = s => s.Value > 0 ? $"+{s.Value} a Combate, Fuerza y Dureza." : null,
        });

        if (subfactionId == "slashers")
        {
            bandAids.Add(new MatchAid
            {
                Key = "victimas", Name = "Marcadores de víctima", Rule = "No estaba muerto",
                Hint = "Uno por cada enemigo que Abate la banda. Si uno cae, 1d10 igual o menos y vuelve.",
            });
            forMembers((_, _) => true, Once("no-estaba-muerto", "Ya tiró No estaba muerto", "No estaba muerto"));
        }
    }

    /// <summary>"La próxima tira 1d10 −2": cada dosis ya tomada resta 1 y la Aberración, 2 más.</summary>
    public static string? DoseNote(string troop, int taken, bool madreInBand)
    {
        var penalty = taken + (Is(troop, Aberracion) ? 2 : 0);
        var roll = penalty == 0 ? "La próxima tira 1d10 sin restar." : $"La próxima tira 1d10 −{penalty}.";
        return madreInBand ? $"{roll} A 20 cm de Madre no tira." : roll;
    }

    private static void AddUnami(string? subfactionId, List<string> troops, List<MatchAid> bandAids)
    {
        if (subfactionId == "descastados")
        {
            bandAids.Add(new MatchAid
            {
                Key = "mojo", Name = "Puntos de Mojo", Start = troops.Count(t => !Is(t, Renegado)), Rule = "Muchas mentes",
                Hint = "1 por Descastado (no Renegados). Un Don a su coste o 1 Acción extra. No se recuperan.",
            });
            return;
        }

        bandAids.Add(new MatchAid
        {
            Key = "mojo", Name = "Puntos de Mojo", PerStandingEachRound = 2, Rule = "Una mente",
            Hint = "2 por cada miniatura en pie al empezar el Turno. Cada una convierte PdM en Acciones (máximo 3).",
            Note = s => troops.Any(t => Is(t, "Elaichik")) ? "Elaichik suma 2 por cada enemigo que Abate cuerpo a cuerpo." : null,
        });

        if (subfactionId == "apostadores")
        {
            bandAids.Add(Once("suerte-novato", "Suerte del novato", "Suerte del novato", "Una vez en la banda: éxito automático en la siguiente tirada."));
        }
    }

    /// <summary>Cargas de Combustible del equipo que lleva cada miembro (lanzallamas, mochila cohete…).</summary>
    private void AddFuel(string factionId, List<string> troops, List<IReadOnlyList<string>> gearByIndex, List<List<MatchAid>> memberAids)
    {
        for (var i = 0; i < troops.Count; i++)
        {
            foreach (var name in gearByIndex[i])
            {
                var info = gear.Find(name, factionId);
                if (info is null || FuelCharges().Match(info.Summary) is not { Success: true } match)
                {
                    continue;
                }

                var charges = int.Parse(match.Groups[1].Value);
                if (Is(troops[i], "Chamusquina") && Is(info.Name, "Lanzallamas"))
                {
                    charges = 5; // Lanzallamitas.
                }

                var key = $"cargas-{RulesText.Slug(info.Name)}";
                if (memberAids[i].Any(a => a.Key == key))
                {
                    continue;
                }

                memberAids[i].Add(new MatchAid
                {
                    Key = key, Name = $"Cargas · {info.Name}", Start = charges, Max = charges, Rule = "Combustible (X)",
                });
            }
        }
    }

    /// <summary>Artículos de coña de la Troupe: un solo uso cada uno.</summary>
    private void AddJokes(List<IReadOnlyList<string>> gearByIndex, Action<Func<string, int, bool>, MatchAid> forMembers)
    {
        var items = book.FindFaction("troupe")?.Extras.FirstOrDefault(g => g.Title == "Artículos de coña")?.Items ?? [];
        foreach (var item in items)
        {
            var key = RulesText.Fold(item.Name);
            forMembers((_, i) => gearByIndex[i].Any(g => RulesText.Fold(g).StartsWith(key, StringComparison.Ordinal)),
                Once($"cona-{item.Slug}", $"Usado: {item.Name}", item.Name, item.Text));
        }
    }

    private string? TroopRule(string factionId, string troop, string rule) =>
        book.FindFaction(factionId)?.Troops.FirstOrDefault(t => Is(t.Name, troop))?.Rules.FirstOrDefault(r => r.Name == rule)?.Text;

    private static IReadOnlyList<string> GearOf(Band band, int index) =>
        band.Members.ElementAtOrDefault(index) is { } member
            ? member.Gear.Select(g => g.Name).Concat(member.Skills).ToList()
            : [];

    private static IEnumerable<string> Bullets(string? text) =>
        text is null ? [] : BulletName().Matches(text).Select(m => m.Groups[1].Value.Trim());

    private static MatchAid Once(string key, string name, string rule, string? hint = null) =>
        new() { Key = key, Name = name, Rule = rule, Hint = hint, Kind = AidKind.Once, Max = 1 };

    /// <summary>"Por cada fracción de 100 puntos": solo cuentan las completas (335 puntos dan 3).</summary>
    public static int Fractions(int cost, int size) => cost <= 0 ? 0 : cost / size;

    private static int CardsAllowed(int round) => (round + 1) / 2;

    private static string? Companerismo(int down) => down switch
    {
        0 => null,
        1 => "1 compañero caído: +1 a Precisión.",
        2 => "2 caídos: +1 a Precisión y Combate.",
        3 => "3 caídos: +1 a Precisión, Combate y Dureza.",
        _ => $"{down} caídos: +1 a Precisión, Combate, Dureza y Acciones.",
    };

    private static bool Is(string troop, string name) =>
        string.Equals(RulesText.Fold(troop), RulesText.Fold(name), StringComparison.Ordinal);

    [GeneratedRegex(@"Combustible \((\d+)\)")]
    private static partial Regex FuelCharges();

    [GeneratedRegex(@"•\s*([^:\n]+):")]
    private static partial Regex BulletName();
}
