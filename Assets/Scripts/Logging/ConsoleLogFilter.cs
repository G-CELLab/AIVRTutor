using System;
using UnityEngine;

/// <summary>
/// Filters Unity console Log messages so only high-signal AI debugging output remains visible.
/// Warnings and errors still pass through unchanged.
/// </summary>
public sealed class ConsoleLogFilter : ILogHandler
{
    private static readonly string[] AllowedLogPrefixes =
    {
        "[ai]",
        "[latency]",
        "[bridge]",
        "[listen]",
        "[tts]",
        "[STT][assistant/done]",
        "[STT][assistant/heard]"
    };

    private readonly ILogHandler innerHandler;
    private static bool installed;

    public ConsoleLogFilter(ILogHandler innerHandler)
    {
        this.innerHandler = innerHandler ?? throw new ArgumentNullException(nameof(innerHandler));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (installed)
        {
            return;
        }

        installed = true;
        Debug.unityLogger.logHandler = new ConsoleLogFilter(Debug.unityLogger.logHandler);
        Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
    }

    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        string message;
        try
        {
            message = (args != null && args.Length > 0) ? string.Format(format, args) : format;
        }
        catch
        {
            message = format;
        }

        if (ShouldAllow(logType, message))
        {
            innerHandler.LogFormat(logType, context, format, args);
        }
    }

    public void LogException(Exception exception, UnityEngine.Object context)
    {
        innerHandler.LogException(exception, context);
    }

    private static bool ShouldAllow(LogType logType, string message)
    {
        if (logType != LogType.Log)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        for (int i = 0; i < AllowedLogPrefixes.Length; i++)
        {
            if (message.StartsWith(AllowedLogPrefixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}