using System;
using UnityEngine;
using Verse;

namespace Paramedic;

/// <summary>
/// Tracks log errors from Paramedic mod in real-time via Application.logMessageReceived.
/// </summary>
[StaticConstructorOnStartup]
public static class LogErrorTracker
{
    public static int ErrorCount { get; private set; }
    public static int ExceptionCount { get; private set; }
    public static int WarningCount { get; private set; }
    
    private static bool _initialized;

    static LogErrorTracker()
    {
        // Don't initialize here - ModsConfig might not be ready
    }

    public static void Init()
    {
        // только для автотеста: у обычных игроков хука на лог быть не должно
        if (_initialized || !SelfTestComponent.Enabled) return;
        _initialized = true;
        
        Application.logMessageReceived += OnLogMessageReceived;
        Log.Message("[Paramedic] LogErrorTracker initialized");
    }

    private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        // Only count errors/exceptions from our mod
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            if (IsFromParamedic(condition, stackTrace))
            {
                if (type == LogType.Exception || type == LogType.Assert)
                    ExceptionCount++;
                else
                    ErrorCount++;
            }
        }
        else if (type == LogType.Warning)
        {
            if (IsFromParamedic(condition, stackTrace))
            {
                WarningCount++;
            }
        }
    }

    private static bool IsFromParamedic(string condition, string stackTrace)
    {
        // Check if the log message or stack trace contains our mod identifiers
        string combined = (condition + " " + stackTrace).ToLowerInvariant();
        return combined.Contains("paramedic") || 
               combined.Contains("bebranotyep") ||
               combined.Contains("pm_");
    }

    public static void Reset()
    {
        ErrorCount = 0;
        ExceptionCount = 0;
        WarningCount = 0;
    }

    public static int TotalErrors => ErrorCount + ExceptionCount;
}