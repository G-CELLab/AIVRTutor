import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import Assets.AiPrototype.rag_cli as rag_cli


class RagCliUnitTests(unittest.TestCase):
    def test_meaningful_terms_filters_stopwords_and_short_tokens(self) -> None:
        terms = rag_cli._meaningful_query_terms("what is dna in a cell and why")
        self.assertEqual(terms, ["dna", "cell"])

    def test_scope_signal_greeting_has_single_meaningful_term(self) -> None:
        index = rag_cli.LocalRagIndex(kb_dir=Path("."))
        signal = rag_cli.retrieval_scope_signal("hello there", [], index)
        self.assertEqual(signal["query_terms"], 1.0)
        self.assertEqual(signal["kb_coverage"], 0.0)
        self.assertEqual(signal["top_score"], 0.0)

    def test_is_out_of_scope_ignores_short_social_queries(self) -> None:
        signal = {"top_score": 0.0, "kb_coverage": 0.0, "query_terms": 0.0, "covered_terms": 0.0}
        self.assertFalse(
            rag_cli.is_out_of_scope(
                signal,
                min_top_score=0.05,
                min_kb_coverage=0.25,
                min_query_terms_for_scope_check=2,
            )
        )

    def test_is_out_of_scope_true_for_low_score_and_low_coverage(self) -> None:
        signal = {"top_score": 0.01, "kb_coverage": 0.1, "query_terms": 4.0, "covered_terms": 0.0}
        self.assertTrue(
            rag_cli.is_out_of_scope(
                signal,
                min_top_score=0.05,
                min_kb_coverage=0.25,
                min_query_terms_for_scope_check=2,
            )
        )

    def test_keep_kb_grounded_sentences_trims_unsupported_content(self) -> None:
        retrieved = [
            (
                rag_cli.Chunk(
                    source="kb.md",
                    text="DNA is stored in the nucleus and condenses into chromosomes before division.",
                ),
                0.9,
            )
        ]
        answer = (
            "DNA is stored in the nucleus. "
            "It has a double helix and nucleotides with sugar-phosphate backbones."
        )
        filtered = rag_cli.keep_kb_grounded_sentences(answer, retrieved, min_sentence_grounding=0.65)
        self.assertIn("DNA is stored in the nucleus.", filtered)
        self.assertNotIn("double helix", filtered)

    def test_keep_kb_grounded_sentences_returns_empty_without_context(self) -> None:
        filtered = rag_cli.keep_kb_grounded_sentences("DNA is stored in the nucleus.", [], min_sentence_grounding=0.65)
        self.assertEqual(filtered, "")

    def test_build_not_in_kb_reply_mentions_phase(self) -> None:
        state = rag_cli.SceneState(phase="metaphase")
        reply = rag_cli.build_not_in_kb_reply(state)
        self.assertIn("metaphase", reply)
        self.assertIn("knowledge base", reply)

    def test_infer_phase_from_text(self) -> None:
        self.assertEqual(rag_cli.infer_phase_from_text("we are now in Prophase"), "prophase")
        self.assertIsNone(rag_cli.infer_phase_from_text("no phase term here"))

    def test_parse_args_defaults_include_new_scope_flags(self) -> None:
        with patch.object(sys, "argv", ["rag_cli.py"]):
            args = rag_cli.parse_args()
        self.assertEqual(args.max_output_tokens, 48)
        self.assertEqual(args.min_retrieval_score, 0.05)
        self.assertEqual(args.min_kb_coverage, 0.25)
        self.assertEqual(args.min_query_terms_for_scope_check, 2)
        self.assertEqual(args.min_sentence_grounding, 0.65)

    def test_make_concise_answer_keeps_one_sentence_and_caps_words(self) -> None:
        raw = "Anaphase is when sister chromatids separate and move to opposite poles quickly. This ensures equal chromosome distribution."
        concise = rag_cli.make_concise_answer(raw, max_sentences=1, max_words=10)
        self.assertNotIn("This ensures", concise)
        self.assertLessEqual(len(concise.split()), 10)

    def test_add_conversational_opening_for_definition_question(self) -> None:
        out = rag_cli.add_conversational_opening(
            "what is anaphase",
            "Anaphase is when sister chromatids separate.",
        )
        self.assertTrue(out.lower().startswith("great question,"))

    def test_add_conversational_opening_yes_for_yes_no_question(self) -> None:
        out = rag_cli.add_conversational_opening(
            "does dna replicate",
            "DNA replicates during S phase.",
        )
        self.assertTrue(out.lower().startswith("yes,"))

    def test_acknowledgement_reply_for_ok_message(self) -> None:
        state = rag_cli.SceneState(phase="metaphase")
        out = rag_cli.acknowledgement_reply("ok i will do that", state)
        self.assertIsNotNone(out)
        self.assertIn("metaphase", out)

    def test_acknowledgement_reply_handles_thank_you_typo(self) -> None:
        state = rag_cli.SceneState(phase="metaphase")
        out = rag_cli.acknowledgement_reply("thannk!", state)
        self.assertIsNotNone(out)
        self.assertIn("metaphase", out)

    def test_acknowledgement_reply_varies_by_input(self) -> None:
        state = rag_cli.SceneState(phase="metaphase")
        a = rag_cli.acknowledgement_reply("ok", state)
        b = rag_cli.acknowledgement_reply("thank you", state)
        self.assertIsNotNone(a)
        self.assertIsNotNone(b)
        self.assertNotEqual(a, b)

    def test_acknowledgement_reply_none_for_content_question(self) -> None:
        state = rag_cli.SceneState(phase="anaphase")
        out = rag_cli.acknowledgement_reply("what is anaphase", state)
        self.assertIsNone(out)

    def test_acknowledgement_reply_none_for_ok_question(self) -> None:
        state = rag_cli.SceneState(phase="interphase")
        out = rag_cli.acknowledgement_reply("okay what should i be doing right now", state)
        self.assertIsNone(out)

    def test_greeting_reply(self) -> None:
        state = rag_cli.SceneState(phase="interphase")
        out = rag_cli.greeting_reply("hello!", state)
        self.assertIsNotNone(out)
        self.assertIn("interphase", out)

    def test_phase_status_reply(self) -> None:
        state = rag_cli.SceneState(phase="prophase")
        out = rag_cli.phase_status_reply("what phase am in", state)
        self.assertEqual(out, "You are in prophase.")

    def test_is_too_brief_or_filler(self) -> None:
        self.assertTrue(rag_cli.is_too_brief_or_filler("Exactly!"))
        self.assertTrue(rag_cli.is_too_brief_or_filler("Yes."))
        self.assertFalse(rag_cli.is_too_brief_or_filler("Line up the chromosomes at the center."))

    def test_extract_phase_action_sentence(self) -> None:
        retrieved = [
            (
                rag_cli.Chunk(
                    source="kb.md",
                    text=(
                        "In metaphase, you should move chromosomes to the center line. "
                        "Chromosomes align at the equator."
                    ),
                ),
                0.8,
            )
        ]
        out = rag_cli.extract_phase_action_sentence("metaphase", retrieved)
        self.assertEqual(out, "In metaphase, you should move chromosomes to the center line.")

    def test_extract_phase_action_sentence_skips_markdown_note_noise(self) -> None:
        retrieved = [
            (
                rag_cli.Chunk(
                    source="kb.md",
                    text=(
                        "## Mitosis vs Meiosis Note (For Comparison Questions) - In mitosis metaphase, individual chromosomes align at the metaphase plate. "
                        "In Metaphase, place the red X-shaped chromosome in the center line with the blue chromosomes."
                    ),
                ),
                0.8,
            )
        ]
        out = rag_cli.extract_phase_action_sentence("metaphase", retrieved)
        self.assertEqual(
            out,
            "In Metaphase, place the red X-shaped chromosome in the center line with the blue chromosomes.",
        )

    def test_extract_phase_action_sentence_avoids_meiosis_comparison(self) -> None:
        retrieved = [
            (
                rag_cli.Chunk(
                    source="mitosis_basics.md",
                    text="In meiosis I metaphase, homologous chromosome pairs align.",
                ),
                0.9,
            ),
            (
                rag_cli.Chunk(
                    source="scene_knowledge.md",
                    text="In Metaphase, line up the red X-shaped chromosome with the blue chromosomes at the center.",
                ),
                0.8,
            ),
        ]
        out = rag_cli.extract_phase_action_sentence("metaphase", retrieved)
        self.assertEqual(
            out,
            "In Metaphase, line up the red X-shaped chromosome with the blue chromosomes at the center.",
        )

    def test_extract_phase_action_sentence_strips_section_prefix(self) -> None:
        retrieved = [
            (
                rag_cli.Chunk(
                    source="scene_knowledge.md",
                    text=(
                        "Short guidance examples - In Metaphase, place the red X-shaped chromosome into the sparkly lineup area under the blue chromosomes."
                    ),
                ),
                0.8,
            )
        ]
        out = rag_cli.extract_phase_action_sentence("metaphase", retrieved)
        self.assertEqual(
            out,
            "In Metaphase, place the red X-shaped chromosome into the sparkly lineup area under the blue chromosomes.",
        )

    def test_simulation_coaching_reply_for_next_step_question(self) -> None:
        state = rag_cli.SceneState(phase="metaphase")
        retrieved = [
            (
                rag_cli.Chunk(
                    source="kb.md",
                    text="In metaphase, you should move chromosomes to the center line.",
                ),
                0.8,
            )
        ]
        out = rag_cli.simulation_coaching_reply("not sure what to do", state, retrieved)
        self.assertEqual(out, "In metaphase, you should move chromosomes to the center line.")

    def test_simulation_coaching_reply_for_confirmation_question(self) -> None:
        state = rag_cli.SceneState(phase="metaphase")
        retrieved = [
            (
                rag_cli.Chunk(
                    source="kb.md",
                    text="In metaphase, you should line up the chromosomes at the center.",
                ),
                0.8,
            )
        ]
        out = rag_cli.simulation_coaching_reply("so i just line them up?", state, retrieved)
        self.assertIsNotNone(out)
        self.assertTrue(out.lower().startswith("yes,"))

    def test_simulation_coaching_reply_for_how_question(self) -> None:
        state = rag_cli.SceneState(phase="anaphase")
        retrieved = [
            (
                rag_cli.Chunk(
                    source="scene_knowledge.md",
                    text="Grab each side of the red X-shaped chromosome and pull them apart toward the left and right.",
                ),
                0.8,
            )
        ]
        out = rag_cli.simulation_coaching_reply("how do i split the sister chromatids?", state, retrieved)
        self.assertEqual(out, "Grab each side of the red X-shaped chromosome and pull them apart toward the left and right.")

    def test_simulation_coaching_reply_for_repair_message(self) -> None:
        state = rag_cli.SceneState(phase="anaphase")
        retrieved = [
            (
                rag_cli.Chunk(
                    source="scene_knowledge.md",
                    text="In anaphase, split the red chromosome into two chromatids and place one on each side.",
                ),
                0.8,
            )
        ]
        out = rag_cli.simulation_coaching_reply("that didnt answer my questjion", state, retrieved)
        self.assertIsNotNone(out)
        self.assertTrue(out.lower().startswith("you're right"))
        self.assertIn("split the red chromosome", out.lower())

    def test_simulation_coaching_reply_repair_uses_actionable_fallback(self) -> None:
        state = rag_cli.SceneState(phase="anaphase")
        retrieved = [
            (
                rag_cli.Chunk(
                    source="mitosis_basics.md",
                    text="During anaphase, sister chromatids separate and each chromatid is considered an individual chromosome.",
                ),
                0.9,
            )
        ]
        out = rag_cli.simulation_coaching_reply("that didn't answer my question", state, retrieved)
        self.assertIsNotNone(out)
        self.assertIn("split the red chromosome", out.lower())

    def test_simulation_coaching_reply_for_confusion_message(self) -> None:
        state = rag_cli.SceneState(phase="interphase")
        retrieved = [
            (
                rag_cli.Chunk(
                    source="scene_knowledge.md",
                    text="In Interphase, place protein, magnesium, and vitamin C into the mitochondria to raise ATP.",
                ),
                0.8,
            )
        ]
        out = rag_cli.simulation_coaching_reply("i dont understand", state, retrieved)
        self.assertIsNotNone(out)
        self.assertTrue(out.lower().startswith("no worries,"))

    def test_parse_args_rejects_removed_offline_flag(self) -> None:
        with patch.object(sys, "argv", ["rag_cli.py", "--offline"]):
            with self.assertRaises(SystemExit):
                rag_cli.parse_args()

    def test_local_rag_index_retrieves_relevant_chunk(self) -> None:
        with tempfile.TemporaryDirectory() as tmp_dir:
            kb_file = Path(tmp_dir) / "sample.md"
            kb_file.write_text(
                "Metaphase aligns chromosomes at the center. Anaphase separates sister chromatids.",
                encoding="utf-8",
            )
            index = rag_cli.LocalRagIndex(kb_dir=Path(tmp_dir), chunk_size=200, overlap=20)
            index.build()

            retrieved = index.retrieve("where do chromosomes align in metaphase", top_k=1)
            self.assertGreaterEqual(len(retrieved), 1)
            self.assertIn("aligns chromosomes", retrieved[0][0].text.lower())
            self.assertGreater(retrieved[0][1], 0.0)


if __name__ == "__main__":
    unittest.main(verbosity=2)
