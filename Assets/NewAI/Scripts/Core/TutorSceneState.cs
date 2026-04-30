using System;

namespace NewAI.Scripts.Core
{
    /// <summary>
    /// Represents the current state of the VR tutoring scene
    /// Used to provide context to the RAG system
    /// </summary>
    [System.Serializable]
    public class TutorSceneState
    {
        public string Phase { get; set; } = "interphase";
        public string SceneSummary { get; set; } = "The learner is in the tutorial/learning simulation.";
        public string CurrentObjective { get; set; } = "Answer biology and mitosis questions conversationally.";
        public string VisibleObjects { get; set; } = 
            "Nutrient capsules: three green capsules (protein, magnesium, vitamin C); " +
            "Red DNA: on the left; " +
            "Centriole: yellow barrel-shaped object on the right.";
        public string NextAction { get; set; } = 
            "If the learner asks what to do next, consult the knowledge base for current phase guidance.";

        public TutorSceneState()
        {
        }

        /// <summary>
        /// Create from dictionary (for JSON deserialization)
        /// </summary>
        public static TutorSceneState FromDictionary(System.Collections.Generic.Dictionary<string, object> dict)
        {
            var state = new TutorSceneState();
            
            if (dict.TryGetValue("phase", out var phase))
                state.Phase = phase?.ToString() ?? "interphase";
                
            if (dict.TryGetValue("scene_summary", out var summary))
                state.SceneSummary = summary?.ToString() ?? state.SceneSummary;
                
            if (dict.TryGetValue("current_objective", out var obj))
                state.CurrentObjective = obj?.ToString() ?? state.CurrentObjective;
                
            if (dict.TryGetValue("visible_objects", out var visible))
                state.VisibleObjects = visible?.ToString() ?? state.VisibleObjects;
                
            if (dict.TryGetValue("next_action", out var next))
                state.NextAction = next?.ToString() ?? state.NextAction;

            return state;
        }

        /// <summary>
        /// Convert to dictionary (for JSON serialization)
        /// </summary>
        public System.Collections.Generic.Dictionary<string, object> ToDictionary()
        {
            return new System.Collections.Generic.Dictionary<string, object>
            {
                { "phase", Phase },
                { "scene_summary", SceneSummary },
                { "current_objective", CurrentObjective },
                { "visible_objects", VisibleObjects },
                { "next_action", NextAction }
            };
        }

        public override string ToString()
        {
            return $"TutorSceneState(phase={Phase}, objective={CurrentObjective})";
        }
    }
}
