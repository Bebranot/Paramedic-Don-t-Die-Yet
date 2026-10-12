using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Paramedic;

/// <summary>
/// Автотест без участия человека: запуск игры с <c>-quicktest -pmselftest</c>.
/// Проверяет роль, клятву, крик, способности; отчёт — ParamedicSelfTest.txt рядом с Player.log, затем выход.
/// </summary>
/// <summary>Диагностика автотеста: в кого летела пуля, попавшая в защищённого медика.</summary>
[HarmonyLib.HarmonyPatch(typeof(Bullet), "Impact")]
public static class SelfTest_BulletImpact_Patch
{
    public static bool Prepare() => SelfTestComponent.Enabled;

    public static void Prefix(Bullet __instance, Thing hitThing)
    {
        if (hitThing is Pawn p && ShoutUtility.Protected.Contains(p))
        {
            float respect = __instance.Launcher is Pawn lp ? ShoutUtility.RespectChance(lp, p) : -1f;
            bool mustRespect = __instance.Launcher is Pawn lp2 && ShoutUtility.IsProtectedFrom(p, lp2);
            if (__instance.intendedTarget.Thing != p) SelfTestComponent.StrayHits++;
            else if (mustRespect) SelfTestComponent.AimedHits++;
            else SelfTestComponent.AllowedHits++;
            SelfTestComponent.Events.Add($"  respect={respect:0.##} mustRespect={mustRespect} mental={(__instance.Launcher as Pawn)?.MentalStateDef?.defName}");
            SelfTestComponent.Events.Add($"bullet hit medic: launcher={__instance.Launcher?.LabelShort}, intended={__instance.intendedTarget.Thing?.LabelShort ?? __instance.intendedTarget.Cell.ToString()}, usedTarget={__instance.usedTarget.Thing?.LabelShort ?? __instance.usedTarget.Cell.ToString()}");
        }
    }
}

public class SelfTestComponent : GameComponent
{
    private const int LiveFireTicks = 3000;

    private int startTick = -1;
    private int phase;              // 0 — ждём карту, 1 — бой идёт, 2 — готово
    private int liveFireStart;
    private Pawn liveMedic;
    private List<Pawn> raiders;
    private int medicInjuriesBefore;
    private int lostProtectionTick = -1;
    public static readonly List<string> Events = new();
    private readonly StringBuilder sb = new();
    private int fails;

    /// <summary>Счётчики из патча Verb.TryStartCastOn (только в автотесте).</summary>
    public static int HostileShots, BlockedShots, AimedHits, StrayHits, AllowedHits;

    public SelfTestComponent(Game game) { }

    public static readonly bool Enabled = GenCommandLine.CommandLineArgPassed("pmselftest");

    private void Check(string name, bool ok, string extra = "")
    {
        if (!ok) fails++;
        sb.AppendLine((ok ? "OK   " : "FAIL ") + name + (extra.NullOrEmpty() ? "" : "  [" + extra + "]"));
    }

    public override void GameComponentTick()
    {
        if (phase == 2 || !Enabled || Find.CurrentMap == null) return;
        int now = Find.TickManager.TicksGame;
        if (startTick < 0) { startTick = now; return; }
        if (phase == 0)
        {
            if (now - startTick < 120) return;
            try
            {
                Run(Check, sb);
                StartLiveFire();
                phase = 1;
            }
            catch (Exception e)
            {
                fails++;
                sb.AppendLine("EXCEPTION " + e);
                Finish();
            }
            return;
        }
        if (lostProtectionTick < 0 && !ShoutUtility.Protected.Contains(liveMedic))
        {
            lostProtectionTick = now - liveFireStart;
            sb.AppendLine($"protection lost at +{lostProtectionTick}t; medic job={liveMedic.CurJobDef?.defName}, drafted={liveMedic.Drafted}, downed={liveMedic.Downed}");
        }
        if (now - liveFireStart < LiveFireTicks) return;
        try
        {
            EndLiveFire();
        }
        catch (Exception e)
        {
            fails++;
            sb.AppendLine("EXCEPTION " + e);
        }
        Finish();
    }

    private void Finish()
    {
        phase = 2;
        ParamedicMod.Settings.shoutRespectFactor = 1f;
        sb.Insert(0, $"Paramedic self-test: {(fails == 0 ? "PASS" : fails + " FAIL")}\n" +
            $"mods: {string.Join(", ", ModsConfig.ActiveModsInLoadOrder.Select(m => m.PackageId))}\n\n");
        // сейв для проверки загрузки: RimWorldWin64.exe -dutyloadsave=ParamedicSelfTest (инструмент DutyOptim)
        try
        {
            GameDataSaveLoader.SaveGame("ParamedicSelfTest");
            sb.AppendLine("\nsaved: ParamedicSelfTest");
        }
        catch (Exception e)
        {
            sb.AppendLine("\nSAVE FAILED " + e.Message);
        }
        string path = Path.Combine(GenFilePaths.SaveDataFolderPath, "ParamedicSelfTest.txt");
        File.WriteAllText(path, sb.ToString());
        Log.Message("[Paramedic] self-test written to " + path);
        if (!GenCommandLine.CommandLineArgPassed("pmstay")) Application.Quit();
    }

    private static int InjuryCount(Pawn p) => p.health.hediffSet.hediffs.Count(h => h is Hediff_Injury);

    /// <summary>Бой: безоружный медик под защитой креста посреди штурма вооружённых пиратов.</summary>
    private void StartLiveFire()
    {
        Map map = Find.CurrentMap;
        sb.AppendLine("\n---- live fire");
        liveMedic = map.mapPawns.FreeColonistsSpawned.Where(p => p.DevelopmentalStage.Adult() && !p.Downed
            && !p.health.hediffSet.HasHediff(PM_DefOf.PM_CrisisOfFaith)).RandomElement();
        liveMedic.apparel.Wear(Make("PM_Apparel_Vest"), dropReplacedApparel: false);
        liveMedic.apparel.Wear(Make("PM_Apparel_Beret"), dropReplacedApparel: false);
        liveMedic.equipment?.DestroyAllEquipment();
        liveMedic.health.AddHediff(PM_DefOf.PM_ShoutProtection);
        Check("live medic on duty + protected", ParamedicUtility.IsOnDuty(liveMedic) && ShoutUtility.Protected.Contains(liveMedic), liveMedic.LabelShort);

        Faction pirates = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Pirate) ?? Find.FactionManager.RandomEnemyFaction();
        PawnKindDef gunner = DefDatabase<PawnKindDef>.GetNamedSilentFail("Mercenary_Gunner") ?? PawnKindDefOf.Drifter;
        raiders = new List<Pawn>();
        IntVec3 at = CellFinder.RandomClosewalkCellNear(liveMedic.Position, map, 14);
        for (int i = 0; i < 3; i++)
        {
            Pawn r = SpawnPawn(gunner, pirates, at, map);
            // детерминированный бой: без кровожадных (им крик не указ) и без взрывчатки (взрыв не выбирает цель)
            Trait blood = r.story?.traits?.GetTrait(TraitDefOf.Bloodlust);
            if (blood != null) r.story.traits.RemoveTrait(blood);
            r.equipment.DestroyAllEquipment();
            r.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle")));
            raiders.Add(r);
        }
        Events.Clear();
        Verse.AI.Group.LordMaker.MakeNewLord(pirates, new LordJob_AssaultColony(pirates, canKidnap: false, canTimeoutOrFlee: false), map, raiders);
        medicInjuriesBefore = InjuryCount(liveMedic);
        HostileShots = BlockedShots = AimedHits = StrayHits = AllowedHits = 0;
        liveFireStart = Find.TickManager.TicksGame;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
    }

    private void EndLiveFire()
    {
        int injuries = InjuryCount(liveMedic) - medicInjuriesBefore;
        foreach (var h in liveMedic.health.hediffSet.hediffs.OfType<Hediff_Injury>().Skip(medicInjuriesBefore).ToList())
        {
            sb.AppendLine($"  injury: {h.def.defName} on {h.Part?.Label} source={h.sourceDef?.defName ?? h.sourceLabel ?? "?"}");
        }
        foreach (string e in Events) sb.AppendLine("  event: " + e);
        sb.AppendLine($"raiders: {string.Join(", ", raiders.Select(r => r.LabelShort + (r.Dead ? " (dead)" : r.Downed ? " (down)" : "") + " w=" + (r.equipment?.Primary?.def.defName ?? "none")))}");
        sb.AppendLine($"hostile casts: {HostileShots}, blocked at medic: {BlockedShots}");
        Check("raiders actually fought", HostileShots > 0, "casts=" + HostileShots);
        sb.AppendLine($"hits on medic: aimed by respecting {AimedHits}, aimed by non-respecting {AllowedHits}, stray {StrayHits}");
        Check("no aimed hits on medic", AimedHits == 0, "aimed=" + AimedHits + ", injuries=" + injuries);
        Check("no perfidy during fight", !Events.Any(e => e.StartsWith("perfidy")), "");
    }


    private static Apparel Make(string def)
    {
        var a = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(def));
        a.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal, ArtGenerationContext.Outsider);
        return a;
    }

    private static Pawn SpawnPawn(PawnKindDef kind, Faction faction, IntVec3 near, Map map)
    {
        Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true,
            mustBeCapableOfViolence: true, developmentalStages: DevelopmentalStage.Adult));
        GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(near, map, 6), map);
        return p;
    }

    private static int OathStage(Pawn p, ThoughtDef def)
    {
        var m = p.needs.mood.thoughts.memories.GetFirstMemoryOfDef(def);
        return m == null ? -1 : m.CurStageIndex;
    }

    private static void Run(Action<string, bool, string> check, StringBuilder sb)
    {
        Map map = Find.CurrentMap;
        Pawn medic = map.mapPawns.FreeColonistsSpawned.First(p => p.DevelopmentalStage.Adult());
        sb.AppendLine("medic: " + medic.LabelShort + "\n");
        ParamedicMod.Settings.shoutRespectFactor = 10f; // в тесте враги всегда уважают крик

        // Сырые (до финализации) значения: в них уже есть множители хедиффов, и их не кэширует сборка
        float Raw(StatDef s) => s.Worker.GetValueUnfinalized(StatRequest.For(medic));
        float tendBefore = Raw(StatDefOf.MedicalTendQuality), shootBefore = Raw(StatDefOf.ShootingAccuracyPawn);

        // ---- 1. Ядро формы
        medic.apparel.Wear(Make("PM_Apparel_Vest"), dropReplacedApparel: false);
        check("vest only: not on duty", !ParamedicUtility.IsOnDuty(medic), "");
        medic.apparel.Wear(Make("PM_Apparel_Beret"), dropReplacedApparel: false);
        check("vest+beret: on duty", ParamedicUtility.IsOnDuty(medic), "");
        Hediff duty = medic.health.hediffSet.GetFirstHediffOfDef(PM_DefOf.PM_OnDuty);
        check("stage basic", duty != null && duty.CurStageIndex == 0, "sev=" + duty?.Severity);
        check("abilities granted", medic.abilities.GetAbility(PM_DefOf.PM_Stabilize) != null
            && medic.abilities.GetAbility(PM_DefOf.PM_DontShoot) != null && medic.abilities.GetAbility(PM_DefOf.PM_AdrenalineShot) != null, "");
        int stock = medic.inventoryStock.GetDesiredCountForGroup(InventoryStockGroupDefOf.Medicine);
        check("medicine pocket = 3", stock == 3, "stock=" + stock);
        check("rank hediff", medic.health.hediffSet.HasHediff(PM_DefOf.PM_Rank), "");
        float tendAfter = Raw(StatDefOf.MedicalTendQuality), shootAfter = Raw(StatDefOf.ShootingAccuracyPawn);
        check("tend quality x1.15", tendAfter > tendBefore * 1.12f, $"{tendBefore:0.000}->{tendAfter:0.000}");
        check("shooting -4", shootAfter < shootBefore - 3.5f, $"{shootBefore:0.0}->{shootAfter:0.0}");
        var st = duty?.CurStage;
        check("stage modifiers", st != null && st.statFactors.Any(m => m.stat == StatDefOf.MedicalTendQuality && m.value > 1.1f)
            && st.statOffsets.Any(m => m.stat == StatDefOf.ShootingAccuracyPawn && m.value < 0f), "");

        // ---- 2. Полная выкладка
        foreach (string d in new[] { "PM_Apparel_Shirt", "PM_Apparel_Pants", "PM_Apparel_Jacket", "PM_Apparel_Goggles", "PM_Apparel_Belt" })
        {
            medic.apparel.Wear(Make(d), dropReplacedApparel: false);
        }
        duty = medic.health.hediffSet.GetFirstHediffOfDef(PM_DefOf.PM_OnDuty);
        check("full kit stage", duty != null && duty.CurStageIndex == 2, "sev=" + duty?.Severity + " worn=" + medic.apparel.WornApparelCount);
        stock = medic.inventoryStock.GetDesiredCountForGroup(InventoryStockGroupDefOf.Medicine);
        check("medicine pocket with rig = 6", stock == 6, "stock=" + stock);
        check("steady nerves (jacket)", medic.health.hediffSet.HasHediff(PM_DefOf.PM_SteadyNerves), "");
        check("cooldown factor full kit", Mathf.Approximately(RankUtility.CooldownFactor(medic), 0.6f), RankUtility.CooldownFactor(medic).ToString());

        // ---- 3. Стабилизация
        Pawn patient = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p != medic) ?? SpawnPawn(PawnKindDefOf.Colonist, Faction.OfPlayer, medic.Position, map);
        for (int i = 0; i < 6 && !patient.Downed; i++)
        {
            patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 8f, 0f, -1f, null, patient.health.hediffSet.GetNotMissingParts().Where(x => x.depth == BodyPartDepth.Outside && x.coverageAbs > 0.01f).RandomElement()));
        }
        if (!patient.Downed) patient.health.AddHediff(HediffDefOf.Anesthetic);
        check("patient downed", patient.Downed, "bleed=" + patient.health.hediffSet.BleedRateTotal.ToString("0.00"));
        Thing med = ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial);
        med.stackCount = 2;
        medic.inventory.innerContainer.TryAdd(med);
        var stab = medic.abilities.GetAbility(PM_DefOf.PM_Stabilize).EffectComps.OfType<CompAbilityEffect_Stabilize>().First();
        check("stabilize valid", stab.Valid((LocalTargetInfo)patient), "");
        stab.Apply(patient, patient);
        check("bandage applied", patient.health.hediffSet.HasHediff(PM_DefOf.PM_PressureBandage), "");
        check("bleeding stopped", patient.health.hediffSet.BleedRateTotal < 0.001f, patient.health.hediffSet.BleedRateTotal.ToString("0.000"));
        check("medicine consumed", medic.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.MedicineIndustrial) == 1, "");
        check("patient thought", patient.needs?.mood?.thoughts.memories.GetFirstMemoryOfDef(PM_DefOf.PM_TreatedByParamedic) != null, "");

        // ---- 4. Адреналин
        medic.inventory.innerContainer.TryAdd(ThingMaker.MakeThing(PM_DefOf.PM_AdrenalineSyringe));
        var adr = medic.abilities.GetAbility(PM_DefOf.PM_AdrenalineShot).EffectComps.OfType<CompAbilityEffect_Adrenaline>().First();
        check("adrenaline not disabled", !adr.GizmoDisabled(out string why), why ?? "");
        adr.Apply(patient, patient);
        check("adrenaline hediff", patient.health.hediffSet.HasHediff(PM_DefOf.PM_Adrenaline), "");
        check("adrenaline heart cooldown", !adr.Valid((LocalTargetInfo)patient), "");

        // ---- 5. Крик
        Faction pirates = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Pirate) ?? Find.FactionManager.RandomEnemyFaction();
        Pawn enemy = SpawnPawn(PawnKindDefOf.Drifter, pirates, medic.Position, map);
        check("enemy hostile", enemy.HostileTo(medic), enemy.Faction?.Name ?? "no faction");
        check("before shout: targetable", !medic.ThreatDisabled(enemy), "");
        var shout = medic.abilities.GetAbility(PM_DefOf.PM_DontShoot).EffectComps.OfType<CompAbilityEffect_DontShoot>().First();
        shout.Apply(medic, medic);
        check("shout hediff", medic.health.hediffSet.HasHediff(PM_DefOf.PM_ShoutProtection), "");
        check("after shout: threat disabled", medic.ThreatDisabled(enemy), "");
        var mech = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.RaceProps.IsMechanoid && p.HostileTo(medic));
        if (mech != null) check("mech ignores shout", !medic.ThreatDisabled(mech), "");
        enemy.mindState.enemyTarget = medic;
        ShoutUtility.SweepTargets(medic);
        check("sweep cleared enemy target", enemy.mindState.enemyTarget != medic, "");

        // ---- 6. Вероломство: атака под защитой
        Verb verb = medic.equipment?.PrimaryEq?.PrimaryVerb ?? medic.meleeVerbs?.TryGetMeleeVerb(enemy);
        if (verb != null)
        {
            // автоатака (без приказа) подавляется и защиту не снимает
            verb.TryStartCastOn(enemy);
            check("auto-attack suppressed, still protected", medic.health.hediffSet.HasHediff(PM_DefOf.PM_ShoutProtection), verb.GetType().Name);
            // атака по приказу игрока — вероломство
            medic.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.AttackMelee, enemy));
            verb.TryStartCastOn(enemy);
            check("ordered attack breaks shout", !medic.health.hediffSet.HasHediff(PM_DefOf.PM_ShoutProtection), verb.GetType().Name);
            check("perfidy thought", OathStage(medic, PM_DefOf.PM_Perfidy) >= 0, "");
            check("shout blocked", shout.GizmoDisabled(out _), "");
            medic.stances.CancelBusyStanceHard();
            medic.jobs.StopAll();
        }
        else sb.AppendLine("SKIP perfidy: no verb");

        // ---- 7. Клятва: увечье, затем убийство
        Pawn victim1 = SpawnPawn(PawnKindDefOf.Drifter, pirates, medic.Position, map);
        BodyPartRecord hand = victim1.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def == BodyPartDefOf.Hand);
        victim1.TakeDamage(new DamageInfo(DamageDefOf.Cut, 999f, 0f, -1f, medic, hand));
        check("maim: victim alive", !victim1.Dead, "");
        check("maim: oath broken (stage 1)", OathStage(medic, PM_DefOf.PM_OathBroken) == 1, "stage=" + OathStage(medic, PM_DefOf.PM_OathBroken));

        Pawn victim2 = SpawnPawn(PawnKindDefOf.Drifter, pirates, medic.Position, map);
        victim2.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 999f, 0f, -1f, medic, victim2.health.hediffSet.GetBrain()));
        check("kill: victim dead", victim2.Dead, "");
        check("kill: oath broken (stage 0)", OathStage(medic, PM_DefOf.PM_OathBroken) == 0, "stage=" + OathStage(medic, PM_DefOf.PM_OathBroken));

        // Не человек — не нарушение
        Pawn animal = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.RaceProps.Animal);
        if (animal != null)
        {
            int before = GameComponent_Paramedic.Instance.Get(medic).violationTicks.Count;
            animal.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 999f, 0f, -1f, medic, animal.health.hediffSet.GetBrain()));
            check("animal kill: no violation", GameComponent_Paramedic.Instance.Get(medic).violationTicks.Count == before, "");
        }

        // ---- 8. Кризис веры: третье нарушение
        Pawn victim3 = SpawnPawn(PawnKindDefOf.Drifter, pirates, medic.Position, map);
        victim3.TakeDamage(new DamageInfo(DamageDefOf.Bullet, 999f, 0f, -1f, medic, victim3.health.hediffSet.GetBrain()));
        check("crisis hediff", medic.health.hediffSet.HasHediff(PM_DefOf.PM_CrisisOfFaith), "violations=" + GameComponent_Paramedic.Instance.Get(medic).violationTicks.Count);
        check("crisis: off duty", !ParamedicUtility.IsOnDuty(medic), "");
        check("crisis: abilities removed", medic.abilities.GetAbility(PM_DefOf.PM_DontShoot) == null, "");
        check("crisis: can't wear vest", !EquipmentUtility.CanEquip(Make("PM_Apparel_Vest"), medic, out string reason), reason ?? "");
        check("crisis: pocket restored", medic.inventoryStock.GetDesiredCountForGroup(InventoryStockGroupDefOf.Medicine) == 0
            || medic.inventoryStock.GetDesiredCountForGroup(InventoryStockGroupDefOf.Medicine) != 6, "");

        // ---- 9. Сохранение перезарядки при снятии/надевании
        var rec = GameComponent_Paramedic.Instance.Get(medic);
        check("cooldown stored on removal", rec.cooldownEnd.ContainsKey(PM_DefOf.PM_DontShoot.defName), "");

        // ---- 10. Спасение засчитывается через сутки
        int savedBefore = rec.saved;
        GameComponent_Paramedic.Instance.AddPending(patient, medic);
        RankUtility.Notify_LifeSaved(medic);
        check("life saved counted", rec.saved == savedBefore + 1 && medic.records.GetValue(PM_DefOf.PM_LivesSaved) >= 1, "saved=" + rec.saved);

        sb.AppendLine("\nmood thoughts on medic:");
        foreach (var m in medic.needs.mood.thoughts.memories.Memories.Where(m => m.def.defName.StartsWith("PM_")))
        {
            sb.AppendLine($"  {m.def.defName} stage={m.CurStageIndex} mood={m.MoodOffset():0.#}");
        }
    }
}
