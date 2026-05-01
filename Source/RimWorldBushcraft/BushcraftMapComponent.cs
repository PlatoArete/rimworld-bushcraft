using RimWorld;
using Verse;

namespace RimWorldBushcraft;

public class BushcraftMapComponent : MapComponent
{
    public const float DensityRecoveryPerTick = 1f / 60000f;

    private const int RecoveryIntervalTicks = 250;

    public BushcraftMapComponent(Map map)
        : base(map)
    {
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();
        if (Find.TickManager.TicksGame % RecoveryIntervalTicks != 0)
        {
            return;
        }

        foreach (Zone zone in map.zoneManager.AllZones)
        {
            if (zone is Zone_Bushcraft bushcraftZone)
            {
                bushcraftZone.RecoverDensity(RecoveryIntervalTicks);
            }
        }
    }
}

