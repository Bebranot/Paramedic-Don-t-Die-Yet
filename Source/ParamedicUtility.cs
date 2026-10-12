using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Paramedic;

public static class ParamedicUtility
{
    private static readonly AbilityDef[] DutyAbilities = new AbilityDef[3];

    public static bool IsOnDuty(Pawn pawn) => pawn?.health?.hediffSet?.HasHediff(PM_DefOf.PM_OnDuty) == true;

    public static bool Wears(Pawn pawn, GearSlot slot)
    {
        List<Apparel> worn = pawn?.apparel?.WornApparel;
        if (worn == null) return false;
        for (int i = 0; i < worn.Count; i++)
        {
            var c = worn[i].GetComp<CompParamedicGear>();
            if (c != null && c.Props.slot == slot) return true;
        }
        return false;
    }

    /// <summary>Число слотов в полной выкладке (все значения GearSlot).</summary>
    public const int FullKitSlots = 7;

    /// <summary>Пересчёт роли: хедиффы, способности, карман, приоритет «Доктор».</summary>
    public static void Recalc(Pawn pawn)
    {
        if (pawn?.health == null || !pawn.RaceProps.Humanlike || pawn.apparel == null) return;
        if (Current.ProgramState != ProgramState.Playing) return;

        var slots = new HashSet<GearSlot>();
        float qualitySum = 0f;
        int qualityCount = 0;
        foreach (Apparel a in pawn.apparel.WornApparel)
        {
            var c = a.GetComp<CompParamedicGear>();
            if (c == null) continue;
            slots.Add(c.Props.slot);
            if (a.TryGetQuality(out QualityCategory q))
            {
                qualitySum += QualityFactor(q);
                qualityCount++;
            }
        }

        bool core = slots.Contains(GearSlot.Vest) && slots.Contains(GearSlot.Head);
        var hediffs = pawn.health.hediffSet;
        var duty = hediffs.GetFirstHediffOfDef(PM_DefOf.PM_OnDuty) as Hediff_OnDuty;
        var rec = GameComponent_Paramedic.Instance?.Get(pawn);

        if (core)
        {
            if (duty == null)
            {
                duty = (Hediff_OnDuty)HediffMaker.MakeHediff(PM_DefOf.PM_OnDuty, pawn);
                pawn.health.AddHediff(duty);
                if (ParamedicMod.Settings.autoDoctorPriority && pawn.workSettings != null && pawn.workSettings.EverWork
                    && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                {
                    pawn.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
                }
            }
            int extras = slots.Count - 2;
            duty.Severity = slots.Count >= FullKitSlots ? 3f : extras >= 3 ? 2f : 1f;
            duty.qualityFactor = qualityCount > 0 ? qualitySum / qualityCount : 1f;
            pawn.health.Notify_HediffChanged(duty);
            RankUtility.EnsureRankHediff(pawn);
        }
        else if (duty != null)
        {
            pawn.health.RemoveHediff(duty);
        }

        SyncAbilities(pawn, core, rec);
        SyncPocket(pawn, slots.Contains(GearSlot.Vest), slots.Contains(GearSlot.Belt), rec);
        SyncSteadyNerves(pawn, core && slots.Contains(GearSlot.Jacket));
    }

    public static float QualityFactor(QualityCategory q) => q switch
    {
        QualityCategory.Awful => 0.5f,
        QualityCategory.Poor => 0.75f,
        QualityCategory.Normal => 1f,
        QualityCategory.Good => 1.15f,
        QualityCategory.Excellent => 1.3f,
        QualityCategory.Masterwork => 1.4f,
        _ => 1.5f,
    };

    /// <summary>Способности есть только на смене; перезарядка переживает снятие формы.</summary>
    private static void SyncAbilities(Pawn pawn, bool onDuty, ParamedicRecord rec)
    {
        if (pawn.abilities == null || rec == null) return;
        DutyAbilities[0] = PM_DefOf.PM_Stabilize;
        DutyAbilities[1] = PM_DefOf.PM_DontShoot;
        DutyAbilities[2] = PM_DefOf.PM_AdrenalineShot;
        foreach (AbilityDef def in DutyAbilities)
        {
            Ability ab = pawn.abilities.GetAbility(def);
            if (onDuty && ab == null)
            {
                pawn.abilities.GainAbility(def);
                ab = pawn.abilities.GetAbility(def);
                if (ab != null && rec.cooldownEnd.TryGetValue(def.defName, out int end) && end > Find.TickManager.TicksGame)
                {
                    ab.cooldownEndTick = end;
                    ab.cooldownDuration = end - Find.TickManager.TicksGame;
                }
            }
            else if (!onDuty && ab != null)
            {
                rec.cooldownEnd[def.defName] = ab.cooldownEndTick;
                pawn.abilities.RemoveAbility(def);
            }
        }
    }

    /// <summary>Карман жилета: запас медикаментов в инвентаре 3, с разгрузкой 6.</summary>
    private static void SyncPocket(Pawn pawn, bool vest, bool belt, ParamedicRecord rec)
    {
        if (pawn.inventoryStock == null || rec == null) return;
        var entry = pawn.inventoryStock.GetCurrentEntryFor(InventoryStockGroupDefOf.Medicine);
        if (vest)
        {
            if (rec.prevMedicineStock < 0) rec.prevMedicineStock = entry.count;
            int want = belt ? 6 : 3;
            if (entry.count < want) entry.count = want;
            else if (!belt && entry.count > 3) entry.count = 3;
        }
        else if (rec.prevMedicineStock >= 0)
        {
            entry.count = rec.prevMedicineStock;
            rec.prevMedicineStock = -1;
        }
    }

    private static void SyncSteadyNerves(Pawn pawn, bool fromGear)
    {
        bool want = fromGear || RankUtility.RankOf(pawn) >= 3 && IsOnDuty(pawn);
        Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(PM_DefOf.PM_SteadyNerves);
        if (want && h == null) pawn.health.AddHediff(PM_DefOf.PM_SteadyNerves);
        else if (!want && h != null) pawn.health.RemoveHediff(h);
    }

    /// <summary>Человек в смысле клятвы: не гуль/шамблер/мутант, не механоид.</summary>
    public static bool CountsAsHuman(Pawn p) => p != null && p.RaceProps.Humanlike && !p.IsMutant;

    public static bool HasMedicineInInventory(Pawn pawn, out Thing medicine)
    {
        medicine = null;
        if (pawn.inventory == null) return false;
        foreach (Thing t in pawn.inventory.innerContainer)
        {
            if (t.def.IsMedicine && (medicine == null || t.def.GetStatValueAbstract(StatDefOf.MedicalPotency) < medicine.def.GetStatValueAbstract(StatDefOf.MedicalPotency)))
            {
                medicine = t; // берём самое простое лекарство — дорогое бережём для перевязки
            }
        }
        return medicine != null;
    }

    public static void ConsumeOne(Pawn pawn, Thing thing)
    {
        if (thing.stackCount > 1) thing.stackCount--;
        else
        {
            pawn.inventory.innerContainer.Remove(thing);
            thing.Destroy();
        }
    }
}
