using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldBushcraft;

public class WorkGiver_BushcraftForage : WorkGiver_Scanner
{
    public override bool Prioritized => true;

    public override IEnumerable<IntVec3> PotentialWorkCellsGlobal(Pawn pawn)
    {
        if (pawn.Map == null)
        {
            yield break;
        }

        foreach (Zone zone in pawn.Map.zoneManager.AllZones)
        {
            if (zone is not Zone_Bushcraft bushcraftZone || !bushcraftZone.ShouldForageNow)
            {
                continue;
            }

            foreach (IntVec3 cell in bushcraftZone.ForageableCells)
            {
                yield return cell;
            }
        }
    }

    public override bool HasJobOnCell(Pawn pawn, IntVec3 c, bool forced = false)
    {
        return c.InBounds(pawn.Map)
            && c.GetZone(pawn.Map) is Zone_Bushcraft zone
            && zone.ShouldForageNow
            && zone.IsForageable(c)
            && !c.IsForbidden(pawn)
            && pawn.CanReserveAndReach(c, PathEndMode.OnCell, Danger.Some, 1, -1, ReservationLayerDefOf.Floor, forced);
    }

    public override Job JobOnCell(Pawn pawn, IntVec3 cell, bool forced = false)
    {
        return JobMaker.MakeJob(BushcraftJobDefOf.Bushcraft_Forage, cell);
    }

    public override float GetPriority(Pawn pawn, TargetInfo t)
    {
        float distance = pawn.Position.DistanceToSquared(t.Cell);
        return 10000f - distance;
    }
}
