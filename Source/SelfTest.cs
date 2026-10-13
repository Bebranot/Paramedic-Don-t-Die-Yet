using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

/// <summary>
/// Диагностика CE-снарядов: <c>CombatExtended.ProjectileCE</c> (и его наследник <c>BulletCE</c>, который зовёт base.Impact)
/// вместо ванильного <c>Bullet</c>. Доступ только через рефлексию — ссылки на сборку CE в csproj нет (правило 5).
/// </summary>
[HarmonyLib.HarmonyPatch]
public static class SelfTest_CEProjectileImpact_Patch
{
    private static FieldInfo launcherField, intendedField;

    public static bool Prepare() => SelfTestComponent.Enabled && SelfTestComponent.CEActive
        && HarmonyLib.AccessTools.TypeByName("CombatExtended.ProjectileCE") != null;

    public static MethodBase TargetMethod()
    {
        Type t = HarmonyLib.AccessTools.TypeByName("CombatExtended.ProjectileCE");
        return HarmonyLib.AccessTools.Method(t, "Impact", new[] { typeof(Thing) });
    }

    public static void Prefix(object __instance, Thing hitThing)
    {
        if (launcherField == null)
        {
            Type t = HarmonyLib.AccessTools.TypeByName("CombatExtended.ProjectileCE");
            launcherField = HarmonyLib.AccessTools.Field(t, "launcher");
            intendedField = HarmonyLib.AccessTools.Field(t, "intendedTarget");
        }
        if (hitThing is not Pawn p || !ShoutUtility.Protected.Contains(p)) return;
        var launcher = launcherField?.GetValue(__instance) as Pawn;
        LocalTargetInfo intended = intendedField != null ? (LocalTargetInfo)intendedField.GetValue(__instance) : LocalTargetInfo.Invalid;
        float respect = launcher != null ? ShoutUtility.RespectChance(launcher, p) : -1f;
        bool mustRespect = launcher != null && ShoutUtility.IsProtectedFrom(p, launcher);
        if (intended.Thing != p) SelfTestComponent.CEStrayHits++;
        else if (mustRespect) SelfTestComponent.CEAimedHits++;
        else SelfTestComponent.CEAllowedHits++;
        SelfTestComponent.Events.Add($"  respect={respect:0.##} mustRespect={mustRespect} mental={launcher?.MentalStateDef?.defName}");
        SelfTestComponent.Events.Add($"CE projectile hit medic: launcher={launcher?.LabelShort ?? "?"}, intended={intended.Thing?.LabelShort ?? intended.Cell.ToString()}");
    }
}

/// <summary>
/// Счёт реальных выстрелов CE: каждый созданный снаряд <c>ProjectileCE</c> (путь <c>Verb_LaunchProjectileCE.SpawnProjectile</c>).
/// Без этого боя с CE может не быть вовсе, а тест «медик не ранен» останется ложно зелёным.
/// </summary>
[HarmonyLib.HarmonyPatch]
public static class SelfTest_CEShot_Patch
{
    public static bool Prepare() => SelfTestComponent.Enabled && SelfTestComponent.CEActive
        && HarmonyLib.AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE") != null;

    public static MethodBase TargetMethod()
    {
        Type t = HarmonyLib.AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
        return HarmonyLib.AccessTools.Method(t, "SpawnProjectile", Type.EmptyTypes);
    }

    public static void Prefix(object __instance)
    {
        if ((__instance as Verb)?.CasterPawn is Pawn p && p.HostileTo(Faction.OfPlayer)) SelfTestComponent.CEShots++;
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
    /// <summary>CE: реальные снаряды (ProjectileCE) и попадания ими по защищённому медику.</summary>
    public static int CEShots, CEAimedHits, CEAllowedHits, CEStrayHits;

    public SelfTestComponent(Game game) { }

    public static readonly bool Enabled = GenCommandLine.CommandLineArgPassed("pmselftest");

    /// <summary>Активен ли Combat Extended (без прямых ссылок на его типы).</summary>
    public static bool CEActive => ModsConfig.IsActive("CETeam.CombatExtended");

    public static bool CEArmoryActive => ModsConfig.IsActive("CETeam.CombatExtendedArmory");

    /// <summary>Режим прогона для отчёта: vanilla / CE / CE+Armory.</summary>
    public static string ModeName => CEActive ? (CEArmoryActive ? "CE+Armory" : "CE") : "vanilla";

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
        int logErrors = LogErrorCount();
        sb.Insert(0, $"Paramedic self-test: {(fails == 0 ? "PASS" : fails + " FAIL")}\n" +
            $"mode: {ModeName}\n" +
            $"log errors (approx, so far): {(logErrors < 0 ? "n/a" : logErrors.ToString())}\n" +
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
        // итог по режиму: файл накапливает строки от прогонов vanilla и CE, чтобы PASS/FAIL сравнивались в одном месте
        try
        {
            File.AppendAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "ParamedicSelfTest.modes.txt"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm} mode={ModeName} selftest={(fails == 0 ? "PASS" : "FAIL")} logerrors={(logErrors < 0 ? "n/a" : logErrors.ToString())}\n");
        }
        catch (Exception e)
        {
            sb.AppendLine("modes summary not written: " + e.Message);
        }
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
            ArmRaider(r);
            raiders.Add(r);
        }
        if (CEActive)
        {
            int armed = raiders.Count(IsCEArmedWithAmmo);
            Check("CE: raiders have CE weapons + ammo", armed == raiders.Count, armed + "/" + raiders.Count);
        }
        Events.Clear();
        Verse.AI.Group.LordMaker.MakeNewLord(pirates, new LordJob_AssaultColony(pirates, canKidnap: false, canTimeoutOrFlee: false), map, raiders);
        medicInjuriesBefore = InjuryCount(liveMedic);
        HostileShots = BlockedShots = AimedHits = StrayHits = AllowedHits = 0;
        CEShots = CEAimedHits = CEAllowedHits = CEStrayHits = 0;
        liveFireStart = Find.TickManager.TicksGame;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
    }

    /// <summary>Вооружает пирата; с CE — досылает магазин и кладёт патроны, иначе он не стреляет и бой ложно зелёный.</summary>
    private void ArmRaider(Pawn r)
    {
        r.equipment.DestroyAllEquipment();
        var gun = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle"));
        r.equipment.AddEquipment(gun);
        if (!CEActive) return;

        ThingComp comp = gun.AllComps.FirstOrDefault(c => c.GetType().Name == "CompAmmoUser");
        if (comp == null)
        {
            sb.AppendLine("  CE: no CompAmmoUser on " + gun.def.defName);
            return;
        }
        // полный магазин — штатный путь CE при генерации рейдеров
        Type ammoDefType = HarmonyLib.AccessTools.TypeByName("CombatExtended.AmmoDef");
        comp.GetType().GetMethod("ResetAmmoCount", new[] { ammoDefType })?.Invoke(comp, new object[] { null });
        // запас патронов в инвентарь — на перезарядку
        ThingDef ammo = FirstAmmoDef(comp);
        if (ammo != null)
        {
            Thing stack = ThingMaker.MakeThing(ammo);
            stack.stackCount = Mathf.Min(60, ammo.stackLimit);
            r.inventory.innerContainer.TryAdd(stack);
        }
        sb.AppendLine($"  CE raider {r.LabelShort}: {gun.def.defName} mag={MagCount(comp)} ammo={ammo?.defName ?? "?"}");
    }

    /// <summary>Первый подходящий патрон патронного набора оружия (через рефлексию).</summary>
    private static ThingDef FirstAmmoDef(ThingComp comp)
    {
        object ammoSet = comp.GetType().GetProperty("CurAmmoSet")?.GetValue(comp);
        if (ammoSet?.GetType().GetField("ammoTypes")?.GetValue(ammoSet) is not System.Collections.IEnumerable links) return null;
        foreach (object link in links) return link.GetType().GetField("ammo")?.GetValue(link) as ThingDef;
        return null;
    }

    private static object MagCount(ThingComp comp) => comp.GetType().GetProperty("CurMagCount")?.GetValue(comp);

    /// <summary>С CE: оружие есть и из него можно выстрелить (магазин или патроны; при выключенной системе патронов — всегда да).</summary>
    private static bool IsCEArmedWithAmmo(Pawn r)
    {
        ThingComp comp = r.equipment?.Primary?.AllComps.FirstOrDefault(c => c.GetType().Name == "CompAmmoUser");
        if (comp == null) return false;
        return comp.GetType().GetProperty("HasAndUsesAmmoOrMagazine")?.GetValue(comp) is bool ok && ok;
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
        sb.AppendLine($"hostile casts: {HostileShots}, blocked at medic: {BlockedShots}"
            + (CEActive ? $", CE projectiles fired: {CEShots}" : ""));
        Check("raiders actually fought", HostileShots > 0, "casts=" + HostileShots);
        // Без единого реального снаряда бой с CE не состоялся — «медик не ранен» ничего не доказывает
        if (CEActive) Check("CE: live fire happened", CEShots > 0, "CE projectiles=" + CEShots);
        sb.AppendLine($"hits on medic: aimed by respecting {AimedHits}, aimed by non-respecting {AllowedHits}, stray {StrayHits}"
            + (CEActive ? $"; CE aimed {CEAimedHits}, CE allowed {CEAllowedHits}, CE stray {CEStrayHits}" : ""));
        Check("no aimed hits on medic", AimedHits + CEAimedHits == 0, "aimed=" + (AimedHits + CEAimedHits) + ", injuries=" + injuries);
        Check("no perfidy during fight", !Events.Any(e => e.StartsWith("perfidy")), "");
    }

    /// <summary>
    /// CE-режим: у формы заданы Bulk/WornBulk и носимый запас влезает в CarryBulk.
    /// Проверяется <b>наличие</b> стата в def: у CE <c>Bulk</c> имеет defaultBaseValue=1,
    /// поэтому проверка значения дала бы ложно-зелёный результат. Без CE статов нет — пункт неприменим.
    /// </summary>
    private static void CheckCEBulk(Action<string, bool, string> check, Pawn medic)
    {
        StatDef bulk = DefDatabase<StatDef>.GetNamedSilentFail("Bulk");
        StatDef wornBulk = DefDatabase<StatDef>.GetNamedSilentFail("WornBulk");
        StatDef carryBulk = DefDatabase<StatDef>.GetNamedSilentFail("CarryBulk");
        if (bulk == null || wornBulk == null || carryBulk == null) return; // CE не загружен

        string[] kit = { "PM_Apparel_Shirt", "PM_Apparel_Pants", "PM_Apparel_Vest", "PM_Apparel_Jacket",
            "PM_Apparel_Beret", "PM_Apparel_Helmet", "PM_Apparel_Goggles", "PM_Apparel_Belt" };
        var noBulk = new List<string>();
        var noWorn = new List<string>();
        foreach (string d in kit)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(d);
            if (def == null) continue;
            if (!def.statBases.Any(m => m.stat == bulk)) noBulk.Add(d);
            if (!def.statBases.Any(m => m.stat == wornBulk)) noWorn.Add(d);
        }
        check("CE: kit apparel has Bulk", noBulk.Count == 0, noBulk.Count == 0 ? kit.Length + " defs" : "missing: " + string.Join(", ", noBulk));
        check("CE: kit apparel has WornBulk", noWorn.Count == 0, noWorn.Count == 0 ? kit.Length + " defs" : "missing: " + string.Join(", ", noWorn));

        // Носимый запас: до 6 медикаментов (повязки тратят по одному) + шприц — должен укладываться в CarryBulk
        float need = 6f * ThingDefOf.MedicineIndustrial.GetStatValueAbstract(bulk)
            + PM_DefOf.PM_AdrenalineSyringe.GetStatValueAbstract(bulk);
        float cap = medic.GetStatValue(carryBulk);
        check("CE: 6 medicine + syringe fit CarryBulk", need <= cap, need.ToString("0.##") + " / " + cap.ToString("0.##"));
    }

    /// <summary>Оценка числа ошибок в Player.log на момент отчёта (скрипт-обвязка считает точнее и с группировкой).</summary>
    private static int LogErrorCount()
    {
        try
        {
            string log = Path.Combine(GenFilePaths.SaveDataFolderPath, "Player.log");
            if (!File.Exists(log)) return -1;
            int n = 0;
            foreach (string line in File.ReadAllLines(log))
            {
                if (line.StartsWith("Exception") || line.Contains("NullReferenceException") || line.Contains("different thread")) n++;
            }
            return n;
        }
        catch { return -1; }
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
        string[] kit = { "PM_Apparel_Shirt", "PM_Apparel_Pants", "PM_Apparel_Jacket", "PM_Apparel_Goggles", "PM_Apparel_Belt" };
        foreach (string d in kit)
        {
            medic.apparel.Wear(Make(d), dropReplacedApparel: false);
        }
        // форма надевается целиком: ни одна вещь не вытеснена конфликтом слоёв/частей тела
        int wornPieces = medic.apparel.WornApparel.Count(a => a.TryGetComp<CompParamedicGear>() != null);
        check("kit worn whole (no slot conflicts)", wornPieces == 7, "pieces=" + wornPieces);
        duty = medic.health.hediffSet.GetFirstHediffOfDef(PM_DefOf.PM_OnDuty);
        check("full kit stage", duty != null && duty.CurStageIndex == 2, "sev=" + duty?.Severity + " worn=" + medic.apparel.WornApparelCount);
        stock = medic.inventoryStock.GetDesiredCountForGroup(InventoryStockGroupDefOf.Medicine);
        check("medicine pocket with rig = 6", stock == 6, "stock=" + stock);
        check("pocket does not exceed limit", stock <= 6, "stock=" + stock);
        CheckCEBulk(check, medic);
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
