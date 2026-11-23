using Mutagen.Bethesda;
using System.Text.RegularExpressions;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;

namespace NormalBodies;

/// <summary>The three builds, ordered from least to most muscular.</summary>
public enum BodyType
{
    Skinny = 0,
    Marathon = 1,
    Rugby = 2,
}

/// <summary>Why an NPC got the body type it did (useful for the log and for tests).</summary>
public record Classification(BodyType Type, bool IsGuard, bool IsFighter, bool IsRich);

/// <summary>
/// Works out which build an NPC should have from its class, factions and outfit,
/// following NPC templates for any data the NPC inherits.
/// </summary>
public class BodyClassifier
{
    private const int MaxTemplateDepth = 10;

    private static readonly Skill[] CombatSkills =
    {
        Skill.OneHanded, Skill.TwoHanded, Skill.Archery, Skill.Block, Skill.HeavyArmor, Skill.LightArmor,
    };

    private readonly ILinkCache _linkCache;
    private readonly Settings _settings;
    private readonly Regex _guard;
    private readonly Regex _fighterFaction;
    private readonly Regex _richFaction;
    private readonly HashSet<string> _richKeywords;
    private readonly Dictionary<FormKey, string?> _editorIds = new();
    private readonly Dictionary<FormKey, bool> _richItems = new();

    public BodyClassifier(ILinkCache linkCache, Settings settings)
    {
        _linkCache = linkCache;
        _settings = settings;
        _guard = new Regex(settings.GuardPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        _fighterFaction = new Regex(settings.FighterFactionPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        _richFaction = new Regex(settings.RichFactionPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        _richKeywords = new HashSet<string>(settings.RichArmorKeywords, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>True for adult NPCs of a humanoid ("ActorTypeNPC") race, i.e. ones that have a weight slider.</summary>
    public bool IsAdultHumanoid(INpcGetter npc)
    {
        foreach (var source in Sources(npc, NpcConfiguration.TemplateFlag.Traits))
        {
            if (!source.Race.TryResolve(_linkCache, out var race)) continue;
            if (race.Flags.HasFlag(Race.Flag.Child)) return false;
            if (race.EditorID?.Contains("Child", StringComparison.OrdinalIgnoreCase) == true) return false;
            return race.Keywords?.Any(k => EditorIdOf(k) == "ActorTypeNPC") == true;
        }
        return false;
    }

    public bool IsFemale(INpcGetter npc) =>
        Sources(npc, NpcConfiguration.TemplateFlag.Traits)
            .Any(s => s.Configuration.Flags.HasFlag(NpcConfiguration.Flag.Female));

    public Classification Classify(INpcGetter npc)
    {
        var factionIds = Sources(npc, NpcConfiguration.TemplateFlag.Factions)
            .SelectMany(s => s.Factions)
            .Select(f => EditorIdOf(f.Faction))
            .OfType<string>()
            .ToList();

        var classes = Sources(npc, NpcConfiguration.TemplateFlag.Stats)
            .Select(s => s.Class.TryResolve(_linkCache, out var c) ? c : null)
            .OfType<IClassGetter>()
            .ToList();

        var isGuard =
            (npc.EditorID is { } id && _guard.IsMatch(id))
            || factionIds.Any(_guard.IsMatch)
            || classes.Any(c => c.EditorID is { } cid && _guard.IsMatch(cid));

        var isFighter =
            isGuard
            || classes.Any(IsCombatClass)
            || factionIds.Any(_fighterFaction.IsMatch);

        var isRich = false;
        if (isFighter)
        {
            isRich = factionIds.Any(_richFaction.IsMatch)
                     || Sources(npc, NpcConfiguration.TemplateFlag.Inventory).Any(HasRichOutfit);
        }

        var type = !isFighter ? BodyType.Skinny
            : isRich ? BodyType.Rugby
            : BodyType.Marathon;

        return new Classification(type, isGuard, isFighter, isRich);
    }

    /// <summary>
    /// The NPCs this NPC takes its Traits (and therefore its weight) from.
    /// Empty when the NPC sets its own weight.
    /// </summary>
    public IEnumerable<INpcGetter> TraitTemplates(INpcGetter npc) =>
        UsesTemplate(npc, NpcConfiguration.TemplateFlag.Traits)
            ? Sources(npc, NpcConfiguration.TemplateFlag.Traits)
            : Enumerable.Empty<INpcGetter>();

    public static bool UsesTemplate(INpcGetter npc, NpcConfiguration.TemplateFlag flag) =>
        npc.Configuration.TemplateFlags.HasFlag(flag) && !npc.Template.IsNull;

    public bool IsCombatClass(IClassGetter cls)
    {
        double total = 0, combat = 0;
        foreach (var (skill, weight) in cls.SkillWeights)
        {
            total += weight;
            if (CombatSkills.Contains(skill)) combat += weight;
        }
        return total > 0 && combat / total >= _settings.FighterSkillShare;
    }

    /// <summary>
    /// The NPC records that actually supply the data covered by <paramref name="flag"/>:
    /// the NPC itself if it doesn't inherit it, otherwise whatever its template
    /// (possibly through leveled lists and template chains) resolves to.
    /// </summary>
    public IEnumerable<INpcGetter> Sources(INpcGetter npc, NpcConfiguration.TemplateFlag flag)
    {
        var seen = new HashSet<FormKey>();
        return SourcesInner(npc, flag, 0, seen);
    }

    private IEnumerable<INpcGetter> SourcesInner(INpcGetter npc, NpcConfiguration.TemplateFlag flag, int depth, HashSet<FormKey> seen)
    {
        if (!seen.Add(npc.FormKey)) yield break;

        if (!UsesTemplate(npc, flag) || depth >= MaxTemplateDepth)
        {
            yield return npc;
            yield break;
        }

        var found = false;
        foreach (var next in ResolveSpawn(npc.Template.FormKey, depth))
        {
            foreach (var src in SourcesInner(next, flag, depth + 1, seen))
            {
                found = true;
                yield return src;
            }
        }

        // Broken template: fall back to the NPC's own data, like the game does.
        if (!found) yield return npc;
    }

    private IEnumerable<INpcGetter> ResolveSpawn(FormKey key, int depth)
    {
        if (depth >= MaxTemplateDepth) yield break;

        if (_linkCache.TryResolve<INpcGetter>(key, out var npc))
        {
            yield return npc;
        }
        else if (_linkCache.TryResolve<ILeveledNpcGetter>(key, out var list))
        {
            foreach (var entry in list.Entries ?? Array.Empty<ILeveledNpcEntryGetter>())
            {
                if (entry.Data?.Reference.FormKey is not { } refKey) continue;
                foreach (var n in ResolveSpawn(refKey, depth + 1)) yield return n;
            }
        }
    }

    private bool HasRichOutfit(INpcGetter npc)
    {
        if (!npc.DefaultOutfit.TryResolve(_linkCache, out var outfit) || outfit.Items is null) return false;

        uint value = 0;
        foreach (var item in outfit.Items)
        {
            if (IsRichItem(item.FormKey, 0)) return true;
            if (_linkCache.TryResolve<IArmorGetter>(item.FormKey, out var armor)) value += armor.Value;
        }
        return value >= _settings.RichOutfitValue;
    }

    /// <summary>
    /// An armour is rich if it carries a rich material keyword. A leveled list is rich
    /// if at least half of its entries are, so a bandit chief whose list only reaches
    /// ebony at level 40 doesn't count, but a list of all high-tier gear does.
    /// </summary>
    private bool IsRichItem(FormKey key, int depth)
    {
        if (_richItems.TryGetValue(key, out var cached)) return cached;
        var result = false;

        if (_linkCache.TryResolve<IArmorGetter>(key, out var armor))
        {
            result = armor.Keywords?.Any(k => EditorIdOf(k) is { } kid && _richKeywords.Contains(kid)) == true;
        }
        else if (depth < MaxTemplateDepth && _linkCache.TryResolve<ILeveledItemGetter>(key, out var list) && list.Entries is { Count: > 0 } entries)
        {
            var refs = entries
                .Select(e => e.Data?.Reference.FormKey)
                .OfType<FormKey>()
                .ToList();
            var rich = refs.Count(r => IsRichItem(r, depth + 1));
            result = refs.Count > 0 && rich * 2 >= refs.Count;
        }

        _richItems[key] = result;
        return result;
    }

    private string? EditorIdOf<T>(IFormLinkGetter<T> link)
        where T : class, IMajorRecordGetter
    {
        if (_editorIds.TryGetValue(link.FormKey, out var id)) return id;
        id = _linkCache.TryResolveIdentifier<T>(link.FormKey, out var editorId) ? editorId : null;
        _editorIds[link.FormKey] = id;
        return id;
    }
}
