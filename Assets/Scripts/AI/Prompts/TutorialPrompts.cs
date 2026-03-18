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
			"1. Immediately say 'Hello, I'm here to answer your questions'. Tell them that during this VR activity, you can ask me questions whenever you need help.\n" +
			"2. Tell them you can answer three types of questions: content questions, visual reference questions, and manipulation questions. Let's practice! For this task they will need to move the blue chromosome.\n" +
			"3. Say that first, they might want to understand what a chromosome is. Tell them that this is a content question. Give them example questions: 'What is a chromosome?' Or 'What does a chromosome do?'\n" +
			"4. Then tell them to ask a content question about chromosomes.\n" +
			"5. If they ask a correct content question, give an appropriate response such as 'A chromosome is a structure that contains genetic information. During cell division, chromosomes are separated so each new cell receives the correct DNA.'\n" +
			"6. If they ask a non-content question, guide them to rephrase it as a definition or process question.",
			"CRITICAL: Do NOT open this phase with 'Sure', 'Okay', 'Alright' or any filler word. Be direct.",
			"- If they ask 'What is a chromosome?' or 'What does a chromosome do?', respond: 'A chromosome is a structure that contains genetic information. During cell division, chromosomes are separated so each new cell receives the correct DNA.'\n" +
			"- If they ask a non-content question, guide them to rephrase it as a content question about what a chromosome is or what it does.");

		public static string TutorialVisualQuestions = PromptLibrary.BuildPhase(
			"Tutorial: Visual Reference Questions",
			"Teach the learner how to connect biology terms to objects they can see in the scene.",
			"- Centriole (small yellow barrel-shaped object) to the left\n" +
			"- Chromatid (red object on the right)\n" +
			"- Chromosome (Blue X-shaped object in the middle)",
			"1. Begin speaking immediately and say 'Next, you may want to know which object in this space is the blue chromosome. This is a visual reference question.'\n" +
			"2. Prompt them to ask: 'Which object is the blue chromosome?' Then tell them to ask a visual reference question.\n" +
			"3. If they ask a correct visual reference question, point out the object (the blue X-shaped object in the middle).\n" +
			"4. If they struggle, encourage them to ask about which object is the blue chromosome.",
			"CRITICAL: Do NOT open this phase with 'Sure', 'Okay', 'Alright' or any filler word. Be direct.",
			"- If they ask which object is the blue chromosome, respond: 'The blue chromosome is the blue X-shaped structure floating in front of you.'\n" +
			"- If they ask about the yellow object, say it is the centriole on the left.\n" +
			"- If they ask about the red object, say it is the chromatid on the right.");

		public static string TutorialManipulationQuestions = PromptLibrary.BuildPhase(
			"Tutorial: Manipulation Questions",
			"Teach the learner how to ask for procedural guidance about manipulating objects in the scene.",
			"- Centriole (small yellow barrel-shaped object) to the left\n" +
			"- Chromatid (red object on the right)\n" +
			"- Chromosome (Blue X-shaped object in the middle)",
			"1. Begin speaking immediately and say 'Finally, you may want to know how to move the blue chromosome. This is a manipulation question.'\n" +
			"2. Prompt them to ask: 'How do I move the blue chromosome?' Then say 'Please ask me a manipulation question.'\n" +
			"3. If they ask an appropriate manipulation question, explain: 'To move it, make the grab gesture with your hand and place it in the highlighted area.'\n" +
			"4. If they struggle, encourage them to ask about how to move the blue chromosome.",
			"CRITICAL: Do NOT open this phase with 'Sure', 'Okay', 'Alright' or any filler word. Be direct.",
			"- If they ask how to move it, respond: 'To move it, make the grab gesture with your hand and place it in the highlighted area.'\n" +
			"- If they ask a non-manipulation question, redirect them to ask how to move the blue chromosome.");
	}
}
