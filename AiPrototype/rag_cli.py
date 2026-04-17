import argparse
import json
import math
import os
import re
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Tuple

from openai import OpenAI


WORD_RE = re.compile(r"[a-zA-Z0-9_]+")
PHASE_RE = re.compile(r"\b(interphase|prophase|metaphase|anaphase|telophase)\b", re.IGNORECASE)

PHASE_GUIDANCE = {
    "interphase": "The cell builds energy from nutrients and prepares for the next phase.",
    "prophase": "DNA condenses into chromosomes and the spindle starts forming.",
    "metaphase": "Chromosomes line up in the center.",
    "anaphase": "Sister chromatids separate toward opposite sides.",
    "telophase": "Chromosomes reach opposite sides and new nuclei reform.",
}

PHASE_NEXT_ACTION = {
    "interphase": "place protein, magnesium, and vitamin C into the mitochondria to raise ATP, then replicate the centriole.",
    "prophase": "hold the red DNA steady until it condenses into an X-shaped chromosome.",
    "metaphase": "move chromosomes to the center line and keep them aligned.",
    "anaphase": "pull chromosomes apart into two chromatids and move each to opposite yellow markers.",
    "telophase": "confirm chromosomes are at opposite ends and watch the nuclei reform.",
}


@dataclass
class Chunk:
    source: str
    text: str


@dataclass
class SceneState:
    phase: str = "interphase"
    scene_summary: str = "The learner is in the tutorial/learning simulation."
    current_objective: str = "Answer biology and mitosis questions conversationally."
    visible_objects: str = (
        "Nutrient capsules: three green capsules (protein, magnesium, vitamin C); "
        "Red DNA: on the left; "
        "Centriole: yellow barrel-shaped object on the right."
    )
    next_action: str = "If the learner asks what to do next, give a short step-by-step instruction."

    def summary(self) -> str:
        return (
            f"Phase: {self.phase}. "
            f"Scene: {self.scene_summary}. "
            f"Objective: {self.current_objective}. "
            f"Objects: {self.get_visible_objects()}. "
            f"Guidance: {self.next_action}"
        )

    def phase_guidance(self) -> str:
        phase = self.phase.lower().strip()
        return PHASE_GUIDANCE.get(phase, "The learner is in the mitosis simulation.")

    def next_action_guidance(self) -> str:
        phase = self.phase.lower().strip()
        return PHASE_NEXT_ACTION.get(phase, self.next_action)

    def get_visible_objects(self) -> str:
        """Return phase-specific object descriptions."""
        phase = self.phase.lower().strip()
        if phase == "interphase":
            return "Three green nutrient capsules (protein, magnesium, vitamin C); red DNA on the left; yellow barrel-shaped centriole on the right."
        elif phase == "prophase":
            return "Red stringy DNA: the object you are holding, which will condense into an X-shape."
        elif phase == "metaphase":
            return "Red X-shaped chromosome: in the center lineup area; Blue chromosomes: also in the center; Yellow spindle fibers: visible moving chromosomes."
        elif phase in ("anaphase", "telophase"):
            return "Chromatids: separated red chromosomes moving to opposite poles; Spindle fibers: yellow fibers supporting chromosome movement."
        else:
            return self.visible_objects

    def to_dict(self) -> dict:
        return {
            "phase": self.phase,
            "scene_summary": self.scene_summary,
            "current_objective": self.current_objective,
            "visible_objects": self.visible_objects,
            "next_action": self.next_action,
        }

    @classmethod
    def from_dict(cls, data: dict) -> "SceneState":
        return cls(
            phase=str(data.get("phase", "interphase")).strip().lower() or "interphase",
            scene_summary=str(data.get("scene_summary", "The learner is in the tutorial/learning simulation.")),
            current_objective=str(data.get("current_objective", "Answer biology and mitosis questions conversationally.")),
            visible_objects=str(data.get("visible_objects", "Nutrient capsules: three green capsules (protein, magnesium, vitamin C); Red DNA: on the left; Centriole: yellow barrel-shaped object on the right.")),
            next_action=str(data.get("next_action", "If the learner asks what to do next, give a short step-by-step instruction.")),
        )


class LocalRagIndex:
    def __init__(self, kb_dir: Path, chunk_size: int = 500, overlap: int = 80) -> None:
        self.kb_dir = kb_dir
        self.chunk_size = chunk_size
        self.overlap = max(0, min(overlap, chunk_size // 2))
        self.chunks: List[Chunk] = []
        self.doc_freq: Dict[str, int] = {}
        self.chunk_term_freqs: List[Dict[str, float]] = []

    def build(self) -> None:
        self.chunks.clear()
        self.doc_freq.clear()
        self.chunk_term_freqs.clear()

        files = sorted(self.kb_dir.glob("*.md"))
        for file_path in files:
            raw = file_path.read_text(encoding="utf-8", errors="ignore")
            for piece in self._chunk_text(raw):
                self.chunks.append(Chunk(source=file_path.name, text=piece))

        for chunk in self.chunks:
            tf = self._term_frequency(chunk.text)
            self.chunk_term_freqs.append(tf)
            for term in tf.keys():
                self.doc_freq[term] = self.doc_freq.get(term, 0) + 1

    def retrieve(self, query: str, top_k: int = 3) -> List[Tuple[Chunk, float]]:
        if not self.chunks:
            return []

        query_tf = self._term_frequency(query)
        if not query_tf:
            return []

        scored: List[Tuple[int, float]] = []
        qvec = self._tfidf(query_tf)
        qnorm = self._norm(qvec)
        if qnorm == 0:
            return []

        for i, chunk_tf in enumerate(self.chunk_term_freqs):
            cvec = self._tfidf(chunk_tf)
            denom = qnorm * self._norm(cvec)
            if denom == 0:
                continue
            sim = self._dot(qvec, cvec) / denom
            if sim > 0:
                scored.append((i, sim))

        scored.sort(key=lambda x: x[1], reverse=True)
        out: List[Tuple[Chunk, float]] = []
        for idx, score in scored[: max(1, top_k)]:
            out.append((self.chunks[idx], score))
        return out

    def _chunk_text(self, text: str) -> List[str]:
        normalized = re.sub(r"\s+", " ", text).strip()
        if not normalized:
            return []

        chunks: List[str] = []
        start = 0
        n = len(normalized)
        step = max(1, self.chunk_size - self.overlap)
        while start < n:
            end = min(n, start + self.chunk_size)
            chunks.append(normalized[start:end].strip())
            if end >= n:
                break
            start += step
        return chunks

    def _term_frequency(self, text: str) -> Dict[str, float]:
        tokens = [m.group(0).lower() for m in WORD_RE.finditer(text)]
        if not tokens:
            return {}
        tf: Dict[str, float] = {}
        for t in tokens:
            tf[t] = tf.get(t, 0.0) + 1.0
        total = float(len(tokens))
        for t in list(tf.keys()):
            tf[t] = tf[t] / total
        return tf

    def _tfidf(self, tf: Dict[str, float]) -> Dict[str, float]:
        n_docs = max(1, len(self.chunks))
        out: Dict[str, float] = {}
        for term, freq in tf.items():
            df = self.doc_freq.get(term, 0)
            idf = math.log((1 + n_docs) / (1 + df)) + 1.0
            out[term] = freq * idf
        return out

    @staticmethod
    def _dot(a: Dict[str, float], b: Dict[str, float]) -> float:
        if len(a) > len(b):
            a, b = b, a
        return sum(v * b.get(k, 0.0) for k, v in a.items())

    @staticmethod
    def _norm(v: Dict[str, float]) -> float:
        return math.sqrt(sum(x * x for x in v.values()))


def summarize_history(history: List[dict], max_turns: int = 6, max_chars: int = 500) -> str:
    if not history:
        return ""
    tail = history[-max(0, max_turns * 2):]
    lines: List[str] = []
    for turn in tail:
        role = turn.get("role", "user")
        text = str(turn.get("content", "")).strip()
        if not text:
            continue
        short = text if len(text) <= 100 else text[:100] + "..."
        lines.append(f"{role}: {short}")
    summary = " | ".join(lines)
    if len(summary) > max_chars:
        return summary[-max_chars:]
    return summary


def rewrite_query(query: str, history_summary: str) -> str:
    q = (query or "").strip()
    if not q:
        return ""

    # Lightweight rewrite for pronoun-heavy short follow-ups.
    if history_summary and len(q) <= 40 and any(p in q.lower() for p in ("it", "that", "this", "why", "how")):
        return f"Recent context: {history_summary}\nCurrent user query: {q}"
    return q


def request_clarification(query: str) -> str | None:
    q = (query or "").strip()
    if len(q) < 2:
        return "Could you say that one more time in a few words?"
    if q.lower() in {"what", "huh", "help", "idk"}:
        return "Could you be a little more specific about what you want to know?"
    return None


def should_compress_context(retrieved: List[Tuple[Chunk, float]], char_budget: int) -> bool:
    total = 0
    for chunk, _ in retrieved:
        total += len(chunk.text)
    return total > max(100, char_budget)


def compress_context(retrieved: List[Tuple[Chunk, float]], char_budget: int) -> List[Tuple[Chunk, float]]:
    out: List[Tuple[Chunk, float]] = []
    used = 0
    for chunk, score in retrieved:
        if used >= char_budget:
            break
        remain = char_budget - used
        if remain <= 0:
            break
        text = chunk.text
        if len(text) > remain:
            text = text[:remain].rstrip()
        out.append((Chunk(source=chunk.source, text=text), score))
        used += len(text)
    return out


def is_out_of_scope_query(retrieved: List[Tuple[Chunk, float]]) -> bool:
    """
    Return True if the top retrieval is general biology (mitosis_basics.md)
    with a high confidence score (>0.25), indicating the query is well-answered
    by textbook content and out of scope for simulation guidance.
    """
    if not retrieved:
        return False
    top_chunk, top_score = retrieved[0]
    return top_chunk.source == "mitosis_basics.md" and top_score > 0.25


def build_messages(
    system_prompt: str,
    query: str,
    retrieved: List[Tuple[Chunk, float]],
    scene_state: SceneState,
    history_summary: str,
) -> List[dict]:
    context_lines: List[str] = []
    for i, (chunk, score) in enumerate(retrieved, start=1):
        context_lines.append(f"[{i}] {chunk.text}")

    context_block = "\n\n".join(context_lines) if context_lines else ""

    user_msg = (
        f"Current simulation state: {scene_state.summary()} "
        f"Recent conversation summary: {history_summary or '(none)'}. "
        "Answer faithfully even when the user asks about a different phase. "
        "If the user statement clearly updates the phase, trust the latest user state. "
        "If the user asks what to do next, use the scene state's next action guidance rather than inventing a tutorial step. "
        "Do not mention a tutorial hand sphere unless the scene is actually in the tutorial phase. "
        "Use retrieved context when relevant, but keep the reply natural and concise.\n\n"
        f"Retrieved context:\n{context_block}\n\n"
        f"User question: {query}"
    )

    return [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": user_msg},
    ]


def stream_openai_response(
    client: OpenAI,
    model: str,
    messages: List[dict],
    max_output_tokens: int,
) -> Tuple[str, float]:
    t0 = time.perf_counter()
    first_token_ms = -1.0
    chunks: List[str] = []

    stream = client.chat.completions.create(
        model=model,
        messages=messages,
        temperature=0.2,
        max_tokens=max(20, max_output_tokens),
        stream=True,
    )

    for event in stream:
        delta = event.choices[0].delta.content if event.choices and event.choices[0].delta else None
        if not delta:
            continue
        if first_token_ms < 0:
            first_token_ms = (time.perf_counter() - t0) * 1000.0
        print(delta, end="", flush=True)
        chunks.append(delta)

    print()
    return "".join(chunks), first_token_ms


def offline_answer(query: str, retrieved: List[Tuple[Chunk, float]], scene_state: SceneState) -> str:
    direct = respond_from_scene_state(query, scene_state)
    if direct:
        return f"[OFFLINE MODE] {direct}"

    if not retrieved:
        return f"[OFFLINE MODE] phase={scene_state.phase}. I need more local knowledge to answer that."

    return f"[OFFLINE MODE] phase={scene_state.phase}. Best match found for: {query}."


def build_system_prompt(scene_state: SceneState) -> str:
    return (
        "You are a friendly biology tutor for a VR mitosis simulation. "
        f"The learner is currently in phase: {scene_state.phase}. "
        "Use a warm, encouraging tone. "
        "Answer clearly and naturally. If asked about any phase, answer it faithfully. "
        "If the learner states a new phase, use that latest phase. "
        "When asked what to do next, give guidance based on the current phase and scene state. "
        "Simulation rule: for Interphase, use nutrient/ATP/centriole language, not DNA replication language, unless the learner explicitly asks for textbook biology. "
        "Do not invent a tutorial sphere unless the scene state indicates the tutorial phase. "
        "Use scene object cues only when relevant. "
        "Style rules for spoken output: use 1-2 short sentences. Keep replies under 35 words when possible. "
        "For instructions, use one direct action sentence. Avoid filler openings like 'Sure' or 'Okay'."
    )


def respond_from_scene_state(query: str, scene_state: SceneState) -> str | None:
    text = (query or "").strip().lower()
    phase = scene_state.phase.lower().strip()

    if not text:
        return None

    if any(phrase in text for phrase in ("what phase am i in", "what phase is this", "what phase are we in", "which phase am i in")):
        return f"You are in {phase}. {scene_state.phase_guidance()}"

    if phase == "interphase" and any(
        phrase in text for phrase in (
            "tell me about interphase",
            "what is interphase",
            "what happens in interphase",
            "explain interphase",
        )
    ):
        return "In Interphase, you place protein, magnesium, and vitamin C into the mitochondria to raise ATP, then replicate the yellow centriole."

    if any(phrase in text for phrase in ("is this offline", "are you offline", "offline mode", "am i offline")):
        return "I can answer scene-state questions instantly, but I cannot tell mode from this prompt alone. Check /status to see whether offline is true or false."

    if any(phrase in text for phrase in ("which ones are they", "what objects are here", "what objects do i see", "which objects are here")):
        return f"In {phase}, the objects are: {scene_state.get_visible_objects()}"

    if any(phrase in text for phrase in ("what should i do next", "what do i do next", "what next", "next step", "next")):
        return f"In {phase}, you should {scene_state.next_action_guidance()}"

    if ("hold it" in text or "just need to hold" in text) and "why" in text:
        if phase == "prophase":
            return "You do. Holding it steady lets the DNA condense into a stable X-shaped chromosome for the next step."
        return "Holding it steady helps the phase action complete correctly before moving on."

    if "what sphere" in text or "which sphere" in text or text == "sphere" or "sphere on my hand" in text:
        if phase == "tutorial":
            return "The sphere is the hand touch sphere used for tutorial practice."
        return f"There is no tutorial hand sphere in {phase}. Focus on the phase objects: {scene_state.get_visible_objects()}"

    if "what is prophase" in text:
        return "Prophase is when chromatin condenses into visible chromosomes, the spindle starts forming, and the nuclear envelope begins breaking down."

    if phase == "prophase" and any(phrase in text for phrase in ("what is this", "what am i holding", "what is that")):
        return "You're holding the red DNA. Keep it steady—it will transform into a red X-shaped chromosome."

    if "what is metaphase" in text:
        return "Metaphase is when chromosomes line up at the center of the cell on the metaphase plate."

    if "what is anaphase" in text:
        return "Anaphase is when sister chromatids separate and move toward opposite poles."

    if "what is telophase" in text:
        return "Telophase is when chromosomes reach opposite poles and new nuclei begin to reform."

    return None


def load_scene_state(path: Path) -> SceneState:
    if not path.exists():
        return SceneState()
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
        if isinstance(raw, dict):
            return SceneState.from_dict(raw)
    except Exception:
        pass
    return SceneState()


def save_scene_state(path: Path, scene_state: SceneState) -> None:
    try:
        path.write_text(json.dumps(scene_state.to_dict(), indent=2), encoding="utf-8")
    except Exception:
        pass


def infer_phase_from_text(text: str) -> str | None:
    match = PHASE_RE.search(text or "")
    if not match:
        return None
    return match.group(1).lower()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Text-based RAG prototype for AIVRTutor")
    parser.add_argument("--kb-dir", default="knowledge_base", help="Directory with .md knowledge files")
    parser.add_argument("--top-k", type=int, default=3, help="Number of retrieved chunks")
    parser.add_argument("--chunk-size", type=int, default=500, help="Chunk size in characters")
    parser.add_argument("--chunk-overlap", type=int, default=80, help="Chunk overlap in characters")
    parser.add_argument("--model", default=os.getenv("OPENAI_MODEL", "gpt-4o-mini"), help="OpenAI model")
    parser.add_argument("--offline", action="store_true", help="Run retrieval-only mode (no OpenAI call)")
    parser.add_argument("--show-context", action="store_true", help="Print retrieved context previews for debugging")
    parser.add_argument("--scene-file", default="scene_state.json", help="JSON file that stores the current scene state")
    parser.add_argument("--max-output-tokens", type=int, default=80, help="Cap model output length for shorter spoken replies")
    parser.add_argument("--history-turns", type=int, default=6, help="Number of recent turns to summarize")
    parser.add_argument("--context-char-budget", type=int, default=900, help="Character budget before retrieval context compression")
    parser.add_argument("--trace-flow", action="store_true", help="Print orchestrator flow steps for debugging")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    kb_dir = Path(args.kb_dir)

    if not kb_dir.exists():
        raise SystemExit(f"KB directory not found: {kb_dir}")

    index = LocalRagIndex(kb_dir=kb_dir, chunk_size=args.chunk_size, overlap=args.chunk_overlap)

    t_build0 = time.perf_counter()
    index.build()
    build_ms = (time.perf_counter() - t_build0) * 1000.0
    print(f"[init] indexed {len(index.chunks)} chunks from {kb_dir} in {build_ms:.1f}ms")

    offline = args.offline
    client = None
    scene_file = Path(args.scene_file)
    scene_state = load_scene_state(scene_file)
    conversation_history: List[dict] = []

    if not offline:
        api_key = ""  # os.getenv("OPENAI_API_KEY", "")
        if not api_key:
            print("[warn] OPENAI_API_KEY is missing. Switching to --offline mode.")
            offline = True
        else:
            client = OpenAI(api_key=api_key)

    system_prompt = build_system_prompt(scene_state)

    print("\nType your question. Commands: /phase <name>, /scene <text>, /status, /reindex, /exit\n")

    while True:
        try:
            user_query = input("you> ").strip()
        except (EOFError, KeyboardInterrupt):
            print("\nbye")
            break

        if not user_query:
            continue

        if user_query.lower() in {"/exit", "exit", "quit"}:
            print("bye")
            break

        if user_query.lower() == "/reindex":
            t0 = time.perf_counter()
            index.build()
            ms = (time.perf_counter() - t0) * 1000.0
            print(f"[init] reindexed {len(index.chunks)} chunks in {ms:.1f}ms")
            continue

        if user_query.lower() == "/status":
            print(f"[status] phase={scene_state.phase} | chunks={len(index.chunks)} | offline={offline}")
            print(f"[status] scene={scene_state.scene_summary}")
            continue

        if user_query.lower().startswith("/phase "):
            new_phase = user_query[7:].strip().lower()
            if not new_phase:
                print("[status] usage: /phase <interphase|prophase|metaphase|anaphase|telophase|cytokinesis|tutorial>")
                continue
            scene_state.phase = new_phase
            scene_state.scene_summary = f"The learner is in the {new_phase} stage of the mitosis simulation."
            scene_state.current_objective = f"Answer questions about {new_phase} and guide the learner in the current scene."
            scene_state.next_action = PHASE_NEXT_ACTION.get(new_phase, scene_state.next_action)
            system_prompt = build_system_prompt(scene_state)
            save_scene_state(scene_file, scene_state)
            print(f"[status] current phase updated to: {scene_state.phase}")
            continue

        if user_query.lower().startswith("/scene "):
            scene_state.scene_summary = user_query[7:].strip() or scene_state.scene_summary
            system_prompt = build_system_prompt(scene_state)
            save_scene_state(scene_file, scene_state)
            print("[status] scene summary updated")
            continue

        inferred_phase = infer_phase_from_text(user_query)
        if inferred_phase:
            scene_state.phase = inferred_phase
            scene_state.scene_summary = f"The learner is in the {inferred_phase} stage of the mitosis simulation."
            scene_state.current_objective = f"Answer questions about {inferred_phase} and guide the learner in the current scene."
            scene_state.next_action = PHASE_NEXT_ACTION.get(inferred_phase, scene_state.next_action)
            system_prompt = build_system_prompt(scene_state)
            save_scene_state(scene_file, scene_state)

        # ===== Orchestrator flow =====
        history_summary = summarize_history(conversation_history, max_turns=args.history_turns)
        rewritten_query = rewrite_query(user_query, history_summary)
        clarification = request_clarification(rewritten_query)
        if args.trace_flow:
            print("[flow] summarize_history -> rewrite_query -> request_clarification")

        if clarification:
            print("assistant> ", end="", flush=True)
            print(clarification)
            print("[latency] retrieval=0.0ms | first_token=0.0ms | total=0.0ms")
            print()
            conversation_history.append({"role": "user", "content": user_query})
            conversation_history.append({"role": "assistant", "content": clarification})
            continue

        # Use the raw user message for deterministic routing so history-rewrite text
        # does not accidentally trigger the wrong fast-path handler.
        direct_answer = respond_from_scene_state(user_query, scene_state)
        if direct_answer:
            print("assistant> ", end="", flush=True)
            print(direct_answer)
            print("[latency] retrieval=0.0ms | first_token=0.0ms | total=0.0ms")
            print()
            conversation_history.append({"role": "user", "content": user_query})
            conversation_history.append({"role": "assistant", "content": direct_answer})
            continue

        t_retrieve0 = time.perf_counter()
        retrieved = index.retrieve(rewritten_query, top_k=args.top_k)
        retrieve_ms = (time.perf_counter() - t_retrieve0) * 1000.0

        if should_compress_context(retrieved, args.context_char_budget):
            retrieved = compress_context(retrieved, args.context_char_budget)
            if args.trace_flow:
                print("[flow] should_compress_context -> compress_context")

        # Check if the query is general biology (out of scope)
        if is_out_of_scope_query(retrieved):
            out_of_scope_msg = "That's general biology and out of scope for this simulation. Focus on what to do next in the scene instead."
            print("assistant> ", end="", flush=True)
            print(out_of_scope_msg)
            print(f"[latency] retrieval={retrieve_ms:.1f}ms | first_token=0.0ms | total={retrieve_ms:.1f}ms")
            print()
            conversation_history.append({"role": "user", "content": user_query})
            conversation_history.append({"role": "assistant", "content": out_of_scope_msg})
            continue

        if args.show_context:
            print(f"[debug] retrieval: {retrieve_ms:.1f}ms, hits={len(retrieved)}")
            for i, (chunk, score) in enumerate(retrieved, start=1):
                preview = chunk.text[:120].replace("\n", " ")
                print(f"  [{i}] {chunk.source} score={score:.3f} preview={preview}")

        print("assistant> ", end="", flush=True)
        answer = ""

        if offline:
            t0 = time.perf_counter()
            answer = offline_answer(rewritten_query, retrieved, scene_state)
            first_token_ms = (time.perf_counter() - t0) * 1000.0
            print(answer)
            total_ms = (time.perf_counter() - t0) * 1000.0
        else:
            assert client is not None
            messages = build_messages(system_prompt, rewritten_query, retrieved, scene_state, history_summary)
            t0 = time.perf_counter()
            try:
                answer, first_token_ms = stream_openai_response(
                    client,
                    args.model,
                    messages,
                    args.max_output_tokens,
                )
                total_ms = (time.perf_counter() - t0) * 1000.0
            except Exception as e:
                print(f"[error] OpenAI call failed: {e}")
                print("[hint] Set OPENAI_API_KEY or run with --offline")
                total_ms = (time.perf_counter() - t0) * 1000.0
                first_token_ms = -1.0
                answer = "I hit a connection issue. Please try again."

        print(
            f"[latency] retrieval={retrieve_ms:.1f}ms | first_token={first_token_ms:.1f}ms | total={total_ms:.1f}ms"
        )
        print()

        conversation_history.append({"role": "user", "content": user_query})
        conversation_history.append({"role": "assistant", "content": answer})


if __name__ == "__main__":
    main()
