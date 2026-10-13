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
    public static bool Active => ModsConfig.IsActive("CETeam.CombatExtended");

    // Кэшированные типы/методы CE
    private static Type verbShootCE;
    private static Type verbLaunchProjectileCE;
    private static Type compSuppressable;
    private static Type ceAmmoDef;
    private static Type ceWeaponInfo;
    private static MethodInfo tryStartCastOn_CE;
    private static MethodInfo tryCastShot_CE;
    private static PropertyInfo suppressorProp;
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
        if (!Active)
        {
            Log.Message("[Paramedic] Combat Extended not active");
            return;
        }
        if (initialized) return;
        initialized = true;

        try
        {
            Assembly ceAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "CombatExtended");
            if (ceAsm == null)
            {
                Log.Warning("[Paramedic] CE active but CombatExtended assembly not found in AppDomain");
                return;
            }

            verbShootCE = ceAsm.GetType("CombatExtended.Verb_ShootCE");
            verbLaunchProjectileCE = ceAsm.GetType("CombatExtended.Verb_LaunchProjectileCE");
            compSuppressable = ceAsm.GetType("CombatExtended.CompSuppressable");
            ceAmmoDef = ceAsm.GetType("CombatExtended.CE_AmmoDef");
            ceWeaponInfo = ceAsm.GetType("CombatExtended.CE_WeaponInfo");

            if (verbShootCE != null)
            {
                tryStartCastOn_CE = AccessTools.Method(verbShootCE, "TryStartCastOn", new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
                tryCastShot_CE = AccessTools.Method(verbShootCE, "TryCastShot");
            }
            if (verbLaunchProjectileCE != null && tryCastShot_CE == null)
            {
                tryCastShot_CE = AccessTools.Method(verbLaunchProjectileCE, "TryCastShot");
            }
            if (compSuppressable != null)
            {
                suppressorProp = AccessTools.Property(compSuppressable, "Suppressor");
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
        if (target == null || compSuppressable == null) return false;
        try
        {
            // Use reflection to call the generic GetComp<T> method with our runtime type
            var getCompMethod = typeof(ThingCompUtility).GetMethod("GetComp", new[] { typeof(Thing) })
                ?? typeof(Thing).GetMethod("GetComp", new Type[0])?.GetGenericMethodDefinition();
            if (getCompMethod == null) return false;
            var genericMethod = getCompMethod.MakeGenericMethod(compSuppressable);
            var comp = genericMethod.Invoke(null, new object[] { target });
            if (comp == null) return false;
            return (bool?)isSuppressedProp?.GetValue(comp) == true;
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

    /// <summary>Попытка вызвать CE-версию TryStartCastOn через рефлексию. Возвращает true, если вызов прошёл (даже если вернул false).</summary>
    public static bool TryInvokeCE_TryStartCastOn(Verb verb, LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack, bool canHitNonTargetPawns, bool preventFriendlyFire, bool nonInterrupting)
    {
        if (verb == null || tryStartCastOn_CE == null) return false;
        try
        {
            object result = tryStartCastOn_CE.Invoke(verb, new object[] { castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterrupting });
            return result is bool b && b;
        }
        catch (Exception e)
        {
            Log.Warning("[Paramedic] CompatCE.TryInvokeCE_TryStartCastOn error: " + e);
            return false;
        }
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

    /// <summary>CE меняет модели урона/брони. Вернуть true, если DamageInfo, вероятно, от CE-снаряда.</summary>
    public static bool IsCEDamageInfo(DamageInfo dinfo)
    {
        // CE-снаряды часто имеют Def = CE_AmmoDef или Instigator/Launcher заполнены особым образом
        if (dinfo.Def != null && ceAmmoDef != null && dinfo.Def.GetType() == ceAmmoDef) return true;
        if (dinfo.Instigator != null && GetCEInstigator(dinfo.Instigator) != null) return true;
        return false;
    }
}