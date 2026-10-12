using RimWorld;
using UnityEngine;
using Verse;

namespace Paramedic;

public static class RankUtility
{
    public static readonly int[] Thresholds = { 0, 10, 30, 75, 150 };

    public static int RankOf(Pawn pawn)
    {
        var rec = GameComponent_Paramedic.Instance?.Get(pawn, create: false);
        if (rec == null) return 0;
        int rank = 0;
        for (int i = 0; i < Thresholds.Length; i++)
        {
            if (rec.saved >= Thresholds[i]) rank = i;
        }
        return rank;
    }

    public static void EnsureRankHediff(Pawn pawn)
    {
        Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(PM_DefOf.PM_Rank);
        if (h == null)
        {
            h = HediffMaker.MakeHediff(PM_DefOf.PM_Rank, pawn);
            pawn.health.AddHediff(h);
        }
        float sev = RankOf(pawn) + 0.5f;
        if (!Mathf.Approximately(h.Severity, sev)) h.Severity = sev;
    }

    public static void Notify_LifeSaved(Pawn doctor)
    {
        var rec = GameComponent_Paramedic.Instance.Get(doctor);
        int before = RankOf(doctor);
        rec.saved++;
        doctor.records?.Increment(PM_DefOf.PM_LivesSaved);
        int after = RankOf(doctor);
        EnsureRankHediff(doctor);
        if (after > before && doctor.Faction == Faction.OfPlayer)
        {
            Messages.Message("PM_RankUp".Translate(doctor.Named("PAWN"), PM_DefOf.PM_Rank.stages[after].label.Named("RANK")),
                doctor, MessageTypeDefOf.PositiveEvent);
            ParamedicUtility.Recalc(doctor);
        }
    }

    /// <summary>Множитель перезарядки: комплект и звание.</summary>
    public static float CooldownFactor(Pawn pawn)
    {
        float f = ParamedicMod.Settings.cooldownFactor;
        Hediff duty = pawn.health.hediffSet.GetFirstHediffOfDef(PM_DefOf.PM_OnDuty);
        if (duty != null)
        {
            if (duty.Severity >= 3f) f *= 0.6f;
            else if (duty.Severity >= 2f) f *= 0.8f;
        }
        if (RankOf(pawn) >= 2) f *= 0.9f;
        return f;
    }
}

public static class OathUtility
{
    private const int CrisisWindow = GenDate.TicksPerDay * 15;

    /// <summary>Парамедик убил (maim=false) или искалечил (maim=true) человека своими руками.</summary>
    public static void Notify_Violation(Pawn medic, Pawn victim, bool maim)
    {
        if (medic == null || medic == victim || medic.needs?.mood == null) return;
        if (!ParamedicUtility.CountsAsHuman(victim)) return;
        var rec = GameComponent_Paramedic.Instance?.Get(medic, create: false);
        bool onDuty = ParamedicUtility.IsOnDuty(medic);
        if (!onDuty && (rec == null || !rec.OathTaken)) return;
        rec ??= GameComponent_Paramedic.Instance.Get(medic);

        int stage = RankUtility.RankOf(medic) * 2 + (maim ? 1 : 0);
        ThoughtDef def = onDuty ? PM_DefOf.PM_OathBroken : PM_DefOf.PM_OathWeight;
        GainWorst(medic, def, stage);

        int now = Find.TickManager.TicksGame;
        rec.violationTicks.Add(now);
        rec.violationTicks.RemoveAll(t => now - t > CrisisWindow);
        if (ParamedicMod.Settings.crisisEnabled && rec.violationTicks.Count >= 3 && onDuty)
        {
            rec.violationTicks.Clear();
            StartCrisis(medic);
        }
    }

    /// <summary>Новая мысль клятвы заменяет старую, если она не слабее (одна на пешку).</summary>
    private static void GainWorst(Pawn medic, ThoughtDef def, int stage)
    {
        var mem = medic.needs.mood.thoughts.memories;
        var fresh = (Thought_Memory)ThoughtMaker.MakeThought(def, stage);
        float freshMood = fresh.CurStage.baseMoodEffect;
        foreach (ThoughtDef d in new[] { PM_DefOf.PM_OathBroken, PM_DefOf.PM_OathWeight })
        {
            Thought_Memory old = mem.GetFirstMemoryOfDef(d);
            if (old == null) continue;
            if (old.CurStage.baseMoodEffect < freshMood)
            {
                old.Renew(); // старая тяжелее — только продлеваем
                return;
            }
            mem.RemoveMemory(old);
        }
        mem.TryGainMemory(fresh);
    }

    public static void StartCrisis(Pawn medic)
    {
        medic.health.AddHediff(PM_DefOf.PM_CrisisOfFaith);
        if (medic.apparel != null)
        {
            foreach (Apparel a in medic.apparel.WornApparel.ToArray())
            {
                var c = a.GetComp<CompParamedicGear>();
                if (c != null && c.Props.IsCore && medic.Spawned)
                {
                    medic.apparel.TryDrop(a);
                }
            }
        }
        if (medic.Faction == Faction.OfPlayer)
        {
            Find.LetterStack.ReceiveLetter("PM_CrisisLabel".Translate(), "PM_CrisisText".Translate(medic.Named("PAWN")),
                LetterDefOf.NegativeEvent, medic);
        }
    }

    /// <summary>Атака под защитой крика — вероломство.</summary>
    public static void Notify_Perfidy(Pawn medic, Thing target)
    {
        var rec = GameComponent_Paramedic.Instance.Get(medic);
        rec.shoutBlockedUntil = Find.TickManager.TicksGame + GenDate.TicksPerDay * 15;
        medic.needs?.mood?.thoughts.memories.TryGainMemory(PM_DefOf.PM_Perfidy);
        Faction f = (target as Pawn)?.Faction ?? target?.Faction;
        if (f != null && f != Faction.OfPlayer && !f.def.permanentEnemy && medic.Faction == Faction.OfPlayer)
        {
            f.TryAffectGoodwillWith(Faction.OfPlayer, -15, reason: PM_DefOf.PM_PerfidyEvent);
        }
        if (medic.Faction == Faction.OfPlayer)
        {
            Messages.Message("PM_PerfidyMsg".Translate(medic.Named("PAWN")), medic, MessageTypeDefOf.NegativeEvent);
        }
    }

    public static void Notify_PatientLost(Pawn doctor)
    {
        doctor.needs?.mood?.thoughts.memories.TryGainMemory(PM_DefOf.PM_NotSaved);
        if (!ParamedicMod.Settings.burnoutEnabled) return;
        var rec = GameComponent_Paramedic.Instance.Get(doctor);
        int now = Find.TickManager.TicksGame;
        rec.patientDeathTicks.Add(now);
        rec.patientDeathTicks.RemoveAll(t => now - t > GenDate.TicksPerDay * 10);
        if (rec.patientDeathTicks.Count >= 5)
        {
            doctor.needs?.mood?.thoughts.memories.TryGainMemory(PM_DefOf.PM_Burnout);
            if (RankUtility.RankOf(doctor) >= 3 && Rand.Chance(0.1f) && doctor.Spawned && !doctor.InMentalState)
            {
                doctor.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_Sad, "PM_BurnoutBreak".Translate());
            }
        }
    }
}

/// <summary>Мысль клятвы: сила регулируется настройкой.</summary>
public class Thought_PMOath : Thought_Memory
{
    public override float MoodOffset() => base.MoodOffset() * ParamedicMod.Settings.oathStrength;
}
