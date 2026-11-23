using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Xunit;

namespace NormalBodies.Tests;

public class BodyPatcherTests
{
    private readonly SkyrimMod _mod = new(ModKey.FromNameAndExtension("Test.esm"), SkyrimRelease.SkyrimSE);
    private readonly Settings _settings = new();
    private readonly Race _nord;
    private readonly Race _child;
    private readonly Class _warrior;
    private readonly Class _merchant;
    private readonly Faction _guardFaction;
    private readonly Faction _banditFaction;
    private readonly Outfit _steelPlate;
    private readonly Outfit _farmClothes;

    public BodyPatcherTests()
    {
        var npcType = _mod.Keywords.AddNew("ActorTypeNPC");
        _nord = _mod.Races.AddNew("NordRace");
        _nord.Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>> { npcType.ToLink() };
        _child = _mod.Races.AddNew("NordRaceChild");
        _child.Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>> { npcType.ToLink() };
        _child.Flags |= Race.Flag.Child;

        _warrior = _mod.Classes.AddNew("CombatWarrior1H");
        _warrior.SkillWeights[Skill.OneHanded] = 6;
        _warrior.SkillWeights[Skill.HeavyArmor] = 4;
        _warrior.SkillWeights[Skill.Smithing] = 1;

        _merchant = _mod.Classes.AddNew("Merchant");
        _merchant.SkillWeights[Skill.Speech] = 8;
        _merchant.SkillWeights[Skill.Pickpocket] = 2;
        _merchant.SkillWeights[Skill.OneHanded] = 1;

        _guardFaction = _mod.Factions.AddNew("IsGuardFaction");
        _banditFaction = _mod.Factions.AddNew("BanditFaction");

        var plateKw = _mod.Keywords.AddNew("ArmorMaterialSteelPlate");
        var plate = _mod.Armors.AddNew("ArmorSteelPlateCuirass");
        plate.Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>> { plateKw.ToLink() };
        plate.Value = 625;
        _steelPlate = OutfitOf("SteelPlateOutfit", plate);

        var rags = _mod.Armors.AddNew("FarmClothes01");
        rags.Value = 5;
        _farmClothes = OutfitOf("FarmClothesOutfit", rags);
    }

    private Outfit OutfitOf(string editorId, params IItemGetter[] items)
    {
        var outfit = _mod.Outfits.AddNew(editorId);
        outfit.Items = new ExtendedList<IFormLinkGetter<IOutfitTargetGetter>>();
        foreach (var item in items) outfit.Items.Add(new FormLink<IOutfitTargetGetter>(item.FormKey));
        return outfit;
    }

    private Npc Npc(string editorId, float weight, Class? cls = null, Race? race = null, Outfit? outfit = null, params Faction[] factions)
    {
        var npc = _mod.Npcs.AddNew(editorId);
        npc.Race.SetTo(race ?? _nord);
        npc.Class.SetTo(cls ?? _merchant);
        npc.Weight = weight;
        if (outfit is not null) npc.DefaultOutfit.SetTo(outfit);
        foreach (var f in factions) npc.Factions.Add(new RankPlacement { Faction = f.ToLink() });
        return npc;
    }

    private BodyClassifier Classifier() => new(_mod.ToImmutableLinkCache(), _settings);

    private Dictionary<string, WeightChange> Plan() =>
        new BodyPatcher(_mod.ToImmutableLinkCache(), _settings)
            .Plan(_mod.Npcs)
            .ToDictionary(c => c.Npc.EditorID!);

    [Fact]
    public void Merchant_IsSkinny()
    {
        var npc = Npc("Belethor", 80, _merchant, outfit: _farmClothes);
        Assert.Equal(BodyType.Skinny, Classifier().Classify(npc).Type);
    }

    [Fact]
    public void Guard_IsMarathon_EvenWithNonCombatClass()
    {
        var npc = Npc("WhiterunGuard", 100, _merchant, factions: _guardFaction);
        var c = Classifier().Classify(npc);
        Assert.True(c.IsGuard);
        Assert.Equal(BodyType.Marathon, c.Type);
    }

    [Fact]
    public void CombatClass_IsMarathon()
    {
        var npc = Npc("Mercenary", 100, _warrior, outfit: _farmClothes);
        Assert.Equal(BodyType.Marathon, Classifier().Classify(npc).Type);
    }

    [Fact]
    public void FighterFaction_IsMarathon()
    {
        var npc = Npc("BanditArcher", 100, _merchant, factions: _banditFaction);
        Assert.Equal(BodyType.Marathon, Classifier().Classify(npc).Type);
    }

    [Fact]
    public void FighterInHighTierArmour_IsRugby()
    {
        var npc = Npc("Housecarl", 100, _warrior, outfit: _steelPlate);
        var c = Classifier().Classify(npc);
        Assert.True(c.IsRich);
        Assert.Equal(BodyType.Rugby, c.Type);
    }

    [Fact]
    public void FighterInRichFaction_IsRugby()
    {
        var jarlGuard = _mod.Factions.AddNew("JobJarlFaction");
        var npc = Npc("JarlsChampion", 50, _warrior, outfit: _farmClothes, factions: jarlGuard);
        Assert.Equal(BodyType.Rugby, Classifier().Classify(npc).Type);
    }

    [Fact]
    public void RichNonFighter_StaysSkinny()
    {
        var npc = Npc("Noble", 60, _merchant, outfit: _steelPlate);
        Assert.Equal(BodyType.Skinny, Classifier().Classify(npc).Type);
    }

    [Fact]
    public void RichFactionNonFighter_StaysSkinny()
    {
        // e.g. a Thalmor ambassador: rich faction, but not a fighter.
        var thalmor = _mod.Factions.AddNew("ThalmorFaction");
        var npc = Npc("Ambassador", 60, _merchant, factions: thalmor);
        Assert.Equal(BodyType.Skinny, Classifier().Classify(npc).Type);
    }

    [Fact]
    public void RichFactionFighter_IsRugby()
    {
        var thalmor = _mod.Factions.AddNew("ThalmorFaction");
        var npc = Npc("Justiciar", 50, _warrior, outfit: _farmClothes, factions: thalmor);
        Assert.Equal(BodyType.Rugby, Classifier().Classify(npc).Type);
    }

    [Fact]
    public void ExpensiveOutfitByValue_IsRich()
    {
        var a = _mod.Armors.AddNew("FancyA"); a.Value = 700;
        var b = _mod.Armors.AddNew("FancyB"); b.Value = 600;
        var npc = Npc("ModdedKnight", 100, _warrior, outfit: OutfitOf("Fancy", a, b));
        Assert.Equal(BodyType.Rugby, Classifier().Classify(npc).Type);
    }

    [Fact]
    public void LeveledArmour_MostlyCheap_IsNotRich()
    {
        var plate = _mod.Armors.First(a => a.EditorID == "ArmorSteelPlateCuirass");
        var hide = _mod.Armors.AddNew("Hide");
        var iron = _mod.Armors.AddNew("Iron");
        var list = _mod.LeveledItems.AddNew("LItemBanditCuirass");
        list.Entries = new ExtendedList<LeveledItemEntry>();
        foreach (var item in new IItemGetter[] { hide, iron, plate })
            list.Entries.Add(new LeveledItemEntry { Data = new LeveledItemEntryData { Reference = new FormLink<IItemGetter>(item.FormKey), Level = 1, Count = 1 } });

        var npc = Npc("BanditBoss", 100, _warrior, outfit: OutfitOf("BanditOutfit", list));
        Assert.Equal(BodyType.Marathon, Classifier().Classify(npc).Type);
    }

    [Fact]
    public void Children_AreIgnored()
    {
        Npc("Lars", 100, race: _child);
        Assert.Empty(Plan());
    }

    [Fact]
    public void Weights_LandInRange()
    {
        Npc("Farmer", 100);
        Npc("Soldier", 100, _warrior);
        Npc("Knight", 100, _warrior, outfit: _steelPlate);
        Npc("Beggar", 0); // already skinny: left alone

        var plan = Plan();
        Assert.InRange(plan["Farmer"].NewWeight, _settings.Skinny.Min, _settings.Skinny.Max);
        Assert.InRange(plan["Soldier"].NewWeight, _settings.Marathon.Min, _settings.Marathon.Max);
        Assert.InRange(plan["Knight"].NewWeight, _settings.Rugby.Min, _settings.Rugby.Max);
        Assert.False(plan.ContainsKey("Beggar"));
    }

    [Fact]
    public void AlwaysReassign_TouchesInRangeNpcsToo()
    {
        _settings.Mode = WeightMode.AlwaysReassign;
        foreach (var i in Enumerable.Range(0, 20)) Npc($"Farmer{i}", 10);
        Assert.NotEmpty(Plan());
        Assert.All(Plan().Values, c => Assert.InRange(c.NewWeight, _settings.Skinny.Min, _settings.Skinny.Max));
    }

    [Fact]
    public void Weights_AreStableAcrossRuns()
    {
        Npc("Farmer", 100);
        Assert.Equal(Plan()["Farmer"].NewWeight, Plan()["Farmer"].NewWeight);
    }

    [Fact]
    public void TemplatedGuard_PushesTypeOntoTraitTemplate()
    {
        // Vanilla style: the actual guard inherits Traits (incl. weight) from a generic
        // template NPC via a leveled list, but has its own guard faction.
        var template = Npc("EncGuardNordM01", 100);
        var lvl = _mod.LeveledNpcs.AddNew("LCharGuardWhiterun");
        lvl.Entries = new ExtendedList<LeveledNpcEntry>
        {
            new() { Data = new LeveledNpcEntryData { Reference = new FormLink<INpcSpawnGetter>(template.FormKey), Level = 1, Count = 1 } },
        };
        var guard = Npc("WhiterunGuardPost", 100, factions: _guardFaction);
        guard.Template.SetTo(lvl.FormKey);
        guard.Configuration.TemplateFlags |= NpcConfiguration.TemplateFlag.Traits;

        var plan = Plan();
        Assert.False(plan.ContainsKey("WhiterunGuardPost")); // weight is inherited, so not edited
        Assert.Equal(BodyType.Marathon, plan["EncGuardNordM01"].Type);
        Assert.InRange(plan["EncGuardNordM01"].NewWeight, _settings.Marathon.Min, _settings.Marathon.Max);
    }

    [Fact]
    public void FactionsInheritedFromTemplate_AreUsed()
    {
        var template = Npc("BanditTemplate", 100, factions: _banditFaction);
        var bandit = Npc("BanditCampMember", 100);
        bandit.Template.SetTo(template.FormKey);
        bandit.Configuration.TemplateFlags |= NpcConfiguration.TemplateFlag.Factions;

        Assert.Equal(BodyType.Marathon, Classifier().Classify(bandit).Type);
    }

    [Fact]
    public void FemalesCanBeExcluded()
    {
        _settings.AffectFemales = false;
        var npc = Npc("Farmwife", 100);
        npc.Configuration.Flags |= NpcConfiguration.Flag.Female;
        Assert.Empty(Plan());
    }

    [Fact]
    public void ExcludedNpcs_AreSkipped()
    {
        var npc = Npc("Farkas", 100, _warrior);
        _settings.ExcludedNpcs.Add(new FormLink<INpcGetter>(npc.FormKey));
        Assert.Empty(Plan());
    }
}
