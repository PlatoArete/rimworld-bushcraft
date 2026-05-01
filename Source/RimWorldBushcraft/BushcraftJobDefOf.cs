using RimWorld;
using Verse;

namespace RimWorldBushcraft;

[DefOf]
public static class BushcraftJobDefOf
{
    public static JobDef Bushcraft_Forage;

    static BushcraftJobDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(BushcraftJobDefOf));
    }
}

