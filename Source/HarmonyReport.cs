using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Paramedic;

/// <summary>
/// Generates Harmony patch report for CE compatibility analysis.
/// Run with: RimWorldWin64.exe -quicktest -pmselftest -dutypatchreport
/// </summary>
public static class HarmonyReport
{
    public static readonly bool Enabled = GenCommandLine.CommandLineArgPassed("dutypatchreport");

    public static void GenerateReport()
    {
        if (!Enabled) return;

        var harmony = new Harmony("bebranotyep.paramedic");
        var sb = new StringBuilder();
        sb.AppendLine("=== Paramedic Harmony Patch Report ===");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Mod: bebranotyep.paramedic");
        sb.AppendLine();

        var targetMethods = new[]
        {
            AccessTools.Method(typeof(Pawn), nameof(Pawn.ThreatDisabled)),
            AccessTools.Method(typeof(Verb), nameof(Verb.TryStartCastOn), 
                new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) }),
            AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage)),
            AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill)),
        };

        foreach (var method in targetMethods)
        {
            if (method == null) continue;
            
            sb.AppendLine($"=== {method.DeclaringType.FullName}.{method.Name} ===");
            
            var patchInfo = Harmony.GetPatchInfo(method);
            if (patchInfo == null)
            {
                sb.AppendLine("  No patches");
                continue;
            }

            sb.AppendLine($"  Prefixes ({patchInfo.Prefixes.Count}):");
            foreach (var patch in patchInfo.Prefixes)
            {
                sb.AppendLine($"    Owner: {patch.owner}, Priority: {patch.priority}, Before: {patch.before}, After: {patch.after}");
            }

            sb.AppendLine($"  Postfixes ({patchInfo.Postfixes.Count}):");
            foreach (var patch in patchInfo.Postfixes)
            {
                sb.AppendLine($"    Owner: {patch.owner}, Priority: {patch.priority}, Before: {patch.before}, After: {patch.after}");
            }

            sb.AppendLine($"  Transpilers ({patchInfo.Transpilers.Count}):");
            foreach (var patch in patchInfo.Transpilers)
            {
                sb.AppendLine($"    Owner: {patch.owner}, Priority: {patch.priority}, Before: {patch.before}, After: {patch.after}");
            }

            sb.AppendLine();
        }

        // Also dump all our patches
        sb.AppendLine("=== All Paramedic Patches ===");
        foreach (var type in AccessTools.GetTypesFromAssembly(typeof(ParamedicMod).Assembly))
        {
            if (!type.IsDefined(typeof(HarmonyPatch), inherit: false)) continue;
            
            try
            {
                var processor = harmony.CreateClassProcessor(type);
                // Use reflection to access the internal patch info
                var mi = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                sb.AppendLine($"  {type.Name}:");
                foreach (var m in mi)
                {
                    var patches = m.GetCustomAttributes(typeof(HarmonyPatch), false);
                    if (patches.Length > 0)
                    {
                        foreach (var h in patches)
                        {
                            var hp = h as HarmonyPatch;
                            if (hp?.info?.priority != null)
                            {
                                sb.AppendLine($"    {m.Name} (prio={hp.info.priority})");
                            }
                            else
                            {
                                sb.AppendLine($"    {m.Name}");
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                sb.AppendLine($"  {type.Name}: ERROR - {e.Message}");
            }
        }

        // Write to file
        string path = System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "Harmony_CE.txt");
        File.WriteAllText(path, sb.ToString());
        Log.Message("[Paramedic] Harmony report written to " + path);
    }
}