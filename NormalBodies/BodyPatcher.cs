using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;

namespace NormalBodies;

public record WeightChange(INpcGetter Npc, BodyType Type, float OldWeight, float NewWeight);

public class BodyPatcher
{
    private static readonly FormKey Player = FormKey.Factory("000007:Skyrim.esm");

    private readonly ILinkCache _linkCache;
    private readonly Settings _settings;
    private readonly BodyClassifier _classifier;

    public BodyPatcher(ILinkCache linkCache, Settings settings)
    {
        _linkCache = linkCache;
        _settings = settings;
        _classifier = new BodyClassifier(linkCache, settings);
    }

    /// <summary>
    /// Decides the new weight for every NPC in <paramref name="npcs"/> (winning overrides)
    /// and returns only the ones that change.
    /// </summary>
    public List<WeightChange> Plan(IEnumerable<INpcGetter> npcs)
    {
        var excluded = _settings.ExcludedNpcs.Select(l => l.FormKey).ToHashSet();
        excluded.Add(Player);

        var candidates = npcs
            .Where(n => !excluded.Contains(n.FormKey))
            .Where(_classifier.IsAdultHumanoid)
            .ToList();

        var types = new Dictionary<FormKey, BodyType>();
        foreach (var npc in candidates)
        {
            types[npc.FormKey] = _classifier.Classify(npc).Type;
        }

        // Many NPCs (most guards and bandits) don't have a weight of their own: they inherit
        // "Traits" from a template NPC. Push their body type onto that template so the
        // NPC that actually owns the weight gets it.
        foreach (var npc in candidates)
        {
            var type = types[npc.FormKey];
            foreach (var template in _classifier.TraitTemplates(npc))
            {
                if (types.TryGetValue(template.FormKey, out var existing) && existing >= type) continue;
                types[template.FormKey] = type;
            }
        }

        var changes = new List<WeightChange>();
        foreach (var npc in candidates)
        {
            if (BodyClassifier.UsesTemplate(npc, NpcConfiguration.TemplateFlag.Traits)) continue;

            var female = npc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Female);
            if (female ? !_settings.AffectFemales : !_settings.AffectMales) continue;

            var type = types[npc.FormKey];
            var range = RangeFor(type);
            var weight = npc.Weight;

            if (_settings.Mode == WeightMode.KeepIfInRange && range.Contains(weight)) continue;

            var target = PickWeight(npc.FormKey, range);
            if (Math.Abs(target - weight) < 0.5f) continue;

            changes.Add(new WeightChange(npc, type, weight, target));
        }
        return changes;
    }

    public WeightRange RangeFor(BodyType type) => type switch
    {
        BodyType.Rugby => _settings.Rugby,
        BodyType.Marathon => _settings.Marathon,
        _ => _settings.Skinny,
    };

    /// <summary>
    /// A weight inside the range that's random-looking but stable: the same NPC always gets
    /// the same weight, so re-running the patcher doesn't reshuffle everyone (and their FaceGen).
    /// </summary>
    public static float PickWeight(FormKey key, WeightRange range)
    {
        var min = Math.Clamp(Math.Min(range.Min, range.Max), 0, 100);
        var max = Math.Clamp(Math.Max(range.Min, range.Max), 0, 100);
        var t = StableHash(key.ToString()) / (double)uint.MaxValue;
        return (float)Math.Round(min + t * (max - min));
    }

    private static uint StableHash(string s)
    {
        // FNV-1a. string.GetHashCode() is randomised per process, so it can't be used here.
        var hash = 2166136261u;
        foreach (var c in s)
        {
            hash ^= c;
            hash *= 16777619u;
        }
        return hash;
    }
}
