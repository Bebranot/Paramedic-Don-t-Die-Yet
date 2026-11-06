using System.Collections.Generic;
using System.IO;
using RimWorld;
using UnityEngine;
using Verse;

namespace Paramedic;

/// <summary>
/// Снимки формы на всех типах тел: запуск с <c>-quicktest -pmshot</c>.
/// Пять пешек (Male/Female/Thin/Hulk/Fat) в полной выкладке, три снимка (юг/восток/север) → ParamedicShot_*.png рядом с Player.log.
/// </summary>
public class ShotTestComponent : GameComponent
{
    public static readonly bool Enabled = GenCommandLine.CommandLineArgPassed("pmshot");

    private int startTick = -1;
    private int step;
    private readonly List<Pawn> models = new();
    private static readonly Rot4[] Rots = { Rot4.South, Rot4.East, Rot4.North };

    public ShotTestComponent(Game game) { }

    public override void GameComponentTick()
    {
        if (!Enabled || Find.CurrentMap == null || step > 7) return;
        int now = Find.TickManager.TicksGame;
        if (startTick < 0) { startTick = now; return; }
        int t = now - startTick;
        if (step == 0 && t >= 120) { Setup(); step = 1; return; }
        // шаги 1..3: повернуть, через 60 тиков снять
        if (step is >= 1 and <= 6 && t >= 120 + step * 60)
        {
            int shot = (step - 1) / 2;
            if (step % 2 == 1)
            {
                Find.WindowStack.TryRemove(typeof(LudeonTK.EditWindow_Log), doCloseSound: false);
                Find.CameraDriver.SetRootPosAndSize(models[2].DrawPos, 4f);
                foreach (Pawn p in models)
                {
                    var job = JobMaker.MakeJob(JobDefOf.Wait, 5000);
                    job.overrideFacing = Rots[shot];
                    p.jobs.StartJob(job, Verse.AI.JobCondition.InterruptForced);
                    p.Rotation = Rots[shot];
                }
            }
            else
            {
                string path = Path.Combine(GenFilePaths.SaveDataFolderPath, $"ParamedicShot_{Rots[shot].ToStringHuman()}.png");
                // Через рефлексию: сборка 1.6 не ссылается на UnityEngine.ScreenCaptureModule.
                HarmonyLib.AccessTools.Method(HarmonyLib.AccessTools.TypeByName("UnityEngine.ScreenCapture"), "CaptureScreenshot", new[] { typeof(string) })
                    ?.Invoke(null, new object[] { path });
            }
            step++;
            return;
        }
        if (step == 7 && t >= 120 + 8 * 60)
        {
            step = 8;
            Log.Message("[Paramedic] screenshots done");
            Application.Quit();
        }
    }

    private void Setup()
    {
        Map map = Find.CurrentMap;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
        IntVec3 center = map.Center;
        if (!center.Standable(map)) CellFinder.TryFindRandomCellNear(center, map, 20, c => c.Standable(map), out center);
        BodyTypeDef[] bodies = { BodyTypeDefOf.Male, BodyTypeDefOf.Female, BodyTypeDefOf.Thin, BodyTypeDefOf.Hulk, BodyTypeDefOf.Fat };
        string[] kit = { "PM_Apparel_Shirt", "PM_Apparel_Pants", "PM_Apparel_Vest", "PM_Apparel_Jacket", "PM_Apparel_Beret", "PM_Apparel_Goggles", "PM_Apparel_Belt" };
        for (int i = 0; i < bodies.Length; i++)
        {
            Pawn p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                forceGenerateNewPawn: true, developmentalStages: DevelopmentalStage.Adult));
            p.story.bodyType = bodies[i];
            p.apparel.DestroyAll();
            p.equipment?.DestroyAllEquipment();
            // каждый второй — в каске вместо берета, центральный без куртки (видно жилет)
            foreach (string d in kit)
            {
                if (i % 2 == 1 && d == "PM_Apparel_Beret") continue;
                if (i == 2 && d == "PM_Apparel_Jacket") continue;
                p.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(d)), dropReplacedApparel: false);
            }
            if (i % 2 == 1) p.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("PM_Apparel_Helmet")), dropReplacedApparel: false);
            IntVec3 cell = center + new IntVec3(i * 2 - 4, 0, 0);
            GenSpawn.Spawn(p, cell, map);
            models.Add(p);
        }
        Find.CameraDriver.SetRootPosAndSize(center.ToVector3Shifted(), 9f);
        // CE на картинку формы не влияет (Bulk/броня — только статы); режим в логе, чтобы не путать снимки
        Log.Message($"[Paramedic] shot mode: {SelfTestComponent.ModeName}");
    }
}
