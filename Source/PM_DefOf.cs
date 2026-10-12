using RimWorld;
using Verse;

namespace Paramedic;

[DefOf]
public static class PM_DefOf
{
    public static HediffDef PM_OnDuty;
    public static HediffDef PM_Rank;
    public static HediffDef PM_SteadyNerves;
    public static HediffDef PM_PressureBandage;
    public static HediffDef PM_ShoutProtection;
    public static HediffDef PM_Adrenaline;
    public static HediffDef PM_Comedown;
    public static HediffDef PM_AdrenalineCooldown;
    public static HediffDef PM_CrisisOfFaith;

    public static ThoughtDef PM_OathBroken;
    public static ThoughtDef PM_OathWeight;
    public static ThoughtDef PM_Perfidy;
    public static ThoughtDef PM_NotSaved;
    public static ThoughtDef PM_TreatedByParamedic;
    public static ThoughtDef PM_Burnout;

    public static AbilityDef PM_Stabilize;
    public static AbilityDef PM_DontShoot;
    public static AbilityDef PM_AdrenalineShot;

    public static ThingDef PM_AdrenalineSyringe;
    public static RecordDef PM_LivesSaved;
    public static HistoryEventDef PM_HumanitarianAid;
    public static HistoryEventDef PM_PerfidyEvent;

    static PM_DefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(PM_DefOf));
}
