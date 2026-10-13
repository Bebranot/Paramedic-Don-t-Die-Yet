using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Paramedic;

// ===================== Клятва: убийство =====================

[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class Pawn_Kill_Patch
{
    public static void Prefix(Pawn __instance, out bool __state) => __state = !__instance.Dead;

    public static void Postfix(Pawn __instance, DamageInfo? dinfo, bool __state)
    {
        if (!__state || !__instance.Dead || GameComponent_Paramedic.Instance == null) return;
        GameComponent_Paramedic.Instance.Notify_PatientDied(__instance);
        if (!ParamedicUtility.CountsAsHuman(__instance)) return;

        Pawn killer = dinfo?.Instigator as Pawn;

        // CE: попробуем получить виновника из CE-снаряда (instigator/launcher)
        if (killer == null && CompatCE.Active && dinfo.HasValue)
        {
            Thing instigatorThing = dinfo.Value.Instigator;
            if (instigatorThing != null)
            {
                killer = CompatCE.GetCEInstigator(instigatorThing) ?? CompatCE.GetCELauncher(instigatorThing);
            }
        }

        if (killer == null && GameComponent_Paramedic.Instance.lastHarm.TryGetValue(__instance, out var harm)
            && Find.TickManager.TicksGame - harm.tick < GenDate.TicksPerHour * 2)
        {
            killer = harm.attacker; // умер от кровопотери/ран, нанесённых парамедиком
        }
        GameComponent_Paramedic.Instance.lastHarm.Remove(__instance);
        if (killer != null && killer != __instance)
        {
            OathUtility.Notify_Violation(killer, __instance, maim: false);
        }
    }
}

// ===================== Клятва: увечье =====================

[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
public static class Thing_TakeDamage_Patch
{
    public static void Prefix(Thing __instance, DamageInfo dinfo, out int __state)
    {
        __state = -1;
        Pawn attacker = dinfo.Instigator as Pawn;

        // CE: если инстигатор не Pawn, попробуем достать из CE-снаряда
        if (attacker == null && CompatCE.Active)
        {
            attacker = CompatCE.GetCEInstigator(dinfo.Instigator) ?? CompatCE.GetCELauncher(dinfo.Instigator);
        }

        if (__instance is Pawn victim && attacker != null && attacker != victim
            && attacker.RaceProps.Humanlike && ParamedicUtility.CountsAsHuman(victim) && IsSworn(attacker))
        {
            __state = victim.health.hediffSet.GetMissingPartsCommonAncestors().Count;
        }
    }

    /// <summary>Клятва касается пешки: на смене или уже давала клятву.</summary>
    private static bool IsSworn(Pawn p) =>
        ParamedicUtility.IsOnDuty(p) || GameComponent_Paramedic.Instance?.Get(p, create: false)?.OathTaken == true;

    public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result, int __state)
    {
        if (__state < 0 || __instance is not Pawn victim) return;
        if (__result == null || __result.totalDamageDealt <= 0f) return;
        var comp = GameComponent_Paramedic.Instance;
        if (comp == null) return;

        Pawn attacker = dinfo.Instigator as Pawn;

        // CE: если инстигатор не Pawn, попробуем достать из CE-снаряда
        if (attacker == null && CompatCE.Active)
        {
            attacker = CompatCE.GetCEInstigator(dinfo.Instigator) ?? CompatCE.GetCELauncher(dinfo.Instigator);
        }

        if (attacker == null) return;

        comp.lastHarm[victim] = (attacker, Find.TickManager.TicksGame);
        if (victim.Dead) return; // смерть посчитает Pawn.Kill

        bool maimed = victim.health.hediffSet.GetMissingPartsCommonAncestors().Count > __state;
        if (!maimed && __result.hediffs != null)
        {
            foreach (Hediff h in __result.hediffs)
            {
                var perm = h.TryGetComp<HediffComp_GetsPermanent>();
                if (perm != null && (perm.IsPermanent || perm.permanentDamageThreshold < 9999f))
                {
                    maimed = true;
                    break;
                }
            }
        }
        if (maimed) OathUtility.Notify_Violation(attacker, victim, maim: true);
    }
}

// ===================== Клятва: изъятие здоровых органов =====================

[HarmonyPatch(typeof(Recipe_RemoveBodyPart), nameof(Recipe_RemoveBodyPart.ApplyThoughts))]
public static class Recipe_RemoveBodyPart_ApplyThoughts_Patch
{
    public static void Postfix(Pawn pawn, Pawn billDoer)
    {
        if (billDoer != null && !pawn.Dead) OathUtility.Notify_Violation(billDoer, pawn, maim: true);
        // при смерти — нарушение засчитает Pawn.Kill только при инстигаторе; здесь — явно
        else if (billDoer != null) OathUtility.Notify_Violation(billDoer, pawn, maim: false);
    }
}

// ===================== Запрет казни и вредных операций на смене =====================

[HarmonyPatch(typeof(WorkGiver_Warden_DoExecution), nameof(WorkGiver_Warden_DoExecution.JobOnThing))]
public static class WorkGiver_Warden_DoExecution_Patch
{
    public static void Postfix(Pawn pawn, ref Job __result)
    {
        if (__result != null && ParamedicUtility.IsOnDuty(pawn))
        {
            JobFailReason.Is("PM_OathForbids".Translate());
            __result = null;
        }
    }
}

[HarmonyPatch(typeof(Bill_Medical), nameof(Bill_Medical.PawnAllowedToStartAnew))]
public static class Bill_Medical_PawnAllowed_Patch
{
    public static void Postfix(Bill_Medical __instance, Pawn pawn, ref bool __result)
    {
        if (!__result || !ParamedicUtility.IsOnDuty(pawn)) return;
        Pawn patient = __instance.GiverPawn;
        if (patient == null || !ParamedicUtility.CountsAsHuman(patient)) return;
        RecipeDef r = __instance.recipe;
        bool harmful = r.workerClass == typeof(Recipe_ExecuteByCut)
            || typeof(Recipe_RemoveBodyPart).IsAssignableFrom(r.workerClass) && __instance.Part != null
               && MedicalRecipesUtility.IsClean(patient, __instance.Part)
               && !patient.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(__instance.Part);
        if (harmful)
        {
            JobFailReason.Is("PM_OathForbids".Translate());
            __result = false;
        }
    }
}

// ===================== Крик «Не стрелять!» =====================

[HarmonyPatch(typeof(Pawn), nameof(Pawn.ThreatDisabled))]
public static class Pawn_ThreatDisabled_Patch
{
    public static void Postfix(Pawn __instance, IAttackTargetSearcher disabledFor, ref bool __result)
    {
        if (__result || ShoutUtility.Protected.Count == 0 || disabledFor == null) return;
        if (ShoutUtility.IsProtectedFrom(__instance, disabledFor.Thing)) __result = true;
    }
}

[HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool))]
public static class Verb_TryStartCastOn_Patch
{
    /// <summary>
    /// Под CE Verb_ShootCE может переопределять TryStartCastOn.
    /// Prepare: без CE патч не нужен (возвращаем false, Harmony пропускает).
    /// Priority: ниже дефолтного, чтобы CE-патчи (если есть) сработали первыми.
    /// </summary>
    static bool Prepare() => true; // работаем всегда; CE-совместимость внутри Prefix

    static int Priority => CompatCE.Active ? -100 : 0;

    public static bool Prefix(Verb __instance, LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack, bool canHitNonTargetPawns, bool preventFriendlyFire, bool nonInterrupting)
    {
        if (ShoutUtility.Protected.Count == 0 || __instance is Verb_CastAbility) return true;
        Pawn caster = __instance.CasterPawn;
        if (caster == null) return true;

        // CE: если пешка подавлена (CompSuppressable), она и так не стреляет — не вмешиваемся.
        if (CompatCE.Active && CompatCE.IsSuppressed(caster)) return true;

        if (castTarg.Thing == null)
        {
            // огонь по клетке (подавление у CAI 5000 / CE suppression fire): не по позиции защищённого медика
            if (castTarg.IsValid && ShoutUtility.ProtectedNear(castTarg.Cell, caster, 1.9f))
            {
                if (SelfTestComponent.Enabled) SelfTestComponent.BlockedShots++;
                return false;
            }
            return true;
        }

        // Медик под защитой: автоатаки (самооборона, огонь с места) подавляются,
        // а атака по приказу игрока снимает защиту — это вероломство
        if (ShoutUtility.Protected.Contains(caster) && castTarg.Thing != caster
            && (castTarg.Thing is Pawn { RaceProps.Humanlike: true } || castTarg.Thing.HostileTo(caster)))
        {
            if (caster.CurJob == null || !caster.CurJob.playerForced) return false;
            Hediff h = caster.health.hediffSet.GetFirstHediffOfDef(PM_DefOf.PM_ShoutProtection);
            if (h != null) caster.health.RemoveHediff(h);
            if (SelfTestComponent.Enabled)
            {
                SelfTestComponent.Events.Add($"perfidy: {caster.LabelShort} {__instance.GetType().Name} -> {castTarg.Thing.LabelShort}, job={caster.CurJobDef?.defName}");
            }
            OathUtility.Notify_Perfidy(caster, castTarg.Thing);
            return true;
        }

        if (SelfTestComponent.Enabled && caster.HostileTo(Faction.OfPlayer)) SelfTestComponent.HostileShots++;

        // Страховка: враг не начинает выстрел по защищённому медику
        if (castTarg.Thing is Pawn target && ShoutUtility.IsProtectedFrom(target, caster))
        {
            if (SelfTestComponent.Enabled) SelfTestComponent.BlockedShots++;
            return false;
        }
        return true;
    }
}

// ===================== Кризис веры: нельзя надеть ядро формы =====================

[HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip),
    new[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) },
    new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
public static class EquipmentUtility_CanEquip_Patch
{
    public static void Postfix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
    {
        if (!__result || pawn?.health == null) return;
        var c = thing.TryGetComp<CompParamedicGear>();
        if (c == null || !c.Props.IsCore) return;
        if (pawn.health.hediffSet.HasHediff(PM_DefOf.PM_CrisisOfFaith))
        {
            cantReason = "PM_CrisisCantWear".Translate();
            __result = false;
        }
        else if (!pawn.DevelopmentalStage.Adult())
        {
            cantReason = "PM_AdultsOnly".Translate();
            __result = false;
        }
    }
}

// ===================== Лечение: спасённые, «меня лечил парамедик», гуманитарка =====================

[HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
public static class TendPatches
{
    public static void Postfix(Pawn doctor, Pawn patient)
    {
        if (doctor != null && doctor != patient && ParamedicUtility.IsOnDuty(doctor)) Notify_ParamedicHelped(doctor, patient);
    }

    public static void Notify_ParamedicHelped(Pawn medic, Pawn patient)
    {
        var comp = GameComponent_Paramedic.Instance;
        if (comp == null || patient == null || !patient.RaceProps.Humanlike) return;
        if (patient.Downed || patient.health.hediffSet.BleedRateTotal > 0.01f) comp.AddPending(patient, medic);
        patient.needs?.mood?.thoughts.memories.TryGainMemory(PM_DefOf.PM_TreatedByParamedic, medic);
        if (ParamedicMod.Settings.humanitarianEnabled && patient.Faction != null && patient.Faction != Faction.OfPlayer
            && medic.Faction == Faction.OfPlayer && !patient.Faction.def.permanentEnemy)
        {
            comp.MarkHumanitarian(patient);
        }
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.ExitMap))]
public static class Pawn_ExitMap_Patch
{
    public static void Prefix(Pawn __instance)
    {
        var comp = GameComponent_Paramedic.Instance;
        if (comp == null || __instance.Dead || !comp.TakeHumanitarian(__instance)) return;
        Faction f = __instance.Faction;
        if (f == null || f == Faction.OfPlayer || f.def.permanentEnemy || !comp.HumanitarianAllowed(f)) return;
        f.TryAffectGoodwillWith(Faction.OfPlayer, 5, reason: PM_DefOf.PM_HumanitarianAid);
    }
}

// ===================== Сортировка: время до смерти в меню «Спасти» =====================

[HarmonyPatch(typeof(FloatMenuOptionProvider_RescuePawn), "GetSingleOptionFor")]
public static class RescueOption_Triage_Patch
{
    public static void Postfix(Pawn clickedPawn, FloatMenuContext context, FloatMenuOption __result)
    {
        if (__result == null || __result.Disabled || !ParamedicUtility.IsOnDuty(context.FirstSelectedPawn)) return;
        if (clickedPawn.health.hediffSet.BleedRateTotal <= 0.01f) return;
        int ticks = HealthUtility.TicksUntilDeathDueToBloodLoss(clickedPawn);
        if (ticks < GenDate.TicksPerDay)
        {
            __result.Label += " " + "PM_Triage".Translate(ticks.ToStringTicksToPeriod(allowSeconds: false, shortForm: true));
        }
    }
}

// ===================== Перезарядки с учётом комплекта и звания =====================

[HarmonyPatch(typeof(Ability), nameof(Ability.StartCooldown))]
public static class Ability_StartCooldown_Patch
{
    private static readonly HashSet<AbilityDef> Ours = new();

    public static void Prefix(Ability __instance, ref int ticks)
    {
        if (Ours.Count == 0)
        {
            Ours.Add(PM_DefOf.PM_Stabilize);
            Ours.Add(PM_DefOf.PM_DontShoot);
            Ours.Add(PM_DefOf.PM_AdrenalineShot);
        }
        if (__instance.pawn != null && Ours.Contains(__instance.def))
        {
            ticks = (int)(ticks * RankUtility.CooldownFactor(__instance.pawn));
        }
    }
}
