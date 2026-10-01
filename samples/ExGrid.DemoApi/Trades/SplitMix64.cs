namespace ExGrid.DemoApi;

/// <summary>
/// Steele, Lea and Flood's SplitMix64: a few lines, written out here rather than taken from
/// <see cref="Random"/>, whose seeded sequence the runtime does not promise to keep. The data
/// has to be the same for the same count on every machine and every runtime (ADR-0069), and a
/// generator this small cannot drift.
/// </summary>
internal struct SplitMix64
{
    private const ulong Gamma = 0x9E3779B97F4A7C15;

    private ulong _state;

    /// <summary>
    /// The numbers for one member of a family, such as one trade of the generated data or one
    /// tick of the live updates. Each member starts from its own mixed state, so any member's
    /// numbers can be had without the members before it, and two members' numbers do not
    /// overlap as consecutive seeds' would.
    /// </summary>
    public SplitMix64(ulong seed, ulong member) => _state = Mix(seed + (member + 1) * Gamma);

    /// <summary>The next 64 random bits.</summary>
    public ulong Next()
    {
        _state += Gamma;
        return Mix(_state);
    }

    /// <summary>A number from 0 up to, not including, <paramref name="bound"/>. Lemire's
    /// multiply-shift, whose bias is far below anything a demo can see.</summary>
    public int Next(int bound) => (int)(((Next() >> 32) * (ulong)bound) >> 32);

    /// <summary>A number from 0 up to, not including, <paramref name="bound"/>.</summary>
    public long NextLong(long bound) => (long)Math.BigMul(Next(), (ulong)bound, out _);

    private static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
        return z ^ (z >> 31);
    }
}
