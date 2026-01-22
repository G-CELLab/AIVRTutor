// Centralizes AI-related settings for easy customization
namespace AI.Prompts
{
    public static class Customizations
    {
        // OpenAI
        public static string DefaultChatModel = "openai/gpt-oss-120b";
        public static int DefaultRequestTimeoutSeconds = 30;

        // Realtime
        public static bool DefaultUseRealtime = true;
        public static string DefaultRealtimeModel = "openai/gpt-oss-120b";
        public static int DefaultRealtimeSampleRate = 24000;
        public static bool DefaultRealtimeDumpEvents = false;

        // Audio/Voice
        public static bool DefaultPreferModelAudio = true;
        public static string DefaultGptVoice = "alloy";
        public static string DefaultGptAudioFormat = "pcm16";
        public static string DefaultTtsModel = "openai/gpt-oss-120b";
        public static string DefaultTtsVoice = "alloy";
        public static string DefaultPhaseAudioFormat = "wav";

        // Language
        public static bool DefaultAlwaysEnglish = true;

        // Conversation/Memory
        public static int DefaultMaxHistoryTurnsToSend = 8;
        public static int DefaultMaxCharsBudget = 12000;

        // Debug
        public static bool DefaultVerboseDebug = true;
        public static bool DefaultDumpResponsesToFile = true;
        public static float DefaultDebugLogInterval = 0.25f;
        public static int DefaultMaxLogChars = 600;

        // Phase/Precompute
        public static bool DefaultPrecomputePhaseAssetsOnStart = true;
        public static bool DefaultSavePhaseTextFiles = true;
        public static bool DefaultGeneratePhaseTtsAudio = true;
        public static bool DefaultPrependPhaseTextAsDeveloperItem = true;
        public static bool DefaultPrependPhaseInstructionAudio = false;
    }
}
