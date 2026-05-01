using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimWorldBushcraft;

public class JobDriver_BushcraftForage : JobDriver
{
    private const TargetIndex ForageCellInd = TargetIndex.A;
    private const int BaseWorkTicks = 2500;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.GetTarget(ForageCellInd), job, 1, -1, ReservationLayerDefOf.Floor, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOn(() => !job.GetTarget(ForageCellInd).Cell.InBounds(pawn.Map));
        this.FailOn(() => job.GetTarget(ForageCellInd).Cell.GetZone(pawn.Map) is not Zone_Bushcraft zone || !zone.ShouldForageNow);

        yield return Toils_Goto.GotoCell(ForageCellInd, PathEndMode.OnCell);

        Toil forage = Toils_General.Wait(BaseWorkTicks, ForageCellInd);
        forage.WithProgressBarToilDelay(ForageCellInd);
        forage.tickAction = delegate
        {
            pawn.skills?.Learn(SkillDefOf.Plants, 0.015f);
            pawn.skills?.Learn(SkillDefOf.Animals, 0.01f);
        };
        forage.activeSkill = () => SkillDefOf.Plants;
        yield return forage;

        yield return CompleteForageToil();
    }

    private Toil CompleteForageToil()
    {
        Toil toil = ToilMaker.MakeToil("CompleteBushcraftForage");
        toil.initAction = delegate
        {
            Zone_Bushcraft zone = job.GetTarget(ForageCellInd).Cell.GetZone(pawn.Map) as Zone_Bushcraft;
            Thing result = BushcraftLootUtility.TryGenerateForageResult(pawn, zone);
            if (result != null)
            {
                GenPlace.TryPlaceThing(result, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                zone?.Notify_Foraged(result);
            }
        };
        return toil;
    }
}
