namespace Punkartuchera.Core.Matches;

/// <summary>
/// Tiradas de dados de la mesa. El reglamento usa dados de 10 caras (Reglas, p. 2):
/// el 10 se lee como 10, no como 0.
/// </summary>
public static class DiceRoll
{
    public const int Faces = 10;
    public const int MinDice = 1;
    public const int MaxDice = 6;

    /// <summary>Tira <paramref name="count"/> d10 (se recorta entre 1 y 6).</summary>
    public static IReadOnlyList<int> Roll(int count, Random? random = null)
    {
        random ??= Random.Shared;
        var dice = new int[Math.Clamp(count, MinDice, MaxDice)];
        for (var i = 0; i < dice.Length; i++)
        {
            dice[i] = random.Next(1, Faces + 1);
        }

        return dice;
    }
}
