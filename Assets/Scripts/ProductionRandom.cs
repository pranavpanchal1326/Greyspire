using System;
using UnityEngine;

// Portable, versioned xorshift32 streams. Cosmetic consumption never advances
// combat, AI, spawn, loot-selection or pickup-motion state.
public sealed class RandomStream
{
    public uint State { get; private set; }
    public RandomStream(uint seed) { State = seed == 0 ? 0x6D2B79F5u : seed; }
    public uint Next() { uint x = State; x ^= x << 13; x ^= x >> 17; x ^= x << 5; return State = x; }
    public float value => (Next() >> 8) * (1f / 16777216f);
    public int Range(int min, int max) => max <= min ? min : min + (int)(Next() % (uint)(max - min));
    public float Range(float min, float max) => Mathf.Lerp(min, max, value);
    public Vector2 insideUnitCircle { get { float angle = value * Mathf.PI * 2, radius = Mathf.Sqrt(value); return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius; } }
    public Vector3 onUnitSphere { get { float z = Range(-1f, 1f), a = value * Mathf.PI * 2, r = Mathf.Sqrt(Mathf.Max(0, 1-z*z)); return new Vector3(r*Mathf.Cos(a), z, r*Mathf.Sin(a)); } }
    public Vector3 insideUnitSphere => onUnitSphere * Mathf.Pow(value, 1f/3f);
}
public static class ProductionRandom
{
    public const int PolicyVersion = 1;
    public const string ContentVersion = "legacy30-chapter-v3";
    public const string BuildVersion = "greyspire-g8-w01-v1";
    public static int? OverrideSeedForTest;
    static string campaign = "", scope = "startup";
    static int root, spawnIndex;
    static RandomStream combat, cosmetic, audio, narrative, scatter, spawn;
    public static uint Hash(string text)
    {
        uint hash = 2166136261;
        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(text ?? "")) { hash ^= b; hash *= 16777619; }
        return hash == 0 ? 1u : hash;
    }
    public static int Root
    {
        get
        {
            if (OverrideSeedForTest.HasValue) return OverrideSeedForTest.Value;
            if (JourneyStore.Error != null) return root;
            string current = JourneyStore.Campaign;
            if (campaign == current && JourneyStore.HasKey("productionSeed")) return root;
            int seed = unchecked((int)Hash(current));
            JourneyStore.Transaction(() =>
            {
                if (JourneyStore.GetInt("seedPolicy", 0) > PolicyVersion) throw new NotSupportedException("Future random-stream policy; open this journey with its newer build.");
                if (!JourneyStore.HasKey("productionSeed")) JourneyStore.SetInt("productionSeed", seed);
                JourneyStore.SetInt("seedPolicy", PolicyVersion);
            });
            campaign = current; root = JourneyStore.GetInt("productionSeed", seed); return root;
        }
    }
    public static string Scope => scope;
    public static RandomStream Stream(string domain, string identity) => new RandomStream(Hash(Root.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":v1:" + domain + ":" + identity));
    public static void BeginScene(string identity)
    {
        scope = identity; spawnIndex = 0;
        combat = Stream("combat", identity); cosmetic = Stream("cosmetic", identity);
        audio = Stream("audio", identity); narrative = Stream("narrative", identity);
        scatter = Stream("pickup-motion", identity); spawn = Stream("spawn", identity);
    }
    public static string NextSpawnId() => scope + ":actor:" + (++spawnIndex).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
    public static string ChildId(string parent, int branch) => parent + ":split:" + branch.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static RandomStream Combat => combat ?? (combat = Stream("combat", scope));
    public static RandomStream Cosmetic => cosmetic ?? (cosmetic = Stream("cosmetic", scope));
    public static RandomStream Audio => audio ?? (audio = Stream("audio", scope));
    public static RandomStream Narrative => narrative ?? (narrative = Stream("narrative", scope));
    public static RandomStream Scatter => scatter ?? (scatter = Stream("pickup-motion", scope));
    public static RandomStream Spawn => spawn ?? (spawn = Stream("spawn", scope));
    public static float LootValue()
    {
        float result = 0;
        JourneyStore.Transaction(() =>
        {
            var stream = new RandomStream(unchecked((uint)JourneyStore.GetInt("rng:loot", (int)Hash(Root.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":loot-v1"))));
            result = stream.value; JourneyStore.SetInt("rng:loot", unchecked((int)stream.State));
        });
        return result;
    }
    public static void ClearRuntimeStreams() { combat = cosmetic = audio = narrative = scatter = spawn = null; campaign = ""; }
}
