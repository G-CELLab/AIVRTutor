# AI Prototype (Text + RAG in Terminal)

This prototype is a terminal-only chatbot with lightweight local RAG.

## What it does
- Loads markdown files from `knowledge_base/`
- Chunks and ranks snippets with local TF-IDF retrieval
- Sends user query + top retrieved context to OpenAI
- Streams output token-by-token for lower perceived latency
- Prints latency metrics per turn
- Supports phase-aware tutoring with `/phase <name>` and `/status`
- Answers cross-phase questions faithfully (for example, asking about prophase while currently in anaphase)

## Setup
1. Open terminal in `AiPrototype`
2. Install dependencies:
   - `pip install -r requirements.txt`
3. Configure environment variables:
   - Copy `.env.example` values into your shell env
   - PowerShell example: `$env:OPENAI_API_KEY="..."`

## Run
- `python rag_cli.py`

In-chat commands:
- `/phase <name>` set current scene phase context
- `/status` show current phase/index status
- `/reindex` reload knowledge files
- `/exit` quit

Optional flags:
- `python rag_cli.py --top-k 3 --chunk-size 450 --model gpt-4o-mini`
- `python rag_cli.py --offline` (no OpenAI call, retrieval-only response)

## Knowledge base
Put your custom content in markdown files inside `knowledge_base/`.

## Goal for your next phase
Use this to tune retrieval speed and prompt shape before integrating into Unity.
