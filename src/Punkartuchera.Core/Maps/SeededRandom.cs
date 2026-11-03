namespace Punkartuchera.Core.Maps;

/// <summary>
/// Generador pseudoaleatorio propio (mulberry32). System.Random no garantiza la misma
/// secuencia entre versiones de .NET, y un código de mapa tiene que dar siempre el mismo mapa.
/// </summary>
internal sealed class SeededRandom(uint seed)
{
    private uint _state = seed;

    /// <summary>Número en [0, 1).</summary>
    public double NextDouble()
    {
        var t = _state += 0x6D2B79F5u;
        t = (t ^ (t >> 15)) * (t | 1u);
        t ^= t + (t ^ (t >> 7)) * (t | 61u);
        return (t ^ (t >> 14)) / 4294967296.0;
    }

    /// <summary>Entero en [min, max].</summary>
    public int Next(int min, int max) => min + (int)(NextDouble() * (max - min + 1));

    public bool NextBool() => NextDouble() < 0.5;
}
