namespace AI.Prompts
{
	public static class TutorialPrompts
	{
		// Place holder (change)
		public static string Tutorial = PromptLibrary.BuildPhase(
			"Tutorial",
			"Teach the student how to interact in the tutorial area before entering the cell.",
			"- The hand wound touch sphere\n" +
			"- Grab objects for practice\n" +
			"- Duplicate object pair",
			"1. Touch and hold the sphere on the hand for 3 seconds\n" +
			"2. Grab the highlighted object and move it to the target\n" +
			"3. Duplicate the object by pulling one copy away and holding it there",
			"Keep guidance focused on the current tutorial step. If they ask for help, point them to the on-screen panels and give a short reminder.",
			"- 'What do I do first?' → 'Touch and hold the sphere on your hand for a few seconds.'\n" +
			"- 'How do I grab it?' → 'Reach out and use the grab control to pick it up.'\n" +
			"- 'How do I duplicate it?' → 'Move one copy away and hold it there for a moment.'");

		public static string TutorialContentQuestions = PromptLibrary.BuildPhase(
			"Tutorial: Content Questions",
			"Teach the learner how to ask content knowledge questions about biology terms without diving into mitosis yet.",
			"- Chromatid (visible in front of the learner)\n" +
			"- Biology terms like chromatid and metaphase",
			"1. Begin speaking immediately and explain that this phase is about how to ask content knowledge questions\n" +
			"2. Prompt them to ask: 'What is a chromatid?' or 'What happens during metaphase?'\n" +
			"3. If they ask a correct content question, answer briefly and end with: 'That was a content question.'\n" +
			"4. If they struggle, offer sentence starters like 'What is ___?' or 'What happens during ___?'",
			"CRITICAL: Do NOT open this phase with 'Sure', 'Okay', 'Alright' or any filler word. Be direct and jump straight into explaining what content questions are. Use 'That was a content question.' or similar phrasing to label correct content questions flexibly. Do not mention mitosis or teach phase details beyond a one-sentence definition.",
			"- If they ask a content question, define the term in one short sentence and keep it brief.\n" +
			"- If they ask a non-content question, guide them to rephrase it as a definition or process question.");

		public static string TutorialVisualQuestions = PromptLibrary.BuildPhase(
			"Tutorial: Visual Reference Questions",
			"Teach the learner how to connect biology terms to objects they can see in the scene.",
			"- Centriole (small barrel-shaped object)\n" +
			"- Chromosome (X-shaped object)",
			"1. Begin speaking immediately and explain visual reference questions\n" +
			"2. Prompt them to ask: 'Which object here is the chromosome?' or 'Which object here duplicates to pull chromosomes apart?'\n" +
			"3. If they ask a correct visual reference question, point out the object and end with: 'That was a visual reference question.'\n" +
			"4. If they struggle, encourage them to connect a biology term to something they see",
			"CRITICAL: Do NOT open this phase with 'Sure', 'Okay', 'Alright' or any filler word. Be direct and jump straight into explaining visual reference questions. Use 'That was a visual reference question.' or similar phrasing to label correct visual reference questions flexibly.",
			"- If they ask about the chromosome, say it is the blue object on the right.\n" +
			"- If they ask about the yellow object, say it is the centriole on the left.\n" +
			"- If they ask about the centriole, describe the barrel-shaped object on the left.");

		public static string TutorialManipulationQuestions = PromptLibrary.BuildPhase(
			"Tutorial: Manipulation Questions",
			"Teach the learner how to ask for procedural guidance when unsure what to do.",
			"- Centriole (still visible in the scene)",
			"1. Begin speaking immediately and explain manipulation questions are for 'what should I do next?'\n" +
			"2. Prompt them to ask: 'What should I do next?' or 'What do I do with the centriole?'\n" +
			"3. If they ask an appropriate manipulation question, give clear guidance and end with: 'That was a manipulation question.'\n" +
			"4. If they are unsure, gently prompt them to ask for guidance",
			"CRITICAL: Do NOT open this phase with 'Sure', 'Okay', 'Alright' or any filler word. Be direct and jump straight into explaining manipulation questions. Use 'That was a manipulation question.' or similar phrasing to label correct manipulation questions flexibly. End with a short summary of the three question types.",
			"- If they ask what to do next, give 1-2 clear steps and encourage them.\n" +
			"- If they ask about the centriole, give a brief procedural cue without teaching new biology content.");
	}
}
