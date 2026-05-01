using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldBushcraft;

public static class BushcraftLootUtility
{
    private const float BaseSuccessChance = 0.45f;
    private const float SkillSuccessFactor = 0.025f;

    private const float GlobalLootWeightMultiplier = 1f;
    private const float ScavengingReferenceReduction = 0.95f;
    private const float SkillWeightFactorPerLevel = 0.08f;
    private const float DiscoveredHarvestWeightScale = 70f;
    private const float BaseBadFailureChance = 0.035f;
    private const float BaseManhunterFailureChance = 0.003f;

    private static readonly BaseLootEntry[] BaseLootTable =
    {
        new BaseLootEntry("RawFungus", BushcraftLootCategory.Fungus, 10f * ScavengingReferenceReduction, 1, 10),
        new BaseLootEntry("RawBerries", BushcraftLootCategory.Plant, 10f * ScavengingReferenceReduction, 1, 10),
        new BaseLootEntry("EggDuckUnfertilized", BushcraftLootCategory.Egg, 10f * ScavengingReferenceReduction, 1, 5),
        new BaseLootEntry(null, BushcraftLootCategory.Insect, 25f * ScavengingReferenceReduction, 1, 15, BuiltInLoot.InsectMeat),
        new BaseLootEntry("MedicineHerbal", BushcraftLootCategory.Medicine, 1.5f, 1, 1),
        new BaseLootEntry("InsectJelly", BushcraftLootCategory.Insect, 1.1f, 1, 3)
    };

    public static List<BushcraftDiscoveredLootCandidate> BaseLootCandidates()
    {
        List<BushcraftDiscoveredLootCandidate> candidates = new List<BushcraftDiscoveredLootCandidate>();
        for (int i = 0; i < BaseLootTable.Length; i++)
        {
            BaseLootEntry entry = BaseLootTable[i];
            ThingDef thingDef = entry.ResolveThingDef();
            if (thingDef != null)
            {
                candidates.Add(new BushcraftDiscoveredLootCandidate(thingDef, entry.Category, entry.BaseWeight, entry.MinCount, entry.MaxCount));
            }
        }

        return candidates;
    }

    public static Thing TryGenerateForageResult(Pawn pawn, Zone_Bushcraft zone)
    {
        int plants = pawn.skills?.GetSkill(SkillDefOf.Plants).Level ?? 0;
        int animals = pawn.skills?.GetSkill(SkillDefOf.Animals).Level ?? 0;
        int bestSkill = plants > animals ? plants : animals;

        float density = zone?.ForageDensity ?? 1f;
        BushcraftEnvironmentFactors environment = BushcraftEnvironmentFactors.For(pawn.Map);
        float successChance = (BaseSuccessChance + bestSkill * SkillSuccessFactor) * density * environment.GeneralSuccessFactor;
        if (!Rand.Chance(successChance))
        {
            TryApplyForageFailure(pawn, zone, bestSkill, density, environment);
            return null;
        }

        TerrainLootProfile terrainProfile = TerrainLootProfile.For(zone);
        List<LootOption> options = new List<LootOption>();
        for (int i = 0; i < BaseLootTable.Length; i++)
        {
            AddOption(options, BaseLootTable[i], plants, animals, density, environment, terrainProfile);
        }

        foreach (BushcraftDiscoveredLootCandidate candidate in DiscoverWildPlantHarvests(pawn.Map))
        {
            AddOption(options, candidate, plants, animals, density, environment, terrainProfile);
        }

        if (options.Count == 0)
        {
            return null;
        }

        LootOption selected = SelectWeighted(options);
        Thing thing = ThingMaker.MakeThing(selected.ThingDef);
        thing.stackCount = selected.CountRange.RandomInRange;
        return thing;
    }

    private static void TryApplyForageFailure(
        Pawn pawn,
        Zone_Bushcraft zone,
        int bestSkill,
        float density,
        BushcraftEnvironmentFactors environment)
    {
        if (pawn == null || pawn.Map == null)
        {
            return;
        }

        float skillRiskFactor = Mathf.Lerp(1.6f, 0.65f, Mathf.Clamp01(bestSkill / 20f));
        float densityRiskFactor = Mathf.Lerp(1.45f, 0.75f, Mathf.Clamp01(density));
        float badFailureChance = BaseBadFailureChance * Settings.badFailureChanceMultiplier * skillRiskFactor * densityRiskFactor * environment.FailureRiskFactor;
        if (!Rand.Chance(badFailureChance))
        {
            return;
        }

        float manhunterChance = BaseManhunterFailureChance * Settings.manhunterChanceMultiplier * skillRiskFactor * environment.ManhunterRiskFactor;
        if (Rand.Chance(manhunterChance) && TrySpawnManhunter(pawn, zone))
        {
            return;
        }

        float roll = Rand.Value;
        if (roll < 0.35f)
        {
            ApplyMinorDamage(pawn, "Scratch", Rand.Range(2f, 5f));
        }
        else if (roll < 0.58f)
        {
            ApplyMinorDamage(pawn, "Bite", Rand.Range(2f, 6f));
        }
        else if (roll < 0.76f)
        {
            ApplyMinorDamage(pawn, "ToxicBite", Rand.Range(1f, 3f));
        }
        else if (roll < 0.88f)
        {
            ApplyFoodPoisoning(pawn, Mathf.Lerp(0.12f, 0.35f, Rand.Value));
        }
        else
        {
            ApplyBadForageThought(pawn);
        }
    }

    private static void ApplyMinorDamage(Pawn pawn, string damageDefName, float amount)
    {
        DamageDef damageDef = DefDatabase<DamageDef>.GetNamed(damageDefName, errorOnFail: false);
        if (damageDef == null)
        {
            return;
        }

        DamageInfo damageInfo = new DamageInfo(damageDef, amount, 0f, -1f, null);
        pawn.TakeDamage(damageInfo);
        ApplyBadForageThought(pawn);
    }

    private static void ApplyFoodPoisoning(Pawn pawn, float severity)
    {
        HealthUtility.AdjustSeverity(pawn, HediffDefOf.FoodPoisoning, severity);
        ApplyBadForageThought(pawn);
    }

    private static void ApplyBadForageThought(Pawn pawn)
    {
        ThoughtDef thoughtDef = DefDatabase<ThoughtDef>.GetNamed("Bushcraft_BadForage", errorOnFail: false);
        if (thoughtDef == null)
        {
            return;
        }

        pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thoughtDef);
    }

    private static bool TrySpawnManhunter(Pawn pawn, Zone_Bushcraft zone)
    {
        Map map = pawn.Map;
        if (map?.Biome == null)
        {
            return false;
        }

        PawnKindDef pawnKindDef = RandomSmallWildAnimal(map.Biome);
        if (pawnKindDef == null)
        {
            return false;
        }

        IntVec3 spawnCell;
        IntVec3 center = zone?.ForageableCells.Count > 0 ? zone.ForageableCells.RandomElement() : pawn.Position;
        bool foundCell = CellFinder.TryFindRandomCellNear(
            center,
            map,
            12,
            cell => cell.Standable(map) && !cell.Fogged(map) && pawn.CanReach(cell, Verse.AI.PathEndMode.OnCell, Danger.Deadly),
            out spawnCell,
            -1);
        if (!foundCell)
        {
            spawnCell = pawn.Position;
        }

        Pawn animal = PawnGenerator.GeneratePawn(pawnKindDef);
        GenSpawn.Spawn(animal, spawnCell, map);
        animal.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter, "Bushcraft_ManhunterReason".Translate(), forceWake: true);
        Messages.Message("Bushcraft_ManhunterMessage".Translate(animal.LabelShortCap), animal, MessageTypeDefOf.ThreatSmall);
        ApplyBadForageThought(pawn);
        return true;
    }

    private static PawnKindDef RandomSmallWildAnimal(BiomeDef biome)
    {
        List<PawnKindDef> options = new List<PawnKindDef>();
        List<float> weights = new List<float>();

        foreach (PawnKindDef pawnKindDef in biome.AllWildAnimals)
        {
            if (pawnKindDef?.RaceProps?.Animal != true || pawnKindDef.RaceProps.baseBodySize > 1.2f)
            {
                continue;
            }

            float commonality = biome.CommonalityOfAnimal(pawnKindDef);
            if (commonality <= 0f)
            {
                continue;
            }

            options.Add(pawnKindDef);
            weights.Add(commonality);
        }

        if (options.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            totalWeight += weights[i];
        }

        float roll = Rand.Range(0f, totalWeight);
        for (int i = 0; i < options.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f)
            {
                return options[i];
            }
        }

        return options[options.Count - 1];
    }

    private static void AddOption(
        List<LootOption> options,
        BaseLootEntry entry,
        int plants,
        int animals,
        float density,
        BushcraftEnvironmentFactors environment,
        TerrainLootProfile terrainProfile)
    {
        ThingDef thingDef = entry.ResolveThingDef();
        if (thingDef == null)
        {
            return;
        }

        BushcraftDiscoveredLootSetting setting = Settings.SettingFor(thingDef);
        if (setting == null || !setting.enabled)
        {
            return;
        }

        float categoryFactor = environment.CategoryFactor(entry.Category) * terrainProfile.CategoryFactor(entry.Category);
        float skillFactor = 1f + RelevantSkill(entry.Category, plants, animals) * SkillWeightFactorPerLevel;
        float weight = entry.BaseWeight * GlobalLootWeightMultiplier * setting.weightMultiplier * density * categoryFactor * skillFactor;
        if (weight <= 0f)
        {
            return;
        }

        int minCount = Mathf.Max(1, Mathf.RoundToInt(entry.MinCount * setting.countMultiplier));
        int maxCount = Mathf.Max(minCount, Mathf.RoundToInt(entry.MaxCount * setting.countMultiplier));
        options.Add(new LootOption(thingDef, weight, new IntRange(minCount, maxCount)));
    }

    private static void AddOption(
        List<LootOption> options,
        BushcraftDiscoveredLootCandidate candidate,
        int plants,
        int animals,
        float density,
        BushcraftEnvironmentFactors environment,
        TerrainLootProfile terrainProfile)
    {
        BushcraftDiscoveredLootSetting setting = Settings.SettingFor(candidate.ThingDef);
        if (setting == null || !setting.enabled)
        {
            return;
        }

        float categoryFactor = environment.CategoryFactor(candidate.Category) * terrainProfile.CategoryFactor(candidate.Category);
        float skillFactor = 1f + RelevantSkill(candidate.Category, plants, animals) * SkillWeightFactorPerLevel;
        float weight = candidate.BaseWeight * Settings.discoveredLootWeightMultiplier * setting.weightMultiplier * density * categoryFactor * skillFactor;
        if (weight <= 0f)
        {
            return;
        }

        int minCount = Mathf.Max(1, Mathf.RoundToInt(candidate.MinCount * Settings.discoveredLootCountMultiplier * setting.countMultiplier));
        int maxCount = Mathf.Max(minCount, Mathf.RoundToInt(candidate.MaxCount * Settings.discoveredLootCountMultiplier * setting.countMultiplier));
        options.Add(new LootOption(candidate.ThingDef, weight, new IntRange(minCount, maxCount)));
    }

    public static List<BushcraftDiscoveredLootCandidate> DiscoverWildPlantHarvests(Map map)
    {
        List<BushcraftDiscoveredLootCandidate> candidates = new List<BushcraftDiscoveredLootCandidate>();
        HashSet<ThingDef> seenHarvestedDefs = new HashSet<ThingDef>();
        HashSet<ThingDef> baseLootDefs = BaseLootThingDefs();

        foreach (ThingDef plantDef in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (plantDef?.category != ThingCategory.Plant || plantDef.plant?.harvestedThingDef == null)
            {
                continue;
            }

            ThingDef harvestedThingDef = plantDef.plant.harvestedThingDef;
            if (baseLootDefs.Contains(harvestedThingDef) || seenHarvestedDefs.Contains(harvestedThingDef) || !CanSpawnForageItem(harvestedThingDef))
            {
                continue;
            }

            float commonality = map?.Biome?.CommonalityOfPlant(plantDef) ?? 0.01f;
            if (map != null && commonality <= 0f)
            {
                continue;
            }

            seenHarvestedDefs.Add(harvestedThingDef);
            BushcraftLootCategory category = CategoryForHarvestedThing(harvestedThingDef, plantDef);
            if (!ShouldAutoAddDiscoveredHarvest(harvestedThingDef, category))
            {
                continue;
            }

            float baseWeight = BaseWeightForDiscoveredHarvest(harvestedThingDef, commonality);
            int maxCount = MaxCountForDiscoveredHarvest(plantDef);
            candidates.Add(new BushcraftDiscoveredLootCandidate(harvestedThingDef, category, baseWeight, 1, maxCount));
        }

        return candidates;
    }

    private static bool CanSpawnForageItem(ThingDef thingDef)
    {
        return thingDef?.category == ThingCategory.Item
            && !thingDef.IsCorpse
            && !thingDef.destroyOnDrop
            && !thingDef.MadeFromStuff;
    }

    private static bool ShouldAutoAddDiscoveredHarvest(ThingDef thingDef, BushcraftLootCategory category)
    {
        if (HasThingCategory(thingDef, "Manufactured"))
        {
            return false;
        }

        if (category == BushcraftLootCategory.Special && IsVanillaThingDef(thingDef))
        {
            return false;
        }

        return true;
    }

    private static HashSet<ThingDef> BaseLootThingDefs()
    {
        HashSet<ThingDef> thingDefs = new HashSet<ThingDef>();
        for (int i = 0; i < BaseLootTable.Length; i++)
        {
            ThingDef thingDef = BaseLootTable[i].ResolveThingDef();
            if (thingDef != null)
            {
                thingDefs.Add(thingDef);
            }
        }

        return thingDefs;
    }

    private static BushcraftLootCategory CategoryForHarvestedThing(ThingDef harvestedThingDef, ThingDef plantDef)
    {
        if (plantDef?.plant?.purpose == PlantPurpose.Beauty)
        {
            return BushcraftLootCategory.Plant;
        }

        if (HasThingCategory(harvestedThingDef, "PlantFoodRaw") || harvestedThingDef.ingestible?.HumanEdible == true)
        {
            return BushcraftLootCategory.Plant;
        }

        if (HasThingCategory(harvestedThingDef, "Medicine") || harvestedThingDef.statBases?.GetStatValueFromList(StatDefOf.MedicalPotency, 0f) > 0f)
        {
            return BushcraftLootCategory.Medicine;
        }

        string defName = harvestedThingDef.defName.ToLowerInvariant();
        if (defName.Contains("fungus") || defName.Contains("mushroom"))
        {
            return BushcraftLootCategory.Fungus;
        }

        return BushcraftLootCategory.Special;
    }

    private static bool HasThingCategory(ThingDef thingDef, string categoryDefName)
    {
        if (thingDef?.thingCategories == null)
        {
            return false;
        }

        for (int i = 0; i < thingDef.thingCategories.Count; i++)
        {
            ThingCategoryDef categoryDef = thingDef.thingCategories[i];
            while (categoryDef != null)
            {
                if (categoryDef.defName == categoryDefName)
                {
                    return true;
                }

                categoryDef = categoryDef.parent;
            }
        }

        return false;
    }

    public static bool IsVanillaThingDef(ThingDef thingDef)
    {
        string packageId = thingDef?.modContentPack?.PackageId;
        return packageId != null && packageId.StartsWith("ludeon.rimworld");
    }

    private static float BaseWeightForDiscoveredHarvest(ThingDef harvestedThingDef, float commonality)
    {
        float baseWeight = Mathf.Clamp(commonality * DiscoveredHarvestWeightScale, 0.15f, 6f);
        if (harvestedThingDef.stackLimit <= 1)
        {
            baseWeight *= 0.25f;
        }

        float marketValue = harvestedThingDef.BaseMarketValue;
        if (marketValue >= 50f)
        {
            baseWeight *= 0.25f;
        }
        else if (marketValue >= 20f)
        {
            baseWeight *= 0.5f;
        }

        if (!harvestedThingDef.IsNutritionGivingIngestible && !HasThingCategory(harvestedThingDef, "PlantFoodRaw"))
        {
            baseWeight *= 0.45f;
        }

        return Mathf.Max(0.02f, baseWeight);
    }

    private static int MaxCountForDiscoveredHarvest(ThingDef plantDef)
    {
        float harvestYield = plantDef?.plant?.harvestYield ?? 1f;
        return Mathf.Clamp(Mathf.RoundToInt(harvestYield), 1, 6);
    }

    private static int RelevantSkill(BushcraftLootCategory category, int plants, int animals)
    {
        switch (category)
        {
            case BushcraftLootCategory.Plant:
            case BushcraftLootCategory.Fungus:
            case BushcraftLootCategory.Medicine:
                return plants;
            case BushcraftLootCategory.Insect:
            case BushcraftLootCategory.Egg:
                return animals;
            case BushcraftLootCategory.Special:
                return plants > animals ? plants : animals;
            default:
                return plants > animals ? plants : animals;
        }
    }

    private static BushcraftSettings Settings => BushcraftMod.Settings ?? LoadedModManager.GetMod<BushcraftMod>().GetSettings<BushcraftSettings>();

    private static ThingDef MegaspiderMeatDef()
    {
        ThingDef megaspider = DefDatabase<ThingDef>.GetNamed("Megaspider", errorOnFail: false);
        return megaspider?.race?.meatDef;
    }

    private static LootOption SelectWeighted(List<LootOption> options)
    {
        float totalWeight = 0f;
        for (int i = 0; i < options.Count; i++)
        {
            totalWeight += options[i].Weight;
        }

        float roll = Rand.Range(0f, totalWeight);
        for (int i = 0; i < options.Count; i++)
        {
            roll -= options[i].Weight;
            if (roll <= 0f)
            {
                return options[i];
            }
        }

        return options[options.Count - 1];
    }

    public enum BushcraftLootCategory
    {
        Plant,
        Fungus,
        Medicine,
        Insect,
        Egg,
        Special
    }

    private enum BuiltInLoot
    {
        None,
        InsectMeat
    }

    private readonly struct BaseLootEntry
    {
        private readonly string defName;
        private readonly BuiltInLoot builtInLoot;

        public readonly BushcraftLootCategory Category;
        public readonly float BaseWeight;
        public readonly int MinCount;
        public readonly int MaxCount;

        public BaseLootEntry(
            string defName,
            BushcraftLootCategory category,
            float baseWeight,
            int minCount,
            int maxCount,
            BuiltInLoot builtInLoot = BuiltInLoot.None)
        {
            this.defName = defName;
            this.builtInLoot = builtInLoot;
            Category = category;
            BaseWeight = baseWeight;
            MinCount = minCount;
            MaxCount = maxCount;
        }

        public ThingDef ResolveThingDef()
        {
            switch (builtInLoot)
            {
                case BuiltInLoot.InsectMeat:
                    return MegaspiderMeatDef();
                default:
                    return DefDatabase<ThingDef>.GetNamed(defName, errorOnFail: false);
            }
        }
    }

    private readonly struct LootOption
    {
        public readonly ThingDef ThingDef;
        public readonly float Weight;
        public readonly IntRange CountRange;

        public LootOption(ThingDef thingDef, float weight, IntRange countRange)
        {
            ThingDef = thingDef;
            Weight = weight;
            CountRange = countRange;
        }
    }

    public readonly struct BushcraftDiscoveredLootCandidate
    {
        public readonly ThingDef ThingDef;
        public readonly BushcraftLootCategory Category;
        public readonly float BaseWeight;
        public readonly int MinCount;
        public readonly int MaxCount;

        public BushcraftDiscoveredLootCandidate(ThingDef thingDef, BushcraftLootCategory category, float baseWeight, int minCount, int maxCount)
        {
            ThingDef = thingDef;
            Category = category;
            BaseWeight = baseWeight;
            MinCount = minCount;
            MaxCount = maxCount;
        }
    }

    private readonly struct TerrainLootProfile
    {
        private readonly float plantFactor;
        private readonly float fungusFactor;
        private readonly float medicineFactor;
        private readonly float insectFactor;
        private readonly float eggFactor;

        private TerrainLootProfile(float plantFactor, float fungusFactor, float medicineFactor, float insectFactor, float eggFactor)
        {
            this.plantFactor = plantFactor;
            this.fungusFactor = fungusFactor;
            this.medicineFactor = medicineFactor;
            this.insectFactor = insectFactor;
            this.eggFactor = eggFactor;
        }

        public float CategoryFactor(BushcraftLootCategory category)
        {
            switch (category)
            {
                case BushcraftLootCategory.Plant:
                    return plantFactor;
                case BushcraftLootCategory.Fungus:
                    return fungusFactor;
                case BushcraftLootCategory.Medicine:
                    return medicineFactor;
                case BushcraftLootCategory.Insect:
                    return insectFactor;
                case BushcraftLootCategory.Egg:
                    return eggFactor;
                default:
                    return 1f;
            }
        }

        public static TerrainLootProfile For(Zone_Bushcraft zone)
        {
            if (zone == null)
            {
                return Default;
            }

            List<IntVec3> forageableCells = zone.ForageableCells;
            if (forageableCells.Count == 0)
            {
                return Default;
            }

            float plant = 0f;
            float fungus = 0f;
            float medicine = 0f;
            float insect = 0f;
            float egg = 0f;
            int countedCells = 0;

            for (int i = 0; i < forageableCells.Count; i++)
            {
                TerrainDef terrain = forageableCells[i].GetTerrain(zone.Map);
                if (terrain == null)
                {
                    continue;
                }

                TerrainLootProfile cellProfile = ForTerrain(terrain);
                plant += cellProfile.plantFactor;
                fungus += cellProfile.fungusFactor;
                medicine += cellProfile.medicineFactor;
                insect += cellProfile.insectFactor;
                egg += cellProfile.eggFactor;
                countedCells++;
            }

            if (countedCells == 0)
            {
                return Default;
            }

            return new TerrainLootProfile(
                plant / countedCells,
                fungus / countedCells,
                medicine / countedCells,
                insect / countedCells,
                egg / countedCells);
        }

        private static TerrainLootProfile ForTerrain(TerrainDef terrain)
        {
            switch (terrain.defName)
            {
                case "Soil":
                    return new TerrainLootProfile(1.05f, 1f, 1f, 1f, 1f);
                case "SoilRich":
                    return new TerrainLootProfile(1.25f, 1.05f, 1.15f, 1f, 1f);
                case "MossyTerrain":
                    return new TerrainLootProfile(1f, 1.35f, 1f, 1.1f, 0.95f);
                case "MarshyTerrain":
                    return new TerrainLootProfile(0.95f, 1.25f, 0.9f, 1.2f, 1.1f);
                case "Mud":
                    return new TerrainLootProfile(0.25f, 0.85f, 0.35f, 1.45f, 0.75f);
                case "Gravel":
                    return new TerrainLootProfile(0.45f, 0.6f, 0.45f, 1.25f, 0.75f);
                case "Sand":
                case "SoftSand":
                    return new TerrainLootProfile(0.12f, 0.12f, 0.18f, 1.35f, 1.15f);
                case "PackedDirt":
                case "Bridge":
                    return new TerrainLootProfile(0.35f, 0.35f, 0.25f, 0.55f, 0.3f);
                default:
                    return Default;
            }
        }

        private static TerrainLootProfile Default => new TerrainLootProfile(1f, 1f, 1f, 1f, 1f);
    }

    private readonly struct BushcraftEnvironmentFactors
    {
        public readonly float GeneralSuccessFactor;
        public readonly float FailureRiskFactor;
        public readonly float ManhunterRiskFactor;
        private readonly float plantFactor;
        private readonly float fungusFactor;
        private readonly float animalFactor;

        private BushcraftEnvironmentFactors(
            float generalSuccessFactor,
            float plantFactor,
            float fungusFactor,
            float animalFactor,
            float failureRiskFactor,
            float manhunterRiskFactor)
        {
            GeneralSuccessFactor = generalSuccessFactor;
            this.plantFactor = plantFactor;
            this.fungusFactor = fungusFactor;
            this.animalFactor = animalFactor;
            FailureRiskFactor = failureRiskFactor;
            ManhunterRiskFactor = manhunterRiskFactor;
        }

        public float CategoryFactor(BushcraftLootCategory category)
        {
            switch (category)
            {
                case BushcraftLootCategory.Plant:
                case BushcraftLootCategory.Medicine:
                    return plantFactor;
                case BushcraftLootCategory.Fungus:
                    return fungusFactor;
                case BushcraftLootCategory.Insect:
                case BushcraftLootCategory.Egg:
                    return animalFactor;
                default:
                    return 1f;
            }
        }

        public static BushcraftEnvironmentFactors For(Map map)
        {
            if (map == null)
            {
                return new BushcraftEnvironmentFactors(1f, 1f, 1f, 1f, 1f, 1f);
            }

            float temperature = map.mapTemperature.OutdoorTemp;
            Season season = GenLocalDate.Season(map);
            int hour = GenLocalDate.HourOfDay(map);
            BiomeProfile biome = BiomeProfile.For(map.Biome);
            WeatherProfile weather = WeatherProfile.For(map.weatherManager?.curWeather);
            TimeProfile time = TimeProfile.For(hour, map.Biome, temperature);

            float general = TemperatureSuccessFactor(temperature) * biome.GeneralSuccessFactor * weather.GeneralSuccessFactor * time.GeneralSuccessFactor;
            float plant = TemperaturePlantFactor(temperature) * SeasonPlantFactor(season) * biome.PlantFactor * weather.PlantFactor * time.PlantFactor;
            float fungus = TemperatureFungusFactor(temperature) * SeasonFungusFactor(season) * biome.FungusFactor * weather.FungusFactor * time.FungusFactor;
            float animal = TemperatureAnimalFactor(temperature) * SeasonAnimalFactor(season) * biome.AnimalFactor * weather.AnimalFactor * time.AnimalFactor;
            float failureRisk = weather.FailureRiskFactor * time.FailureRiskFactor;
            float manhunterRisk = weather.ManhunterRiskFactor * time.ManhunterRiskFactor * biome.ManhunterRiskFactor;

            return new BushcraftEnvironmentFactors(general, plant, fungus, animal, failureRisk, manhunterRisk);
        }

        private static float TemperatureSuccessFactor(float temperature)
        {
            if (temperature <= -10f)
            {
                return 0.25f;
            }

            if (temperature <= 0f)
            {
                return 0.45f;
            }

            if (temperature <= 6f)
            {
                return 0.7f;
            }

            if (temperature >= 45f)
            {
                return 0.55f;
            }

            if (temperature >= 35f)
            {
                return 0.8f;
            }

            return 1f;
        }

        private static float TemperaturePlantFactor(float temperature)
        {
            if (temperature <= -10f)
            {
                return 0.05f;
            }

            if (temperature <= 0f)
            {
                return 0.2f;
            }

            if (temperature <= 6f)
            {
                return 0.55f;
            }

            if (temperature >= 45f)
            {
                return 0.45f;
            }

            if (temperature >= 35f)
            {
                return 0.75f;
            }

            return 1f;
        }

        private static float TemperatureFungusFactor(float temperature)
        {
            if (temperature <= -10f)
            {
                return 0.15f;
            }

            if (temperature <= 0f)
            {
                return 0.35f;
            }

            if (temperature <= 6f)
            {
                return 0.75f;
            }

            if (temperature >= 45f)
            {
                return 0.55f;
            }

            if (temperature >= 35f)
            {
                return 0.85f;
            }

            return 1.05f;
        }

        private static float TemperatureAnimalFactor(float temperature)
        {
            if (temperature <= -10f)
            {
                return 0.2f;
            }

            if (temperature <= 0f)
            {
                return 0.4f;
            }

            if (temperature <= 6f)
            {
                return 0.7f;
            }

            if (temperature >= 45f)
            {
                return 0.6f;
            }

            if (temperature >= 35f)
            {
                return 0.85f;
            }

            return 1f;
        }

        private static float SeasonPlantFactor(Season season)
        {
            switch (season)
            {
                case Season.Spring:
                    return 1.1f;
                case Season.Summer:
                case Season.PermanentSummer:
                    return 1.15f;
                case Season.Fall:
                    return 0.9f;
                case Season.Winter:
                case Season.PermanentWinter:
                    return 0.25f;
                default:
                    return 1f;
            }
        }

        private static float SeasonFungusFactor(Season season)
        {
            switch (season)
            {
                case Season.Spring:
                    return 1.05f;
                case Season.Summer:
                case Season.PermanentSummer:
                    return 1f;
                case Season.Fall:
                    return 1.2f;
                case Season.Winter:
                case Season.PermanentWinter:
                    return 0.6f;
                default:
                    return 1f;
            }
        }

        private static float SeasonAnimalFactor(Season season)
        {
            switch (season)
            {
                case Season.Spring:
                    return 1.05f;
                case Season.Summer:
                case Season.PermanentSummer:
                    return 1.15f;
                case Season.Fall:
                    return 0.85f;
                case Season.Winter:
                case Season.PermanentWinter:
                    return 0.45f;
                default:
                    return 1f;
            }
        }

        private readonly struct BiomeProfile
        {
            public readonly float GeneralSuccessFactor;
            public readonly float PlantFactor;
            public readonly float FungusFactor;
            public readonly float AnimalFactor;
            public readonly float ManhunterRiskFactor;

            private BiomeProfile(float generalSuccessFactor, float plantFactor, float fungusFactor, float animalFactor, float manhunterRiskFactor = 1f)
            {
                GeneralSuccessFactor = generalSuccessFactor;
                PlantFactor = plantFactor;
                FungusFactor = fungusFactor;
                AnimalFactor = animalFactor;
                ManhunterRiskFactor = manhunterRiskFactor;
            }

            public static BiomeProfile For(BiomeDef biome)
            {
                switch (biome?.defName)
                {
                    case "TemperateForest":
                        return new BiomeProfile(1.05f, 1.2f, 1.05f, 0.95f);
                    case "TemperateSwamp":
                        return new BiomeProfile(1.1f, 1.1f, 1.35f, 1.15f, 1.15f);
                    case "BorealForest":
                        return new BiomeProfile(0.95f, 0.95f, 1.15f, 0.9f);
                    case "ColdBog":
                        return new BiomeProfile(0.9f, 0.8f, 1.35f, 0.85f);
                    case "TropicalRainforest":
                        return new BiomeProfile(1.2f, 1.25f, 1.2f, 1.25f, 1.2f);
                    case "TropicalSwamp":
                        return new BiomeProfile(1.2f, 1.15f, 1.45f, 1.3f, 1.25f);
                    case "AridShrubland":
                        return new BiomeProfile(0.75f, 0.65f, 0.45f, 1.05f);
                    case "Desert":
                        return new BiomeProfile(0.45f, 0.25f, 0.2f, 0.8f, 0.85f);
                    case "ExtremeDesert":
                    case "LavaField":
                    case "Scarlands":
                        return new BiomeProfile(0.25f, 0.1f, 0.1f, 0.45f);
                    case "Tundra":
                        return new BiomeProfile(0.45f, 0.35f, 0.55f, 0.45f);
                    case "IceSheet":
                    case "GlacialPlain":
                        return new BiomeProfile(0.18f, 0.05f, 0.2f, 0.2f);
                    case "SeaIce":
                    case "Ocean":
                    case "Lake":
                    case "Space":
                    case "Orbit":
                        return new BiomeProfile(0.02f, 0f, 0f, 0f);
                    case "Underground":
                    case "Undercave":
                        return new BiomeProfile(0.75f, 0.15f, 2f, 1.35f);
                    case "Glowforest":
                        return new BiomeProfile(1.15f, 1.05f, 1.6f, 1.15f);
                    case "Grasslands":
                        return new BiomeProfile(1f, 1.05f, 0.75f, 1f);
                    default:
                        return new BiomeProfile(1f, 1f, 1f, 1f);
                }
            }
        }

        private readonly struct WeatherProfile
        {
            public readonly float GeneralSuccessFactor;
            public readonly float PlantFactor;
            public readonly float FungusFactor;
            public readonly float AnimalFactor;
            public readonly float FailureRiskFactor;
            public readonly float ManhunterRiskFactor;

            private WeatherProfile(
                float generalSuccessFactor,
                float plantFactor,
                float fungusFactor,
                float animalFactor,
                float failureRiskFactor,
                float manhunterRiskFactor)
            {
                GeneralSuccessFactor = generalSuccessFactor;
                PlantFactor = plantFactor;
                FungusFactor = fungusFactor;
                AnimalFactor = animalFactor;
                FailureRiskFactor = failureRiskFactor;
                ManhunterRiskFactor = manhunterRiskFactor;
            }

            public static WeatherProfile For(WeatherDef weather)
            {
                if (weather == null)
                {
                    return Default;
                }

                float general = 1f;
                float plant = 1f;
                float fungus = 1f;
                float animal = 1f;
                float failure = weather.isBad ? 1.25f : 1f;
                float manhunter = 1f;

                if (weather.rainRate > 0f)
                {
                    general *= 0.95f;
                    plant *= 0.95f;
                    fungus *= 1.25f;
                    animal *= 1.2f;
                    failure *= 1.1f;
                    manhunter *= 1.05f;
                }

                if (weather.snowRate > 0f)
                {
                    general *= 0.75f;
                    plant *= 0.45f;
                    fungus *= 0.75f;
                    animal *= 0.7f;
                    failure *= 1.25f;
                    manhunter *= 0.9f;
                }

                if (weather.sandRate > 0f)
                {
                    general *= 0.65f;
                    plant *= 0.45f;
                    fungus *= 0.5f;
                    animal *= 0.85f;
                    failure *= 1.55f;
                    manhunter *= 1.1f;
                }

                if (weather.defName.Contains("Fog"))
                {
                    general *= 0.9f;
                    fungus *= 1.15f;
                    animal *= 0.85f;
                    failure *= 1.15f;
                }

                return new WeatherProfile(general, plant, fungus, animal, failure, manhunter);
            }

            private static WeatherProfile Default => new WeatherProfile(1f, 1f, 1f, 1f, 1f, 1f);
        }

        private readonly struct TimeProfile
        {
            public readonly float GeneralSuccessFactor;
            public readonly float PlantFactor;
            public readonly float FungusFactor;
            public readonly float AnimalFactor;
            public readonly float FailureRiskFactor;
            public readonly float ManhunterRiskFactor;

            private TimeProfile(
                float generalSuccessFactor,
                float plantFactor,
                float fungusFactor,
                float animalFactor,
                float failureRiskFactor,
                float manhunterRiskFactor)
            {
                GeneralSuccessFactor = generalSuccessFactor;
                PlantFactor = plantFactor;
                FungusFactor = fungusFactor;
                AnimalFactor = animalFactor;
                FailureRiskFactor = failureRiskFactor;
                ManhunterRiskFactor = manhunterRiskFactor;
            }

            public static TimeProfile For(int hour, BiomeDef biome, float temperature)
            {
                bool night = hour >= 21 || hour <= 5;
                bool dawnOrDusk = (hour >= 5 && hour <= 7) || (hour >= 18 && hour <= 20);
                bool hotAridNight = night && temperature >= 18f && IsAridBiome(biome);

                if (hotAridNight)
                {
                    return new TimeProfile(1.08f, 0.8f, 0.85f, 1.35f, 1.2f, 1.15f);
                }

                if (night)
                {
                    return new TimeProfile(0.9f, 0.8f, 1.05f, 1.18f, 1.35f, 1.2f);
                }

                if (dawnOrDusk)
                {
                    return new TimeProfile(1.05f, 1f, 1.05f, 1.08f, 0.95f, 1f);
                }

                return new TimeProfile(1f, 1f, 1f, 1f, 1f, 1f);
            }

            private static bool IsAridBiome(BiomeDef biome)
            {
                switch (biome?.defName)
                {
                    case "AridShrubland":
                    case "Desert":
                    case "ExtremeDesert":
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}
