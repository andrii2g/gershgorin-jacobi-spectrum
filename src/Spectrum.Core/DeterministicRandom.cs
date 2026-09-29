namespace Spectrum.Core;

public sealed class DeterministicRandom(ulong seed)
{
    public const string Version = "splitmix64-v1";
    private ulong state = seed;
    public ulong NextUInt64()
    {
        unchecked
        {
            ulong z = state += 0x9E3779B97F4A7C15;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EB;
            return z ^ (z >> 31);
        }
    }
    public double Uniform01() => (NextUInt64() >> 11) * 1.1102230246251565e-16;
    public double UniformSigned() => 2 * Uniform01() - 1;
}
