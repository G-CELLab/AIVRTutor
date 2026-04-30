"""
Python integration example for NewAI system.

This shows how to set up a Flask server that bridges Unity <-> Python RAG backend.
Run this alongside the main rag_cli.py for HTTP-based communication.

Usage:
    python rag_integration_server.py --port 5000 --kb-dir knowledge_base
"""

import json
import argparse
from pathlib import Path
from typing import Dict, List, Tuple

try:
    from flask import Flask, request, jsonify
except ImportError:
    raise SystemExit("Flask not installed. Run: pip install flask")

# Import the RAG system from the main CLI
from rag_cli import LocalRagIndex, SceneState, build_system_prompt, build_messages


app = Flask(__name__)

# Global state
rag_index = None
scene_states: Dict[str, SceneState] = {}  # Track scenes by session ID


@app.route('/health', methods=['GET'])
def health():
    """Health check endpoint"""
    return jsonify({
        "status": "healthy",
        "chunks_indexed": len(rag_index.chunks) if rag_index else 0
    })


@app.route('/retrieve', methods=['POST'])
def retrieve():
    """
    Retrieve chunks from knowledge base.
    
    Request JSON:
    {
        "query": "user query text",
        "top_k": 3,
        "session_id": "optional_session_id"
    }
    
    Response JSON:
    {
        "chunks": [
            {"source": "file.md", "text": "content...", "score": 0.85},
            ...
        ],
        "latency_ms": 12.5
    }
    """
    import time
    
    t0 = time.perf_counter()
    
    data = request.get_json() or {}
    query = data.get('query', '').strip()
    top_k = data.get('top_k', 3)
    session_id = data.get('session_id', 'default')
    
    if not query:
        return jsonify({"error": "Empty query"}), 400
    
    if not rag_index:
        return jsonify({"error": "RAG index not initialized"}), 500
    
    try:
        # Retrieve chunks
        retrieved = rag_index.retrieve(query, top_k=top_k)
        
        # Format response
        chunks = [
            {
                "source": chunk.source,
                "text": chunk.text,
                "score": score
            }
            for chunk, score in retrieved
        ]
        
        latency_ms = (time.perf_counter() - t0) * 1000.0
        
        return jsonify({
            "chunks": chunks,
            "latency_ms": latency_ms,
            "count": len(chunks)
        })
    
    except Exception as e:
        return jsonify({"error": str(e)}), 500


@app.route('/scene/update', methods=['POST'])
def update_scene():
    """
    Update scene state for a session.
    
    Request JSON:
    {
        "session_id": "session_123",
        "phase": "prophase",
        "visible_objects": "spindle fibers, chromosomes"
    }
    """
    data = request.get_json() or {}
    session_id = data.get('session_id', 'default')
    
    scene_state = scene_states.get(session_id, SceneState())
    
    if 'phase' in data:
        scene_state.phase = data['phase'].lower()
    if 'visible_objects' in data:
        scene_state.visible_objects = data['visible_objects']
    if 'scene_summary' in data:
        scene_state.scene_summary = data['scene_summary']
    if 'current_objective' in data:
        scene_state.current_objective = data['current_objective']
    
    scene_states[session_id] = scene_state
    
    return jsonify({
        "session_id": session_id,
        "phase": scene_state.phase,
        "status": "updated"
    })


@app.route('/scene/get', methods=['GET'])
def get_scene():
    """Get current scene state for a session"""
    session_id = request.args.get('session_id', 'default')
    scene_state = scene_states.get(session_id, SceneState())
    
    return jsonify(scene_state.to_dict())


@app.route('/reindex', methods=['POST'])
def reindex():
    """Rebuild the RAG index"""
    import time
    
    if not rag_index:
        return jsonify({"error": "RAG index not initialized"}), 500
    
    t0 = time.perf_counter()
    rag_index.build()
    elapsed_ms = (time.perf_counter() - t0) * 1000.0
    
    return jsonify({
        "chunks_indexed": len(rag_index.chunks),
        "reindex_time_ms": elapsed_ms,
        "status": "success"
    })


def main():
    parser = argparse.ArgumentParser(description="RAG HTTP Integration Server")
    parser.add_argument("--kb-dir", default="knowledge_base", help="Knowledge base directory")
    parser.add_argument("--port", type=int, default=5000, help="Flask server port")
    parser.add_argument("--host", default="127.0.0.1", help="Flask server host")
    parser.add_argument("--chunk-size", type=int, default=1500, help="Chunk size")
    parser.add_argument("--chunk-overlap", type=int, default=80, help="Chunk overlap")
    parser.add_argument("--debug", action="store_true", help="Enable Flask debug mode")
    
    args = parser.parse_args()
    
    # Initialize RAG index
    global rag_index
    kb_dir = Path(args.kb_dir)
    
    if not kb_dir.exists():
        print(f"ERROR: Knowledge base directory not found: {kb_dir}")
        return
    
    print(f"[init] Initializing RAG from {kb_dir}...")
    rag_index = LocalRagIndex(
        kb_dir=kb_dir,
        chunk_size=args.chunk_size,
        overlap=args.chunk_overlap
    )
    
    import time
    t0 = time.perf_counter()
    rag_index.build()
    build_ms = (time.perf_counter() - t0) * 1000.0
    
    print(f"[init] Indexed {len(rag_index.chunks)} chunks in {build_ms:.1f}ms")
    print(f"[init] Starting Flask server on {args.host}:{args.port}")
    
    # Start Flask server
    app.run(host=args.host, port=args.port, debug=args.debug, threaded=True)


if __name__ == '__main__':
    main()
