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
        public static string DefaultRealtimeModel = "gpt-realtime-mini";
        public static int DefaultRealtimeSampleRate = 24000;
        public static bool DefaultRealtimeDumpEvents = false;

        // Audio/Voice
        public static bool DefaultPreferModelAudio = true;
        public static string DefaultGptVoice = "alloy";
        public static string DefaultGptAudioFormat = "pcm16";
        public static string DefaultTtsModel = "openai/gpt-oss-120b";
        public static string DefaultTtsVoice = "alloy";
        public static string DefaultPhaseAudioFormat = "wav";

        // Conversation/Memory
        public static int DefaultMaxHistoryTurnsToSend = 4; // Reduced to limit repeated context
        public static int DefaultMaxCharsBudget = 6000; // Reduced to limit repeated context

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

        // VAD settings for Realtime
        public static int SilenceDurationMs = 1100; // Default: 1200ms (was 450ms)
        public static int PrefixPaddingMs = 250;

        // ===== Response Behavior =====
        public static int DefaultMaxOutputTokens = 1000;
        // Temperature (0.0 = precise, 1.0 = creative)
        public static float DefaultTemperature = 0.8f;

        // ===== Agent Personality =====
        // Agent name (for reference in prompts)
        public static string AgentName = "AI Tutor";
        // Personality traits to append to system prompt
        public static string PersonalityTraits = "Be friendly, patient, and encouraging. Use simple language appropriate for students.";
        // Empathy phrases the agent can use
        public static string EmpathyPhrases = "Great job!;You've got this!;That's exactly right!";

        // ===== Response Style =====
        // Max sentences per response (0 = no limit)
        public static int MaxSentencesPerResponse = 3;
        // Whether to always start with empathy
        public static bool AlwaysStartWithEmpathy = true;
        // Phrase to use when question is off-topic
        public static string OffTopicResponse = "That's a great curiosity, but let's save that for later. Right now, let's focus on what we're doing here.";
    }
}
