using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldBushcraft;

public class BushcraftSettings : ModSettings
{
    private const float ScrollbarWidth = 16f;
    private const float RowHeight = 30f;
    private const float SectionHeight = 34f;

    public float discoveredLootWeightMultiplier = 1f;
    public float discoveredLootCountMultiplier = 1f;
    public float badFailureChanceMultiplier = 1f;
    public float manhunterChanceMultiplier = 1f;

    private string searchText = string.Empty;
    private List<BushcraftDiscoveredLootSetting> discoveredLootSettings = new List<BushcraftDiscoveredLootSetting>();

    public override void ExposeData()
    {
        Scribe_Values.Look(ref discoveredLootWeightMultiplier, "discoveredLootWeightMultiplier", 1f);
        Scribe_Values.Look(ref discoveredLootCountMultiplier, "discoveredLootCountMultiplier", 1f);
        Scribe_Values.Look(ref badFailureChanceMultiplier, "badFailureChanceMultiplier", 1f);
        Scribe_Values.Look(ref manhunterChanceMultiplier, "manhunterChanceMultiplier", 1f);
        Scribe_Collections.Look(ref discoveredLootSettings, "discoveredLootSettings", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit && discoveredLootSettings == null)
        {
            discoveredLootSettings = new List<BushcraftDiscoveredLootSetting>();
        }
    }

    public BushcraftDiscoveredLootSetting SettingFor(ThingDef thingDef)
    {
        if (thingDef == null)
        {
            return null;
        }

        for (int i = 0; i < discoveredLootSettings.Count; i++)
        {
            if (discoveredLootSettings[i].thingDefName == thingDef.defName)
            {
                return discoveredLootSettings[i];
            }
        }

        BushcraftDiscoveredLootSetting setting = new BushcraftDiscoveredLootSetting(thingDef);
        discoveredLootSettings.Add(setting);
        return setting;
    }

    public void DoWindowContents(Rect inRect, ref Vector2 scrollPosition)
    {
        HashSet<string> knownLootDefNames = EnsureKnownLootSettings();
        List<BushcraftDiscoveredLootSetting> visibleSettings = VisibleSettings(knownLootDefNames);
        List<BushcraftDiscoveredLootSetting> moddedSettings = visibleSettings
            .Where(setting => !BushcraftLootUtility.IsVanillaThingDef(setting.ThingDef))
            .OrderBy(setting => setting.Label)
            .ToList();
        List<BushcraftDiscoveredLootSetting> vanillaSettings = visibleSettings
            .Where(setting => BushcraftLootUtility.IsVanillaThingDef(setting.ThingDef))
            .OrderBy(setting => setting.Label)
            .ToList();

        int rowCount = moddedSettings.Count + vanillaSettings.Count;
        float viewHeight = 330f + rowCount * RowHeight + SectionHeight * 2f;
        Rect viewRect = new Rect(0f, 0f, inRect.width - ScrollbarWidth, viewHeight);
        Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

        Listing_Standard listing = new Listing_Standard();
        listing.Begin(viewRect);
        listing.Label("Global bushcraft tuning");
        discoveredLootWeightMultiplier = listing.SliderLabeled(
            $"Discovered item odds: {discoveredLootWeightMultiplier.ToStringPercent()}",
            discoveredLootWeightMultiplier,
            0f,
            3f);
        discoveredLootCountMultiplier = listing.SliderLabeled(
            $"Discovered item count: {discoveredLootCountMultiplier.ToStringPercent()}",
            discoveredLootCountMultiplier,
            0.25f,
            3f);
        badFailureChanceMultiplier = listing.SliderLabeled(
            $"Bad failure odds: {badFailureChanceMultiplier.ToStringPercent()}",
            badFailureChanceMultiplier,
            0f,
            3f);
        manhunterChanceMultiplier = listing.SliderLabeled(
            $"Manhunter odds: {manhunterChanceMultiplier.ToStringPercent()}",
            manhunterChanceMultiplier,
            0f,
            3f);

        listing.GapLine();
        listing.Label("Forage item tuning");
        listing.Label("Discovered entries come from loaded wild plants with harvestedThingDef. Manufactured outputs are skipped by default.");
        listing.Label("Search");
        searchText = Widgets.TextField(listing.GetRect(RowHeight), searchText ?? string.Empty);
        listing.End();

        float curY = listing.CurHeight;
        curY = DrawSection(new Rect(0f, curY, viewRect.width, SectionHeight), "Modded items", moddedSettings, curY);
        curY = DrawSection(new Rect(0f, curY, viewRect.width, SectionHeight), "Vanilla items", vanillaSettings, curY);

        Widgets.EndScrollView();
    }

    private HashSet<string> EnsureKnownLootSettings()
    {
        HashSet<string> knownLootDefNames = new HashSet<string>();
        foreach (BushcraftLootUtility.BushcraftDiscoveredLootCandidate candidate in BushcraftLootUtility.BaseLootCandidates())
        {
            knownLootDefNames.Add(candidate.ThingDef.defName);
            SettingFor(candidate.ThingDef);
        }

        foreach (BushcraftLootUtility.BushcraftDiscoveredLootCandidate candidate in BushcraftLootUtility.DiscoverWildPlantHarvests(null))
        {
            knownLootDefNames.Add(candidate.ThingDef.defName);
            SettingFor(candidate.ThingDef);
        }

        return knownLootDefNames;
    }

    private List<BushcraftDiscoveredLootSetting> VisibleSettings(HashSet<string> knownLootDefNames)
    {
        string normalizedSearch = (searchText ?? string.Empty).Trim().ToLowerInvariant();
        List<BushcraftDiscoveredLootSetting> settings = new List<BushcraftDiscoveredLootSetting>();

        for (int i = 0; i < discoveredLootSettings.Count; i++)
        {
            BushcraftDiscoveredLootSetting setting = discoveredLootSettings[i];
            if (setting?.thingDefName == null || !knownLootDefNames.Contains(setting.thingDefName) || setting.ThingDef == null)
            {
                continue;
            }

            if (normalizedSearch.Length > 0
                && !setting.Label.ToLowerInvariant().Contains(normalizedSearch)
                && !setting.thingDefName.ToLowerInvariant().Contains(normalizedSearch))
            {
                continue;
            }

            settings.Add(setting);
        }

        return settings;
    }

    private static float DrawSection(Rect rect, string label, List<BushcraftDiscoveredLootSetting> settings, float curY)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(rect, label);
        Text.Font = GameFont.Small;
        curY += SectionHeight;

        if (settings.Count == 0)
        {
            Widgets.Label(new Rect(rect.x + 16f, curY, rect.width - 16f, RowHeight), "No matching items.");
            return curY + RowHeight;
        }

        DrawDiscoveredLootHeader(new Rect(rect.x, curY, rect.width, RowHeight));
        curY += RowHeight;

        for (int i = 0; i < settings.Count; i++)
        {
            DrawDiscoveredLootRow(new Rect(rect.x, curY, rect.width, RowHeight), settings[i]);
            curY += RowHeight;
        }

        return curY + 8f;
    }

    private static void DrawDiscoveredLootHeader(Rect rect)
    {
        Widgets.Label(new Rect(rect.x, rect.y, 40f, rect.height), "Use");
        Widgets.Label(new Rect(rect.x + 45f, rect.y, 245f, rect.height), "Item");
        Widgets.Label(new Rect(rect.x + 295f, rect.y, 160f, rect.height), "Odds");
        Widgets.Label(new Rect(rect.x + 460f, rect.y, 160f, rect.height), "Count");
    }

    private static void DrawDiscoveredLootRow(Rect rect, BushcraftDiscoveredLootSetting setting)
    {
        string label = setting.ThingDef?.LabelCap ?? setting.thingDefName;

        Widgets.Checkbox(new Vector2(rect.x + 8f, rect.y + 5f), ref setting.enabled);
        Widgets.Label(new Rect(rect.x + 45f, rect.y, 245f, rect.height), label);

        setting.weightMultiplier = Widgets.HorizontalSlider(
            new Rect(rect.x + 295f, rect.y + 6f, 120f, 20f),
            setting.weightMultiplier,
            0f,
            3f);
        Widgets.Label(new Rect(rect.x + 420f, rect.y, 45f, rect.height), setting.weightMultiplier.ToStringPercent());

        setting.countMultiplier = Widgets.HorizontalSlider(
            new Rect(rect.x + 460f, rect.y + 6f, 120f, 20f),
            setting.countMultiplier,
            0.25f,
            3f);
        Widgets.Label(new Rect(rect.x + 585f, rect.y, 45f, rect.height), setting.countMultiplier.ToStringPercent());
    }
}

public class BushcraftDiscoveredLootSetting : IExposable
{
    public string thingDefName;
    public bool enabled = true;
    public float weightMultiplier = 1f;
    public float countMultiplier = 1f;

    public BushcraftDiscoveredLootSetting()
    {
    }

    public BushcraftDiscoveredLootSetting(ThingDef thingDef)
    {
        thingDefName = thingDef.defName;
    }

    public string Label => DefDatabase<ThingDef>.GetNamed(thingDefName, errorOnFail: false)?.label ?? thingDefName;

    public ThingDef ThingDef => DefDatabase<ThingDef>.GetNamed(thingDefName, errorOnFail: false);

    public void ExposeData()
    {
        Scribe_Values.Look(ref thingDefName, "thingDefName");
        Scribe_Values.Look(ref enabled, "enabled", true);
        Scribe_Values.Look(ref weightMultiplier, "weightMultiplier", 1f);
        Scribe_Values.Look(ref countMultiplier, "countMultiplier", 1f);
    }
}
