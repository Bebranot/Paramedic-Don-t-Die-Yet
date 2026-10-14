using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Paramedic;

/// <summary>
/// Мягкая совместимость с Combat Extended — только рефлексия, без жёстких ссылок на сборку CE.
/// Все члены возвращают null/false при отсутствии CE; ошибки пишутся в лог, не роняют игру.
/// </summary>
public static class CompatCE
{
    // Lazy check: ModsConfig might not be fully populated when ParamedicMod constructor runs.
    // Active is evaluated on first access (at runtime), not at mod load time.
    public static bool Active
    {
        get
        {
            // Check both by name and by assembly presence (more reliable at runtime)
            if (ModsConfig.IsActive("CETeam.CombatExtended") || ModsConfig.IsActive("ceteam.combatextended") || ModsConfig.IsActive("Combat Extended")) return true;
            // Check if assembly is loaded
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.GetName().Name == "CombatExtended") return true;
                }
            }
            catch { }
            return false;
        }
    }

    // Кэшированные типы/методы CE
    private static Type verbShootCE;
    private static Type verbLaunchProjectileCE;
    private static Type compSuppressable;
    private static Type ceAmmoDef;
    private static Type ceWeaponInfo;
    private static PropertyInfo isSuppressedProp;
    private static FieldInfo launcherField;
    private static FieldInfo instigatorField;
    private static MethodInfo getCEProjectileInfo;
    private static bool initialized;

    /// <summary>
    /// Вызывать один раз при старте (например, из ParamedicMod конструктора после PatchAllSafe).
    /// Пишет в лог, что CE найден и какие хуки доступны.
    /// </summary>
    public static void Init()
    {
        // Don't early-return here — ModsConfig might not be ready yet.
        // Just try to find the CE assembly; if found, CE is present.
        if (initialized) return;
        initialized = true;

        try
        {
            Assembly ceAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "CombatExtended");
            if (ceAsm == null)
            {
                Log.Message("[Paramedic] Combat Extended not active (assembly not found)");
                return;
            }

            verbShootCE = ceAsm.GetType("CombatExtended.Verb_ShootCE");
            verbLaunchProjectileCE = ceAsm.GetType("CombatExtended.Verb_LaunchProjectileCE");
            compSuppressable = ceAsm.GetType("CombatExtended.CompSuppressable");
            ceAmmoDef = ceAsm.GetType("CombatExtended.CE_AmmoDef");
            ceWeaponInfo = ceAsm.GetType("CombatExtended.CE_WeaponInfo");

            if (compSuppressable != null)
            {
                isSuppressedProp = AccessTools.Property(compSuppressable, "IsSuppressed");
            }

            // Projectile CE: launcher/instigator fields for damage attribution
            Type projectileCE = ceAsm.GetType("CombatExtended.ProjectileCE");
            if (projectileCE != null)
            {
                launcherField = AccessTools.Field(projectileCE, "launcher");
                instigatorField = AccessTools.Field(projectileCE, "instigator");
            }

            // Helper: get CE ammo/weapon info from ThingDef
            getCEProjectileInfo = AccessTools.Method(ceWeaponInfo, "GetWeaponInfo", new[] { typeof(ThingDef) });

            Log.Message("[Paramedic] Combat Extended detected: Verb_ShootCE=" + (verbShootCE != null)
                + " Verb_LaunchProjectileCE=" + (verbLaunchProjectileCE != null)
                + " CompSuppressable=" + (compSuppressable != null)
                + " ProjectileCE launcher/instigator=" + (launcherField != null && instigatorField != null));
        }
        catch (Exception e)
        {
            Log.Error("[Paramedic] CompatCE.Init failed: " + e);
        }
    }

    // ---------- Публичные безопасные запросы ----------

    /// <summary>True, если verb — это CE-стрелба (Verb_ShootCE или Verb_LaunchProjectileCE).</summary>
    public static bool IsCEVerb(Verb verb)
    {
        if (verb == null) return false;
        Type t = verb.GetType();
        return verbShootCE != null && (t == verbShootCE || t.IsSubclassOf(verbShootCE))
            || verbLaunchProjectileCE != null && (t == verbLaunchProjectileCE || t.IsSubclassOf(verbLaunchProjectileCE));
    }

    /// <summary>True, если на цели есть CompSuppressable и она подавлена.</summary>
    public static bool IsSuppressed(Thing target)
    {
        if (target == null || compSuppressable == null || isSuppressedProp == null) return false;
        try
        {
            // Thing.GetComp<T>() — generic, вызываем через рефлексию
            var getCompMethod = typeof(Thing).GetMethod("GetComp", Type.EmptyTypes)?.MakeGenericMethod(compSuppressable);
            if (getCompMethod == null) return false;
            var comp = getCompMethod.Invoke(target, null);
            if (comp == null) return false;
            return (bool?)isSuppressedProp.GetValue(comp) == true;
        }
        catch (Exception e)
        {
            Log.Warning("[Paramedic] CompatCE.IsSuppressed error: " + e);
            return false;
        }
    }

    /// <summary>Получить instigator (виновника) у CE-снаряда/урона. Возвращает null, если не CE или поле недоступно.</summary>
    public static Pawn GetCEInstigator(Thing projectileOrDamageSource)
    {
        if (projectileOrDamageSource == null || instigatorField == null) return null;
        try
        {
            return instigatorField.GetValue(projectileOrDamageSource) as Pawn;
        }
        catch { return null; }
    }

    /// <summary>Получить launcher (пусковую установку/оружие) у CE-снаряда. Для определения владельца выстрела.</summary>
    public static Pawn GetCELauncher(Thing projectile)
    {
        if (projectile == null || launcherField == null) return null;
        try
        {
            return launcherField.GetValue(projectile) as Pawn;
        }
        catch { return null; }
    }

    /// <summary>Проверка: есть ли у вещи CE-инфо об оружии/амуниции.</summary>
    public static bool HasCEWeaponInfo(ThingDef def)
    {
        if (def == null || getCEProjectileInfo == null) return false;
        try
        {
            return getCEProjectileInfo.Invoke(null, new object[] { def }) != null;
        }
        catch { return false; }
    }
}