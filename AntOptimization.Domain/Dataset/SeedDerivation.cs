namespace AntOptimization.Domain.Dataset;

/// <summary>
/// Deterministic, platform-independent seed derivation (FNV-1a) so that every instance and
/// every ACO run can be reproduced from the job's master seed.
/// (string.GetHashCode is randomized per process and therefore unsuitable.)
/// </summary>
public static class SeedDerivation
{
    public static int ForInstance(int masterSeed, int instanceIndex, int attempt = 0) =>
        Hash($"instance|{masterSeed}|{instanceIndex}|{attempt}");

    public static int ForRun(int instanceSeed, string configKey, int repetition) =>
        Hash($"run|{instanceSeed}|{configKey}|{repetition}");

    private static int Hash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in value)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
