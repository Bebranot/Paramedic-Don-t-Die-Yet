using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Paramedic;

public enum GearSlot { Shirt, Pants, Vest, Jacket, Head, Goggles, Belt }

public class CompProperties_ParamedicGear : CompProperties
{
    public GearSlot slot;

    public CompProperties_ParamedicGear() => compClass = typeof(CompParamedicGear);

    public bool IsCore => slot == GearSlot.Vest || slot == GearSlot.Head;
}

/// <summary>Вещь формы: при надевании/снятии пересчитывает роль владельца.</summary>
public class CompParamedicGear : ThingComp
{
    public CompProperties_ParamedicGear Props => (CompProperties_ParamedicGear)props;

    public override void Notify_Equipped(Pawn pawn) => ParamedicUtility.Recalc(pawn);

    public override void Notify_Unequipped(Pawn pawn) => ParamedicUtility.Recalc(pawn);

    public override string CompInspectStringExtra() =>
        Props.IsCore ? "PM_GearCore".Translate().ToString() : "PM_GearExtra".Translate().ToString();
}

/// <summary>Состояние «На смене»: медицинские бонусы масштабируются качеством формы.</summary>
public class Hediff_OnDuty : HediffWithComps
{
    public float qualityFactor = 1f;

    private HediffStage cached;
    private int cachedIndex = -1;
    private float cachedFactor = -1f;

    private static readonly HashSet<StatDef> Scaled = new();

    public override HediffStage CurStage
    {
        get
        {
            HediffStage stage = base.CurStage;
            if (stage == null) return null;
            if (cachedIndex != CurStageIndex || cachedFactor != qualityFactor || cached == null)
            {
                cached = Scale(stage, qualityFactor);
                cachedIndex = CurStageIndex;
                cachedFactor = qualityFactor;
            }
            return cached;
        }
    }

    public override string LabelInBrackets => base.LabelInBrackets + (Mathf.Approximately(qualityFactor, 1f) ? "" : " ×" + qualityFactor.ToString("0.##"));

    private static HediffStage Scale(HediffStage src, float f)
    {
        if (Scaled.Count == 0)
        {
            Scaled.Add(StatDefOf.MedicalTendQuality);
            Scaled.Add(StatDefOf.MedicalTendSpeed);
            Scaled.Add(StatDefOf.MedicalSurgerySuccessChance);
        }
        // медицинские множители масштабируются качеством: 1.15 при ×1.5 → 1.225, при ×0.5 → 1.075
        var st = new HediffStage
        {
            minSeverity = src.minSeverity,
            label = src.label,
            becomeVisible = src.becomeVisible,
            statOffsets = src.statOffsets,
            capMods = src.capMods,
            statFactors = new List<StatModifier>(),
        };
        if (src.statFactors != null)
        {
            foreach (var m in src.statFactors)
            {
                float v = Scaled.Contains(m.stat) ? 1f + (m.value - 1f) * f : m.value;
                st.statFactors.Add(new StatModifier { stat = m.stat, value = v });
            }
        }
        return st;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref qualityFactor, "qualityFactor", 1f);
    }
}

/// <summary>Переноска раненого быстрее на смене (и ещё чуть быстрее в штанах медика).</summary>
public class StatPart_ParamedicCarrying : StatPart
{
    public float onDutyFactor = 1.25f;
    public float pantsFactor = 1.05f;

    private float Factor(Pawn pawn)
    {
        if (pawn?.carryTracker?.CarriedThing is not Pawn) return 1f;
        float f = 1f;
        if (ParamedicUtility.IsOnDuty(pawn)) f *= onDutyFactor;
        if (ParamedicUtility.Wears(pawn, GearSlot.Pants)) f *= pantsFactor;
        return f;
    }

    public override void TransformValue(StatRequest req, ref float val)
    {
        if (req.Thing is Pawn pawn) val *= Factor(pawn);
    }

    public override string ExplanationPart(StatRequest req)
    {
        if (req.Thing is Pawn pawn)
        {
            float f = Factor(pawn);
            if (f != 1f) return "PM_StatCarrying".Translate() + ": x" + f.ToStringPercent();
        }
        return null;
    }
}
