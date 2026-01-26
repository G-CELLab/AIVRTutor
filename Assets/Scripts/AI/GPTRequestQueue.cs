using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AI
{
    public class GPTRequestQueue
    {
        public enum RequestType { Text, AudioFile, AudioBytes }

        public class QueuedRequest
        {
            public RequestType Type;
            public string Text;
            public string AudioFilePath;
            public byte[] AudioBytes;
            public string AudioFormat;
            public Action OnComplete;
            public float QueuedTime; // Time when request was queued (Time.realtimeSinceStartup)
            public string TranscriptContext; // What the user said (for evaluation)

            /// <summary>
            /// Returns a summary of this request for AI evaluation
            /// </summary>
            public string GetSummary()
            {
                switch (Type)
                {
                    case RequestType.Text:
                        return !string.IsNullOrEmpty(Text) ? Text : "(empty text)";
                    case RequestType.AudioFile:
                    case RequestType.AudioBytes:
                        return !string.IsNullOrEmpty(TranscriptContext) ? TranscriptContext : "(audio input, no transcript)";
                    default:
                        return "(unknown request)";
                }
            }
        }

        private readonly Queue<QueuedRequest> _queue = new Queue<QueuedRequest>();

        public int Count => _queue.Count;

        /// <summary>
        /// Queue a request. If there's already a queued request, replace it with the latest.
        /// </summary>
        public void Enqueue(QueuedRequest req)
        {
            // Only keep the latest request - clear any existing
            _queue.Clear();
            _queue.Enqueue(req);
        }

        /// <summary>
        /// Peek at the next request without removing it
        /// </summary>
        public QueuedRequest Peek()
        {
            if (_queue.Count == 0) return null;
            return _queue.Peek();
        }

        public QueuedRequest Dequeue()
        {
            if (_queue.Count == 0) return null;
            return _queue.Dequeue();
        }

        public void Clear()
        {
            _queue.Clear();
        }
    }
}