using RimWorld;
using Verse;

namespace Paramedic;

/// <summary>«Полевая стабилизация»: давящая повязка на упавшего, расход одного медикамента из инвентаря.</summary>
public class CompProperties_AbilityStabilize : CompProperties_AbilityEffect
{
    public CompProperties_AbilityStabilize() => compClass = typeof(CompAbilityEffect_Stabilize);
}

public class CompAbilityEffect_Stabilize : CompAbilityEffect
{
    public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
    {
        if (target.Pawn is not Pawn p || !p.Downed || p.Dead)
        {
            if (throwMessages) Messages.Message("PM_NeedDowned".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }
        if (p.health.hediffSet.HasHediff(PM_DefOf.PM_PressureBandage))
        {
            if (throwMessages) Messages.Message("PM_AlreadyBandaged".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }
        return base.Valid(target, throwMessages);
    }

    public override bool GizmoDisabled(out string reason)
    {
        if (!ParamedicUtility.HasMedicineInInventory(parent.pawn, out _))
        {
            reason = "PM_NoMedicine".Translate();
            return true;
        }
        return base.GizmoDisabled(out reason);
    }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        Pawn medic = parent.pawn;
        Pawn patient = target.Pawn;
        if (patient == null || !ParamedicUtility.HasMedicineInInventory(medic, out Thing med)) return;
        ParamedicUtility.ConsumeOne(medic, med);
        patient.health.AddHediff(PM_DefOf.PM_PressureBandage);
        TendPatches.Notify_ParamedicHelped(medic, patient);
    }
}

/// <summary>«Адреналин»: упавший союзник встаёт на полминуты, потом отходняк.</summary>
public class CompProperties_AbilityAdrenaline : CompProperties_AbilityEffect
{
    public CompProperties_AbilityAdrenaline() => compClass = typeof(CompAbilityEffect_Adrenaline);
}

public class CompAbilityEffect_Adrenaline : CompAbilityEffect
{
    private Thing Syringe()
    {
        var inv = parent.pawn.inventory?.innerContainer;
        if (inv == null) return null;
        foreach (Thing t in inv)
        {
            if (t.def == PM_DefOf.PM_AdrenalineSyringe) return t;
        }
        return null;
    }

    public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
    {
        if (target.Pawn is not Pawn p || !p.Downed || p.Dead || !p.RaceProps.Humanlike)
        {
            if (throwMessages) Messages.Message("PM_NeedDowned".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }
        if (p.health.hediffSet.HasHediff(PM_DefOf.PM_AdrenalineCooldown))
        {
            if (throwMessages) Messages.Message("PM_AdrenalineHeart".Translate(p.Named("PAWN")), MessageTypeDefOf.RejectInput, false);
            return false;
        }
        return base.Valid(target, throwMessages);
    }

    public override bool GizmoDisabled(out string reason)
    {
        if (!ParamedicUtility.Wears(parent.pawn, GearSlot.Belt))
        {
            reason = "PM_NeedBelt".Translate();
            return true;
        }
        if (Syringe() == null)
        {
            reason = "PM_NoSyringe".Translate();
            return true;
        }
        return base.GizmoDisabled(out reason);
    }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        Pawn patient = target.Pawn;
        Thing syringe = Syringe();
        if (patient == null || syringe == null) return;
        ParamedicUtility.ConsumeOne(parent.pawn, syringe);
        patient.health.AddHediff(PM_DefOf.PM_Adrenaline);
        patient.health.AddHediff(PM_DefOf.PM_AdrenalineCooldown);
    }
}

/// <summary>По окончании действия хедиффа выдаёт другой (адреналин → отходняк).</summary>
public class HediffCompProperties_GiveOnRemoved : HediffCompProperties
{
    public HediffDef hediff;

    public HediffCompProperties_GiveOnRemoved() => compClass = typeof(HediffComp_GiveOnRemoved);
}

public class HediffComp_GiveOnRemoved : HediffComp
{
    public override void CompPostPostRemoved()
    {
        var def = ((HediffCompProperties_GiveOnRemoved)props).hediff;
        if (def != null && !Pawn.Dead) Pawn.health.AddHediff(def);
    }
}
