using HarmonyLib;
using UnityEngine;
using Verse;

namespace Paramedic;

public class ParamedicMod : Mod
{
    public static ParamedicSettings Settings;

    public ParamedicMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<ParamedicSettings>();
        PatchAllSafe(new Harmony("bebranotyep.paramedic"));
    }

    /// <summary>Каждый патч отдельно: сбой одного (например, после обновления игры) не отключает остальные.</summary>
    private static void PatchAllSafe(Harmony harmony)
    {
        foreach (var type in AccessTools.GetTypesFromAssembly(typeof(ParamedicMod).Assembly))
        {
            if (!type.IsDefined(typeof(HarmonyPatch), inherit: false)) continue;
            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (System.Exception e)
            {
                Log.Error($"[Paramedic] patch {type.Name} failed: {e.InnerException?.Message ?? e.Message}");
            }
        }
    }

    public override string SettingsCategory() => "Paramedic: Don't Die Yet!";

    public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);
}

public class ParamedicSettings : ModSettings
{
    public float oathStrength = 1f;
    public bool crisisEnabled = true;
    public bool burnoutEnabled = true;
    public bool humanitarianEnabled = true;
    public float shoutHours = 2f;
    public float shoutRespectFactor = 1f;
    public float cooldownFactor = 1f;
    public bool autoDoctorPriority;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref oathStrength, "oathStrength", 1f);
        Scribe_Values.Look(ref crisisEnabled, "crisisEnabled", true);
        Scribe_Values.Look(ref burnoutEnabled, "burnoutEnabled", true);
        Scribe_Values.Look(ref humanitarianEnabled, "humanitarianEnabled", true);
        Scribe_Values.Look(ref shoutHours, "shoutHours", 2f);
        Scribe_Values.Look(ref shoutRespectFactor, "shoutRespectFactor", 1f);
        Scribe_Values.Look(ref cooldownFactor, "cooldownFactor", 1f);
        Scribe_Values.Look(ref autoDoctorPriority, "autoDoctorPriority", false);
    }

    public void DoWindowContents(Rect rect)
    {
        var l = new Listing_Standard();
        l.Begin(rect);
        l.Label("PM_Set_OathStrength".Translate(oathStrength.ToStringPercent()));
        oathStrength = Mathf.Round(l.Slider(oathStrength, 0f, 2f) * 20f) / 20f;
        l.Label("PM_Set_ShoutHours".Translate(shoutHours.ToString("0.#")));
        shoutHours = Mathf.Round(l.Slider(shoutHours, 0.5f, 6f) * 2f) / 2f;
        l.Label("PM_Set_ShoutRespect".Translate(shoutRespectFactor.ToStringPercent()));
        shoutRespectFactor = Mathf.Round(l.Slider(shoutRespectFactor, 0f, 1.5f) * 20f) / 20f;
        l.Label("PM_Set_Cooldown".Translate(cooldownFactor.ToStringPercent()));
        cooldownFactor = Mathf.Round(l.Slider(cooldownFactor, 0.25f, 2f) * 20f) / 20f;
        l.GapLine();
        l.CheckboxLabeled("PM_Set_Crisis".Translate(), ref crisisEnabled);
        l.CheckboxLabeled("PM_Set_Burnout".Translate(), ref burnoutEnabled);
        l.CheckboxLabeled("PM_Set_Humanitarian".Translate(), ref humanitarianEnabled);
        l.CheckboxLabeled("PM_Set_AutoDoctor".Translate(), ref autoDoctorPriority, "PM_Set_AutoDoctor_Desc".Translate());
        l.Gap();
        if (l.ButtonText("PM_Set_Reset".Translate()))
        {
            oathStrength = 1f; crisisEnabled = burnoutEnabled = humanitarianEnabled = true;
            shoutHours = 2f; shoutRespectFactor = 1f; cooldownFactor = 1f; autoDoctorPriority = false;
        }
        l.End();
    }
}
