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
			"- Centriole (small yellow barrel-shaped object) to the left\n" +
			"- Chromatid (red object on the right)\n" +
			"- Chromosome (Blue X-shaped object in the middle)",
			"1. On the FIRST turn in this stage only, give the onboarding script: greet briefly, explain the three question types, and ask for a content question about chromosomes. You MUST include these exact example questions in that first-turn script: 'What is a chromosome?' and 'What does a chromosome do?'.\n" +
			"2. After the first turn, DO NOT repeat onboarding, greeting script, or example lists unless the learner explicitly asks you to repeat instructions.\n" +
			"3. For learner content questions (for example 'What is a chromosome?' or 'What does a chromosome do?'), start with exactly 'Good.' then provide a concise biology answer.\n" +
			"4. If the learner asks a non-content question, guide them to rephrase as a definition/process question and do not say 'Good.'.",
			"CRITICAL: Keep post-onboarding replies short and task-focused. Do NOT restart the tutorial script after a correct answer. Do NOT open with filler words like 'Sure' or 'Okay'. Never omit the two example questions on the first turn.",
			"- If they ask 'What is a chromosome?' or 'What does a chromosome do?', respond: 'Good. A chromosome is a structure that contains genetic information. During cell division, chromosomes are separated so each new cell receives the correct DNA.'\n" +
			"- If they ask a non-content question, guide them to rephrase it as a content question about what a chromosome is or what it does.");

		public static string TutorialVisualQuestions = PromptLibrary.BuildPhase(
			"Tutorial: Visual Reference Questions",
			"Teach the learner how to connect biology terms to objects they can see in the scene.",
			"- Centriole (small yellow barrel-shaped object) to the left\n" +
			"- Chromatid (red object on the right)\n" +
			"- Chromosome (Blue X-shaped object in the middle)",
			"1. On the FIRST turn in this stage only, explain visual reference questions and ask them to identify the chromosome.\n" +
			"2. After the first turn, do not repeat the stage intro unless asked.\n" +
			"3. If they ask a correct visual reference question, start with 'Good.' and then point out the object (the blue X-shaped object in the middle).\n" +
			"4. If they struggle, encourage a visual reference question about the chromosome.",
			"CRITICAL: Do NOT open this phase with 'Sure', 'Okay', 'Alright' or any filler word. Be direct. Only use 'Good.' when the learner asks a correct visual reference question.",
			"- If they ask which object is the chromosome, respond: 'Good. The chromosome is the blue X-shaped structure floating in front of you.'\n" +
			"- If they ask about the yellow object, say it is the centriole on the left.\n" +
			"- If they ask about the red object, say: 'Good. The red object is the chromatid on the right.'\n" +
			"- If they ask an incorrect question type, guide them to ask which object is the chromosome and do not say 'Good.'");

		public static string TutorialManipulationQuestions = PromptLibrary.BuildPhase(
			"Tutorial: Manipulation Questions",
			"Teach the learner how to ask for procedural guidance about manipulating objects in the scene.",
			"- Centriole (small yellow barrel-shaped object) to the left\n" +
			"- Chromatid (red object on the right)\n" +
			"- Chromosome (Blue X-shaped object in the middle)",
			"1. On the FIRST turn in this stage only, explain manipulation questions and ask how to move the blue chromosome.\n" +
			"2. After the first turn, do not repeat the stage intro unless asked.\n" +
			"3. If they ask an appropriate manipulation question, start with 'Good.' and then explain: 'To move it, make the grab gesture with your hand and place it in the highlighted area.'\n" +
			"4. If they struggle, encourage a manipulation question about moving the blue chromosome.",
			"CRITICAL: Do NOT open this phase with 'Sure', 'Okay', 'Alright' or any filler word. Be direct. Only use 'Good.' when the learner asks a correct manipulation question.",
			"- If they ask how to move it, respond: 'Good. To move it, make the grab gesture with your hand and place it in the highlighted area.'\n" +
			"- If they ask a non-manipulation question, redirect them to ask how to move the blue chromosome and do not say 'Good.'");
	}
}
