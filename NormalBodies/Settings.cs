using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Synthesis.Settings;

namespace NormalBodies;

/// <summary>An inclusive NPC weight range (0 = thinnest body mesh, 100 = heaviest).</summary>
public class WeightRange
{
    [SynthesisTooltip("Lowest weight an NPC in this group can have (0-100).")]
    public float Min { get; set; }

    [SynthesisTooltip("Highest weight an NPC in this group can have (0-100).")]
    public float Max { get; set; }

    public WeightRange() { }

    public WeightRange(float min, float max)
    {
        Min = min;
        Max = max;
    }

    public bool Contains(float weight) => weight >= Min && weight <= Max;
}

public enum WeightMode
{
    /// <summary>Leave NPCs already inside their group's range alone; move only the ones outside it.</summary>
    KeepIfInRange,

    /// <summary>Give every NPC a fresh (deterministic) weight inside their group's range.</summary>
    AlwaysReassign,
}

public class Settings
{
    [SynthesisSettingName("Everyone else (skinny)")]
    [SynthesisTooltip("Weight range for ordinary people: farmers, merchants, mages, beggars, nobles who don't fight, ...")]
    public WeightRange Skinny { get; set; } = new(0, 25);

    [SynthesisSettingName("Guards and fighters (marathon runner)")]
    [SynthesisTooltip("Weight range for guards, soldiers, bandits, mercenaries and other fighters: lean and wiry, not bulky.")]
    public WeightRange Marathon { get; set; } = new(20, 45);

    [SynthesisSettingName("Rich fighters (rugby player)")]
    [SynthesisTooltip("Weight range for well-equipped/well-fed fighters: housecarls, officers, Thalmor, anyone in high-tier armour. Solid, but not a bodybuilder.")]
    public WeightRange Rugby { get; set; } = new(60, 80);

    [SynthesisSettingName("Mode")]
    [SynthesisTooltip("KeepIfInRange only touches NPCs whose current weight is outside their group's range (fewer changed NPCs, fewer FaceGen regenerations). AlwaysReassign rerolls everyone.")]
    public WeightMode Mode { get; set; } = WeightMode.KeepIfInRange;

    [SynthesisSettingName("Affect female NPCs")]
    public bool AffectFemales { get; set; } = true;

    [SynthesisSettingName("Affect male NPCs")]
    public bool AffectMales { get; set; } = true;

    [SynthesisSettingName("Fighter class threshold")]
    [SynthesisTooltip("An NPC counts as a fighter when at least this fraction of their class's skill weights are combat skills (One-Handed, Two-Handed, Archery, Block, Heavy Armor, Light Armor).")]
    public float FighterSkillShare { get; set; } = 0.5f;

    [SynthesisSettingName("Rich outfit value")]
    [SynthesisTooltip("A fighter whose default outfit's armour pieces are together worth at least this many septims counts as rich.")]
    public uint RichOutfitValue { get; set; } = 1200;

    [SynthesisSettingName("Rich armour material keywords")]
    [SynthesisTooltip("Editor IDs of armour keywords that mark an outfit as expensive.")]
    public List<string> RichArmorKeywords { get; set; } = new()
    {
        "ArmorMaterialSteelPlate",
        "ArmorMaterialOrcish",
        "ArmorMaterialDwarven",
        "ArmorMaterialElvenGilded",
        "ArmorMaterialGlass",
        "ArmorMaterialEbony",
        "ArmorMaterialDaedric",
        "ArmorMaterialDragonplate",
        "ArmorMaterialDragonscale",
        "ArmorMaterialBlades",
        "ArmorMaterialThievesGuildLeader",
        "DLC2ArmorMaterialStalhrimHeavy",
        "DLC2ArmorMaterialStalhrimLight",
        "DLC2ArmorMaterialNordicHeavy",
        "DLC2ArmorMaterialNordicLight",
        "DLC2ArmorMaterialChitinHeavy",
        "ClothingRich",
    };

    [SynthesisSettingName("Guard pattern")]
    [SynthesisTooltip("Regex matched against the Editor IDs of an NPC, its class and its factions. A match makes the NPC a guard.")]
    public string GuardPattern { get; set; } = @"Guard(?!ian)";

    [SynthesisSettingName("Fighter faction pattern")]
    [SynthesisTooltip("Regex matched against faction Editor IDs. A match makes the NPC a fighter even if their class isn't combat-heavy.")]
    public string FighterFactionPattern { get; set; } =
        @"Bandit|Soldier|Forsworn|Mercenary|Companions|Dawnguard|Vigilant|Silverhand|Warrior";

    [SynthesisSettingName("Rich faction pattern")]
    [SynthesisTooltip("Regex matched against faction Editor IDs. A fighter in a matching faction counts as rich regardless of outfit.")]
    public string RichFactionPattern { get; set; } =
        @"Jarl|Housecarl|Thane|Noble|Thalmor|Penitus|Legate|Officer|General|Blades";

    [SynthesisSettingName("Excluded NPCs")]
    [SynthesisTooltip("NPCs that should never be changed.")]
    public List<FormLink<Mutagen.Bethesda.Skyrim.INpcGetter>> ExcludedNpcs { get; set; } = new();
}
