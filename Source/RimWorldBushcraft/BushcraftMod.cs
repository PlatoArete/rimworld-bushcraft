using UnityEngine;
using Verse;

namespace RimWorldBushcraft;

public class BushcraftMod : Mod
{
    public static BushcraftSettings Settings;

    private Vector2 scrollPosition;

    public BushcraftMod(ModContentPack content)
        : base(content)
    {
        Settings = GetSettings<BushcraftSettings>();
    }

    public override string SettingsCategory()
    {
        return "RimWorld Bushcraft";
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoWindowContents(inRect, ref scrollPosition);
    }
}
