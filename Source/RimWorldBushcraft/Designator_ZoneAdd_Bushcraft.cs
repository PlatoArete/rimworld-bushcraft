using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldBushcraft;

public class Designator_ZoneAdd_Bushcraft : Designator_ZoneAdd
{
    protected override string NewZoneLabel => "Bushcraft_ZoneLabel".Translate();

    protected virtual bool UseExpandLabel => false;

    public Designator_ZoneAdd_Bushcraft()
    {
        zoneTypeToPlace = typeof(Zone_Bushcraft);
        defaultLabel = "Bushcraft_ZoneLabel".Translate();
        defaultDesc = "Bushcraft_DesignatorZoneDesc".Translate();
        tutorTag = "ZoneAdd_Bushcraft";
        icon = ContentFinder<Texture2D>.Get("UI/Designators/ZoneCreate_Growing");
        hotKey = KeyBindingDefOf.Misc6;
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        AcceptanceReport baseReport = base.CanDesignateCell(c);
        if (!baseReport.Accepted)
        {
            return baseReport;
        }

        TerrainDef terrain = c.GetTerrain(Map);
        if (terrain == null || terrain.IsWater || terrain.passability == Traversability.Impassable || !c.Standable(Map))
        {
            return "Bushcraft_CannotPlaceZone".Translate();
        }

        return true;
    }

    protected override Zone MakeNewZone()
    {
        return new Zone_Bushcraft(Find.CurrentMap.zoneManager);
    }

    protected override void FinalizeDesignationFailed()
    {
        base.FinalizeDesignationFailed();
        Messages.Message("Bushcraft_CannotPlaceZone".Translate(), MessageTypeDefOf.RejectInput, historical: false);
    }
}

