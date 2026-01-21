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

            public async Task ExecuteAsync()
            {
                try
                {
                    switch (Type)
                    {
                        case RequestType.Text:
                            // Handle text request
                            break;
                        case RequestType.AudioFile:
                            // Handle audio file request
                            break;
                        case RequestType.AudioBytes:
                            // Handle audio bytes request
                            break;
                    }
                }
                finally
                {
                    OnComplete?.Invoke();
                }
            }
        }

        private readonly Queue<QueuedRequest> _queue = new Queue<QueuedRequest>();
        private bool isProcessing = false;

        public int Count => _queue.Count;

        public void Enqueue(QueuedRequest req)
        {
            _queue.Enqueue(req);
            ProcessQueue();
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

        private async void ProcessQueue()
        {
            if (isProcessing || _queue.Count == 0)
                return;

            isProcessing = true;

            while (_queue.Count > 0)
            {
                QueuedRequest currentRequest = _queue.Dequeue();
                await currentRequest.ExecuteAsync();
            }

            isProcessing = false;
        }
    }
}