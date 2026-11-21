using System.Runtime.CompilerServices;
using System.Text;
using Punkartuchera.Core.Models;
using Punkartuchera.Core.Rules;

namespace Punkartuchera.Core.Tests;

/// <summary>
/// Foto de cómo se comportan las reglas con todo el catálogo: para cada tropa de cada facción y
/// subfacción, su límite de armas, cada compra que ofrece (y si se puede añadir) y, con cada compra
/// puesta, el Blindaje, las armas a distancia y los avisos. Si un cambio en los JSON o en el código
/// altera algo, este test lo enseña aunque ningún otro lo pille.
/// Para aceptar un cambio a propósito: <c>PUNK_ACTUALIZAR_FOTO=1 dotnet test</c> y revisar el diff.
/// </summary>
public class RuleBehaviourSnapshotTests
{
    private const string SnapshotFile = "rule-behaviour.snapshot.txt";

    private static readonly Rulebook Book = RulebookLoader.LoadEmbedded();
    private static readonly GearLookup Lookup = new(Book);
    private static readonly UnitCostCalculator Costs = new(Book);
    private static readonly RosterBuilder Roster = new(Book, new RulebookCatalog(Book).Stats);
    private static readonly LoadoutAdvisor Advisor = new(Book, Lookup, Costs);
    private static readonly MemberArmor Armor = new(Book, Lookup, Costs);
    private static readonly MemberWeapons Weapons = new(Lookup);

    [Fact]
    public void The_whole_catalog_behaves_as_recorded()
    {
        var actual = Build();
        var path = Path.Combine(SourceDirectory(), SnapshotFile);

        if (Environment.GetEnvironmentVariable("PUNK_ACTUALIZAR_FOTO") == "1" || !File.Exists(path))
        {
            File.WriteAllText(path, actual);
        }

        var expected = File.ReadAllText(path).ReplaceLineEndings("\n");
        if (expected == actual)
        {
            return;
        }

        var a = expected.Split('\n');
        var b = actual.Split('\n');
        var line = Enumerable.Range(0, Math.Min(a.Length, b.Length)).FirstOrDefault(i => a[i] != b[i], Math.Min(a.Length, b.Length));
        Assert.Fail($"Las reglas se comportan distinto desde la línea {line + 1}:\n  antes: {a.ElementAtOrDefault(line)}\n  ahora: {b.ElementAtOrDefault(line)}");
    }

    private static string Build()
    {
        var text = new StringBuilder();
        foreach (var faction in Book.Factions)
        {
            Write(text, faction.Id, null, faction.Troops);
            foreach (var subfaction in faction.Subfactions)
            {
                Write(text, faction.Id, subfaction.Id, subfaction.Troops);
            }
        }

        return text.ToString();
    }

    private static void Write(StringBuilder text, string factionId, string? subfactionId, IEnumerable<Troop> troops)
    {
        foreach (var troop in troops)
        {
            var member = Roster.FromTroop(troop, factionId);
            var loadout = Advisor.Advise(member, factionId, subfactionId);
            text.Append("## ").Append(factionId).Append(subfactionId is null ? "" : "/" + subfactionId).Append(" · ").Append(troop.Name).Append('\n');
            text.Append("límite: ").Append(loadout.Limit is { } l ? $"{l.Min}-{l.Max}" : "-").Append('\n');
            text.Append("base: ").Append(State(member, factionId, subfactionId)).Append('\n');

            foreach (var option in loadout.Gear.Concat(loadout.Skills))
            {
                text.Append("- ").Append(option.Name).Append(" [").Append(option.Slot).Append("] ");
                if (!option.CanAdd)
                {
                    text.Append("NO: ").Append(option.Blocked).Append('\n');
                    continue;
                }

                text.Append(State(With(member, option), factionId, subfactionId)).Append('\n');
            }
        }
    }

    private static Miniature With(Miniature member, LoadoutOption option)
    {
        var copy = new Miniature
        {
            Name = member.Name,
            Role = member.Role,
            Stats = new(member.Stats),
            Gear = member.Gear.Select(g => new Equipment { Name = g.Name }).ToList(),
            Skills = [.. member.Skills],
            Included = [.. member.Included],
            BaseCost = member.BaseCost,
        };
        var names = option.Slot == LoadoutSlot.Skill ? copy.Skills : copy.Gear.Select(g => g.Name).ToList();
        LoadoutAdvisor.Add(names, option);
        if (option.Slot != LoadoutSlot.Skill)
        {
            copy.Gear = names.Select(n => new Equipment { Name = n }).ToList();
        }

        return copy;
    }

    private static string State(Miniature member, string factionId, string? subfactionId)
    {
        var armor = Armor.For(member, factionId, subfactionId);
        var ranged = Weapons.Of(member, factionId).Select(w => $"{w.Name}:{w.Kind}{(w.ReloadActions > 0 ? w.ReloadActions : "")}{(w.Quantity > 1 ? "x" + w.Quantity : "")}").ToList();
        var warnings = Advisor.Advise(member, factionId, subfactionId).Warnings;
        return $"Bl {armor.Value} ({string.Join(", ", armor.Sources)}){(armor.Situational.Count > 0 ? " sit: " + string.Join(" | ", armor.Situational) : "")}"
            + $" · dist: {(ranged.Any() ? string.Join(", ", ranged) : "-")}"
            + (warnings.Count > 0 ? " · avisos: " + string.Join(" | ", warnings) : "");
    }

    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
