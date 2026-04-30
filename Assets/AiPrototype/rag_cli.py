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
    next_action: str = "If the learner asks what to do next, consult the knowledge base for current phase guidance."

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
            next_action=str(data.get("next_action", "If the learner asks what to do next, consult the knowledge base for current phase guidance.")),
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


def request_clarification(query: str) -> str | None:
    q = (query or "").strip()
    if len(q) < 2:
        return "Could you say that one more time in a few words?"
    if q.lower() in {"what", "huh", "help", "idk"}:
        return "Could you be a little more specific about what you want to know?"
    return None


def build_messages(
    system_prompt: str,
    query: str,
    retrieved: List[Tuple[Chunk, float]],
    scene_state: SceneState,
) -> List[dict]:
    """Build messages for OpenAI call with scene context and retrieved KB chunks."""
    context_lines: List[str] = []
    for i, (chunk, score) in enumerate(retrieved, start=1):
        context_lines.append(f"[{i}] {chunk.text}")

    context_block = "\n\n".join(context_lines) if context_lines else "(no relevant knowledge base entries found)"

    user_msg = (
        f"Simulation state: phase={scene_state.phase}. Scene: {scene_state.scene_summary}. "
        f"Visible objects: {scene_state.visible_objects}.\n\n"
        f"Knowledge base:\n{context_block}\n\n"
        f"User: {query}"
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


def build_system_prompt(scene_state: SceneState) -> str:
    return (
        "You are a friendly biology tutor for a VR mitosis simulation. "
        f"The learner is currently in the {scene_state.phase} phase. "
        "Use a warm, encouraging, and natural tone. Answer biology questions clearly and accurately. "
        "Use the knowledge base (retrieved context) for detailed information about mitosis, cell biology, and what the learner should do next in the scene. "
        "If the learner asks what to do, guide them based on the knowledge base instructions for their current phase. "
        "Keep replies concise and conversational—aim for 1-2 sentences when possible. "
        "Never invent instructions or scene mechanics not in the knowledge base."
    )



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
    parser.add_argument("--chunk-size", type=int, default=1500, help="Chunk size in characters")
    parser.add_argument("--chunk-overlap", type=int, default=80, help="Chunk overlap in characters")
    parser.add_argument("--model", default=os.getenv("OPENAI_MODEL", "gpt-4o-mini"), help="OpenAI model")
    parser.add_argument("--show-context", action="store_true", help="Print retrieved context previews for debugging")
    parser.add_argument("--scene-file", default="scene_state.json", help="JSON file that stores the current scene state")
    parser.add_argument("--max-output-tokens", type=int, default=80, help="Cap model output length for shorter spoken replies")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    kb_dir = Path(args.kb_dir)

    if not kb_dir.exists():
        raise SystemExit(f"KB directory not found: {kb_dir}")

    # Build index
    index = LocalRagIndex(kb_dir=kb_dir, chunk_size=args.chunk_size, overlap=args.chunk_overlap)
    t_build0 = time.perf_counter()
    index.build()
    build_ms = (time.perf_counter() - t_build0) * 1000.0
    print(f"[init] indexed {len(index.chunks)} chunks from {kb_dir} in {build_ms:.1f}ms")

    # Setup OpenAI client
    api_key = ""
    if not api_key:
        raise SystemExit("[error] OPENAI_API_KEY is missing. Please set it in the environment.")
    client = OpenAI(api_key=api_key)

    # Load scene state
    scene_file = Path(args.scene_file)
    scene_state = load_scene_state(scene_file)
    conversation_history: List[dict] = []

    print("\nType your question. Commands: /phase <name>, /scene <text>, /status, /reindex, /exit\n")

    while True:
        try:
            user_query = input("you> ").strip()
        except (EOFError, KeyboardInterrupt):
            print("\nbye")
            break

        if not user_query:
            continue

        # Handle exit commands
        if user_query.lower() in {"/exit", "exit", "quit"}:
            print("bye")
            break

        # Handle reindex command
        if user_query.lower() == "/reindex":
            t0 = time.perf_counter()
            index.build()
            ms = (time.perf_counter() - t0) * 1000.0
            print(f"[init] reindexed {len(index.chunks)} chunks in {ms:.1f}ms")
            continue

        # Handle status command
        if user_query.lower() == "/status":
            print(f"[status] phase={scene_state.phase} | chunks={len(index.chunks)}")
            print(f"[status] scene={scene_state.scene_summary}")
            continue

        # Handle phase command
        if user_query.lower().startswith("/phase "):
            new_phase = user_query[7:].strip().lower()
            if not new_phase:
                print("[status] usage: /phase <interphase|prophase|metaphase|anaphase|telophase|cytokinesis|tutorial>")
                continue
            scene_state.phase = new_phase
            scene_state.scene_summary = f"The learner is in the {new_phase} stage of the mitosis simulation."
            save_scene_state(scene_file, scene_state)
            print(f"[status] current phase updated to: {scene_state.phase}")
            continue

        # Handle scene command
        if user_query.lower().startswith("/scene "):
            scene_state.scene_summary = user_query[7:].strip() or scene_state.scene_summary
            save_scene_state(scene_file, scene_state)
            print("[status] scene summary updated")
            continue

        # Infer phase from user query if it mentions one
        inferred_phase = infer_phase_from_text(user_query)
        if inferred_phase:
            scene_state.phase = inferred_phase
            scene_state.scene_summary = f"The learner is in the {inferred_phase} stage of the mitosis simulation."
            save_scene_state(scene_file, scene_state)

        # Check for clarification needed
        clarification = request_clarification(user_query)
        if clarification:
            print("assistant> ", end="", flush=True)
            print(clarification)
            print("[latency] retrieval=0.0ms | first_token=0.0ms | total=0.0ms")
            print()
            conversation_history.append({"role": "user", "content": user_query})
            conversation_history.append({"role": "assistant", "content": clarification})
            continue

        # Retrieve relevant knowledge base chunks (phase-aware)
        t_retrieve0 = time.perf_counter()
        # Always boost by current phase to prioritize phase-specific content
        phase_boosted_query = f"{scene_state.phase} {user_query}"
        retrieved = index.retrieve(phase_boosted_query, top_k=args.top_k)
        retrieve_ms = (time.perf_counter() - t_retrieve0) * 1000.0

        if args.show_context:
            print(f"[debug] retrieval: {retrieve_ms:.1f}ms, hits={len(retrieved)}")
            for i, (chunk, score) in enumerate(retrieved, start=1):
                preview = chunk.text[:120].replace("\n", " ")
                print(f"  [{i}] {chunk.source} score={score:.3f} preview={preview}")

        # Build system prompt
        system_prompt = build_system_prompt(scene_state)

        # Build and send to OpenAI
        print("assistant> ", end="", flush=True)
        messages = build_messages(system_prompt, user_query, retrieved, scene_state)
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
