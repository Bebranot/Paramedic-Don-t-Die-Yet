using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Paramedic;

/// <summary>«Медик! Не стрелять!» — защита от выбора целью враждебными людьми.</summary>
public static class ShoutUtility
{
    /// <summary>Пешки под защитой креста (быстрая проверка в горячем пути ThreatDisabled).</summary>
    public static readonly HashSet<Pawn> Protected = new();

    public static bool IsProtectedFrom(Pawn medic, Thing searcherThing)
    {
        if (!Protected.Contains(medic)) return false;
        if (searcherThing is not Pawn searcher || searcher == medic) return false;
        if (!searcher.RaceProps.Humanlike || searcher.IsMutant) return false;
        if (!searcher.HostileTo(medic)) return false;
        var comp = medic.health.hediffSet.GetFirstHediffOfDef(PM_DefOf.PM_ShoutProtection)?.TryGetComp<HediffComp_ShoutProtection>();
        if (comp == null) return false;
        float chance = RespectChance(searcher, medic);
        if (chance <= 0f) return false;
        // Детерминированный бросок: один и тот же враг в рамках одного крика решает одинаково
        int roll = Gen.HashCombineInt(searcher.thingIDNumber, comp.shoutTick) & 0x7FFFFFFF;
        return roll % 1000 < chance * 1000f;
    }

    public static float RespectChance(Pawn searcher, Pawn medic)
    {
        if (searcher.InMentalState) return 0f;
        if (searcher.story?.traits != null && searcher.story.traits.HasTrait(TraitDefOf.Bloodlust)) return 0f;
        Faction f = searcher.Faction;
        float c;
        if (f == null) c = 0.5f;
        else if (ModsConfig.RoyaltyActive && f == Faction.OfEmpire) c = 0.95f;
        else if (f.def.permanentEnemy) c = RankUtility.RankOf(medic) >= 4 ? 0.75f : 0.5f;
        else if (f.def.techLevel <= TechLevel.Neolithic) c = 0.8f;
        else c = 0.8f;
        // Длинноствол в руках медика — враги верят меньше
        ThingWithComps weapon = medic.equipment?.Primary;
        if (weapon != null && weapon.def.IsRangedWeapon && weapon.def.BaseMass > 2f) c *= 0.5f;
        return c * ParamedicMod.Settings.shoutRespectFactor;
    }

    /// <summary>Сбросить у врагов цель-медика (CAI 5000 пишет enemyTarget напрямую, поэтому периодически).</summary>
    public static void SweepTargets(Pawn medic)
    {
        if (!medic.Spawned) return;
        IReadOnlyList<Pawn> pawns = medic.Map.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < pawns.Count; i++)
        {
            Pawn p = pawns[i];
            if (p == medic || p.mindState == null || !IsProtectedFrom(medic, p)) continue;
            if (p.mindState.enemyTarget == medic) p.mindState.enemyTarget = null;
            Job job = p.CurJob;
            bool aimingAtMedic = p.stances?.curStance is Stance_Warmup w && w.focusTarg.Thing == medic;
            if (aimingAtMedic || job != null && job.targetA.Thing == medic && job.def != JobDefOf.Rescue)
            {
                p.stances?.CancelBusyStanceSoft();
                p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            }
        }
    }
}

public class HediffCompProperties_ShoutProtection : HediffCompProperties
{
    public HediffCompProperties_ShoutProtection() => compClass = typeof(HediffComp_ShoutProtection);
}

public class HediffComp_ShoutProtection : HediffComp
{
    public int shoutTick;

    public override void CompPostPostAdd(DamageInfo? dinfo)
    {
        shoutTick = Find.TickManager.TicksGame;
        ShoutUtility.Protected.Add(Pawn);
        ShoutUtility.SweepTargets(Pawn);
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        if (Pawn.IsHashIntervalTick(30)) ShoutUtility.SweepTargets(Pawn);
    }

    public override void CompPostPostRemoved() => ShoutUtility.Protected.Remove(Pawn);

    public override void CompExposeData()
    {
        Scribe_Values.Look(ref shoutTick, "shoutTick");
        if (Scribe.mode == LoadSaveMode.PostLoadInit) ShoutUtility.Protected.Add(Pawn);
    }
}

/// <summary>Способность крика: блок после вероломства, длительность из настроек.</summary>
public class CompProperties_AbilityDontShoot : CompProperties_AbilityEffect
{
    public CompProperties_AbilityDontShoot() => compClass = typeof(CompAbilityEffect_DontShoot);
}

public class CompAbilityEffect_DontShoot : CompAbilityEffect
{
    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        Pawn medic = parent.pawn;
        Hediff old = medic.health.hediffSet.GetFirstHediffOfDef(PM_DefOf.PM_ShoutProtection);
        if (old != null) medic.health.RemoveHediff(old);
        Hediff h = HediffMaker.MakeHediff(PM_DefOf.PM_ShoutProtection, medic);
        var disappears = h.TryGetComp<HediffComp_Disappears>();
        if (disappears != null) disappears.ticksToDisappear = (int)(ParamedicMod.Settings.shoutHours * GenDate.TicksPerHour);
        medic.health.AddHediff(h);
        if (medic.Spawned)
        {
            MoteMaker.ThrowText(medic.DrawPos, medic.Map, "PM_ShoutMote".Translate(), new UnityEngine.Color(0.3f, 0.9f, 0.4f), 4f);
        }
    }

    public override bool GizmoDisabled(out string reason)
    {
        var rec = GameComponent_Paramedic.Instance?.Get(parent.pawn, create: false);
        if (rec != null && rec.shoutBlockedUntil > Find.TickManager.TicksGame)
        {
            reason = "PM_ShoutBlocked".Translate((rec.shoutBlockedUntil - Find.TickManager.TicksGame).ToStringTicksToPeriod());
            return true;
        }
        return base.GizmoDisabled(out reason);
    }
}
