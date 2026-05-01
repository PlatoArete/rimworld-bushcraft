using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimWorldBushcraft;

public class Zone_Bushcraft : Zone
{
    private const float MinForageDensity = 0.05f;
    private const float DensityLossPerFind = 0.045f;
    private bool allowed = true;
    private float forageDensity = 1f;

    private readonly List<IntVec3> tmpForageableCells = new List<IntVec3>();

    public override bool IsMultiselectable => true;

    protected override Color NextZoneColor => new Color(0.37f, 0.72f, 0.28f, 0.09f);

    public bool Allowed => allowed;

    public float ForageDensity => forageDensity;

    public bool ShouldForageNow => allowed && HasAnyForageableCells;

    public bool HasAnyForageableCells
    {
        get
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (IsForageable(cells[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public List<IntVec3> ForageableCells
    {
        get
        {
            tmpForageableCells.Clear();
            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 cell = cells[i];
                if (IsForageable(cell))
                {
                    tmpForageableCells.Add(cell);
                }
            }

            return tmpForageableCells;
        }
    }

    public Zone_Bushcraft()
    {
    }

    public Zone_Bushcraft(ZoneManager zoneManager)
        : base("Bushcraft_ZoneLabel".Translate(), zoneManager)
    {
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref allowed, "allowed", defaultValue: true);
        Scribe_Values.Look(ref forageDensity, "forageDensity", defaultValue: 1f);
    }

    public bool IsForageable(IntVec3 cell)
    {
        if (!cell.InBounds(Map) || cell.Fogged(Map))
        {
            return false;
        }

        TerrainDef terrain = cell.GetTerrain(Map);
        return terrain != null && !terrain.IsWater && terrain.passability != Traversability.Impassable && cell.Standable(Map);
    }

    public void Notify_Foraged(Thing result)
    {
        if (result == null)
        {
            return;
        }

        float valueFactor = Mathf.Clamp(result.MarketValue * result.stackCount / 40f, 0.5f, 2f);
        forageDensity = Mathf.Clamp(forageDensity - DensityLossPerFind * valueFactor, MinForageDensity, 1f);
    }

    public override string GetInspectString()
    {
        StringBuilder stringBuilder = new StringBuilder(base.GetInspectString());
        if (stringBuilder.Length > 0)
        {
            stringBuilder.AppendLine();
        }

        stringBuilder.AppendLine("Bushcraft_ForageDensity".Translate(forageDensity.ToStringPercent()));
        return stringBuilder.ToString().TrimEndNewlines();
    }

    public override IEnumerable<Gizmo> GetZoneAddGizmos()
    {
        yield return DesignatorUtility.FindAllowedDesignator<Designator_ZoneAdd_Bushcraft_Expand>();
    }

    public void RecoverDensity(int ticks)
    {
        if (forageDensity >= 1f)
        {
            return;
        }

        forageDensity = Mathf.Min(1f, forageDensity + BushcraftMapComponent.DensityRecoveryPerTick * ticks * RecoveryRateFactor);
    }

    public float RecoveryRateFactor
    {
        get
        {
            if (forageDensity >= 0.8f)
            {
                return 1f;
            }

            if (forageDensity >= 0.5f)
            {
                return 0.65f;
            }

            if (forageDensity >= 0.2f)
            {
                return 0.35f;
            }

            return 0.15f;
        }
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        yield return new Command_Toggle
        {
            defaultLabel = "Bushcraft allowed",
            defaultDesc = "Allow pawns to work this bushcraft zone.",
            icon = TexCommand.ForbidOff,
            isActive = () => allowed,
            toggleAction = () => allowed = !allowed
        };

        if (!Prefs.DevMode)
        {
            yield break;
        }

        yield return new Command_Action
        {
            defaultLabel = "Bushcraft debug",
            defaultDesc = "Open a debug readout for this bushcraft zone.",
            icon = TexCommand.DesirePower,
            action = () => Find.WindowStack.Add(new Window_BushcraftDebug(this))
        };
    }
}
