using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldBushcraft;

public class Window_BushcraftDebug : Window
{
    private readonly Zone_Bushcraft zone;
    private Vector2 scrollPosition;

    public override Vector2 InitialSize => new Vector2(620f, 520f);

    public Window_BushcraftDebug(Zone_Bushcraft zone)
    {
        this.zone = zone;
        doCloseButton = true;
        doCloseX = true;
        absorbInputAroundWindow = false;
        forcePause = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Bushcraft debug");
        Text.Font = GameFont.Small;

        string report = BuildReport();
        Rect outRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, inRect.height - 76f);
        Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Text.CalcHeight(report, outRect.width - 16f) + 8f);
        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        Widgets.Label(viewRect, report);
        Widgets.EndScrollView();
    }

    private string BuildReport()
    {
        if (zone == null || zone.Map == null)
        {
            return "Zone is no longer available.";
        }

        Map map = zone.Map;
        List<IntVec3> forageableCells = zone.ForageableCells.ToList();
        List<BushcraftLootUtility.BushcraftDiscoveredLootCandidate> vanillaCandidates = BushcraftLootUtility.BaseLootCandidates();
        List<BushcraftLootUtility.BushcraftDiscoveredLootCandidate> discoveredCandidates = BushcraftLootUtility.DiscoverWildPlantHarvests(map);

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"Zone: {zone.label}");
        builder.AppendLine($"Total cells: {zone.Cells.Count}");
        builder.AppendLine($"Forageable cells: {forageableCells.Count}");
        builder.AppendLine($"Allowed: {zone.Allowed}");
        builder.AppendLine($"Should forage now: {zone.ShouldForageNow}");
        builder.AppendLine($"Forage density: {zone.ForageDensity.ToStringPercent()}");
        builder.AppendLine($"Recovery rate factor: {zone.RecoveryRateFactor.ToStringPercent()}");
        builder.AppendLine();

        builder.AppendLine("Map conditions");
        builder.AppendLine($"Biome: {map.Biome?.LabelCap ?? "None"} ({map.Biome?.defName ?? "none"})");
        builder.AppendLine($"Season: {GenLocalDate.Season(map)}");
        builder.AppendLine($"Hour: {GenLocalDate.HourOfDay(map)}");
        builder.AppendLine($"Outdoor temperature: {map.mapTemperature.OutdoorTemp.ToStringTemperature()}");
        builder.AppendLine($"Weather: {map.weatherManager?.curWeather?.LabelCap ?? "None"}");
        builder.AppendLine();

        builder.AppendLine("Loot candidates");
        builder.AppendLine($"Vanilla candidates: {vanillaCandidates.Count}");
        builder.AppendLine($"Discovered wild plant harvests for this biome: {discoveredCandidates.Count}");
        AppendCandidateList(builder, discoveredCandidates, "Discovered");
        builder.AppendLine();

        builder.AppendLine("Terrain mix");
        AppendTerrainMix(builder, forageableCells, map);

        return builder.ToString().TrimEndNewlines();
    }

    private static void AppendCandidateList(
        StringBuilder builder,
        List<BushcraftLootUtility.BushcraftDiscoveredLootCandidate> candidates,
        string label)
    {
        if (candidates.Count == 0)
        {
            builder.AppendLine($"{label}: none");
            return;
        }

        foreach (BushcraftLootUtility.BushcraftDiscoveredLootCandidate candidate in candidates.OrderBy(candidate => candidate.ThingDef.label).Take(18))
        {
            string modName = candidate.ThingDef.modContentPack?.Name ?? "Unknown mod";
            builder.AppendLine($"- {candidate.ThingDef.LabelCap} [{candidate.Category}] weight {candidate.BaseWeight:0.###}, count {candidate.MinCount}-{candidate.MaxCount}, {modName}");
        }

        if (candidates.Count > 18)
        {
            builder.AppendLine($"- ...and {candidates.Count - 18} more");
        }
    }

    private static void AppendTerrainMix(StringBuilder builder, List<IntVec3> forageableCells, Map map)
    {
        if (forageableCells.Count == 0)
        {
            builder.AppendLine("No forageable terrain.");
            return;
        }

        Dictionary<TerrainDef, int> terrainCounts = new Dictionary<TerrainDef, int>();
        for (int i = 0; i < forageableCells.Count; i++)
        {
            TerrainDef terrain = forageableCells[i].GetTerrain(map);
            if (terrain == null)
            {
                continue;
            }

            terrainCounts.TryGetValue(terrain, out int count);
            terrainCounts[terrain] = count + 1;
        }

        foreach (KeyValuePair<TerrainDef, int> pair in terrainCounts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key.label).Take(12))
        {
            builder.AppendLine($"- {pair.Key.LabelCap}: {pair.Value} cells ({((float)pair.Value / forageableCells.Count).ToStringPercent()})");
        }
    }
}
