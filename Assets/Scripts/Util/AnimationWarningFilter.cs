using UnityEngine;
using UnityEditor;
using System;

namespace Project.Editor
{
    /// <summary>
    /// Suppresses specific annoying project warnings that are known to be harmless.
    /// </summary>
    [InitializeOnLoad]
    internal class AnimationWarningFilter : ILogHandler
    {
        private static readonly ILogHandler DefaultHandler = Debug.unityLogger.logHandler;

        static AnimationWarningFilter()
        {
            if (!(Debug.unityLogger.logHandler is AnimationWarningFilter))
            {
                Debug.unityLogger.logHandler = new AnimationWarningFilter();
            }
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            // Suppress animation import warnings
            if (logType == LogType.Warning && format.Contains("has animation import warnings"))
            {
                return;
            }

            // Suppress Multiple ADB server notifications
            if (format.Contains("Multiple ADB server instances"))
            {
                return;
            }

            // Suppress specific TTS and Speech Recognizer warnings
            if (logType == LogType.Warning)
            {
                if (format.Contains("TextToSpeechPlayer") || 
                    format.Contains("OpenAISpeechRecognizer") ||
                    (args != null && args.Length > 0 && args[0].ToString().Contains("TextToSpeechPlayer")) ||
                    (args != null && args.Length > 0 && args[0].ToString().Contains("OpenAISpeechRecognizer")))
                {
                    return;
                }
            }

            DefaultHandler.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            DefaultHandler.LogException(exception, context);
        }
    }
}