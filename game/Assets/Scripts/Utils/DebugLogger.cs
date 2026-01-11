using UnityEngine;
using System.Diagnostics;

/// <summary>
/// Performance-safe debug logging.
/// Logs are automatically stripped in Release builds.
/// Use this instead of Debug.Log for performance-critical code.
/// </summary>
public static class DebugLogger
{
    #region Constants
    /// <summary>
    /// Define this symbol in Player Settings to enable verbose logging in builds.
    /// Edit → Project Settings → Player → Other Settings → Scripting Define Symbols
    /// Add: ENABLE_DEBUG_LOG
    /// </summary>
    private const string c_EnableSymbol = "ENABLE_DEBUG_LOG";
    #endregion

    #region Public Methods
    /// <summary>
    /// Log message (stripped in Release builds unless ENABLE_DEBUG_LOG is defined).
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional(c_EnableSymbol)]
    public static void Log(string message)
    {
        UnityEngine.Debug.Log(message);
    }

    /// <summary>
    /// Log message with context object.
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional(c_EnableSymbol)]
    public static void Log(string message, Object context)
    {
        UnityEngine.Debug.Log(message, context);
    }

    /// <summary>
    /// Log formatted message (stripped in Release builds).
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional(c_EnableSymbol)]
    public static void LogFormat(string format, params object[] args)
    {
        UnityEngine.Debug.LogFormat(format, args);
    }

    /// <summary>
    /// Log warning (stripped in Release builds unless ENABLE_DEBUG_LOG is defined).
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional(c_EnableSymbol)]
    public static void LogWarning(string message)
    {
        UnityEngine.Debug.LogWarning(message);
    }

    /// <summary>
    /// Log warning with context.
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional(c_EnableSymbol)]
    public static void LogWarning(string message, Object context)
    {
        UnityEngine.Debug.LogWarning(message, context);
    }

    /// <summary>
    /// Log error - NOT stripped (errors should always be logged).
    /// </summary>
    public static void LogError(string message)
    {
        UnityEngine.Debug.LogError(message);
    }

    /// <summary>
    /// Log error with context.
    /// </summary>
    public static void LogError(string message, Object context)
    {
        UnityEngine.Debug.LogError(message, context);
    }
    #endregion

    #region Network-Specific Logging
    /// <summary>
    /// Log network-related message (stripped in Release).
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional(c_EnableSymbol)]
    public static void LogNet(string prefix, string message)
    {
        UnityEngine.Debug.Log($"{prefix} {message}");
    }

    /// <summary>
    /// Log with bool flag check (avoids string formatting if disabled).
    /// Use this pattern: if (enableLogging) DebugLogger.Log(...);
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional(c_EnableSymbol)]
    public static void LogIf(bool condition, string message)
    {
        if (condition)
        {
            UnityEngine.Debug.Log(message);
        }
    }
    #endregion
}

