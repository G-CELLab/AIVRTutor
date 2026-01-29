using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AI.Performance
{
    /// <summary>
    /// Async audio processor that handles all audio encoding/decoding operations
    /// off the main thread to prevent XR frame drops.
    /// 
    /// Key operations moved to background:
    /// - Base64 encoding/decoding
    /// - PCM16 to WAV conversion
    /// - WAV to PCM16 extraction
    /// - File I/O
    /// </summary>
    public static class AsyncAudioProcessor
    {
        /// <summary>
        /// Convert PCM16 bytes to WAV format on background thread
        /// </summary>
        public static void BuildWavFromPcm16Async(byte[] pcm, int sampleRate, int channels, Action<byte[]> onComplete)
        {
            if (pcm == null || pcm.Length == 0)
            {
                onComplete?.Invoke(null);
                return;
            }
            
            ThreadPoolDispatcher.Instance.RunAsync(
                () => BuildWavFromPcm16(pcm, sampleRate, channels),
                onComplete
            );
        }
        
        /// <summary>
        /// Synchronous WAV building (call from background thread)
        /// </summary>
        public static byte[] BuildWavFromPcm16(byte[] pcm, int sampleRate, int channels)
        {
            if (pcm == null) pcm = Array.Empty<byte>();
            
            int byteRate = sampleRate * channels * 2;
            int blockAlign = channels * 2;
            int subchunk2Size = pcm.Length;
            int chunkSize = 36 + subchunk2Size;
            
            // Use pooled memory stream
            var ms = MemoryStreamPool.Instance.Get(44 + subchunk2Size);
            try
            {
                using (var bw = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true))
                {
                    bw.Write(Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(chunkSize);
                    bw.Write(Encoding.ASCII.GetBytes("WAVE"));
                    bw.Write(Encoding.ASCII.GetBytes("fmt "));
                    bw.Write(16);
                    bw.Write((short)1);
                    bw.Write((short)channels);
                    bw.Write(sampleRate);
                    bw.Write(byteRate);
                    bw.Write((short)blockAlign);
                    bw.Write((short)16);
                    bw.Write(Encoding.ASCII.GetBytes("data"));
                    bw.Write(subchunk2Size);
                    bw.Write(pcm);
                }
                
                return ms.ToArray();
            }
            finally
            {
                MemoryStreamPool.Instance.Return(ms);
            }
        }
        
        /// <summary>
        /// Extract PCM16 from WAV on background thread
        /// </summary>
        public static void ExtractPcm16FromWavAsync(byte[] wav, Action<byte[]> onComplete)
        {
            if (wav == null || wav.Length < 44)
            {
                onComplete?.Invoke(null);
                return;
            }
            
            ThreadPoolDispatcher.Instance.RunAsync(
                () => ExtractPcm16FromWavRobust(wav),
                onComplete
            );
        }
        
        /// <summary>
        /// Robust WAV parsing that handles variable chunk sizes
        /// </summary>
        public static byte[] ExtractPcm16FromWavRobust(byte[] wav)
        {
            if (wav == null || wav.Length < 12) return null;
            
            try
            {
                // Check RIFF header
                if (wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F') return null;
                if (wav[8] != 'W' || wav[9] != 'A' || wav[10] != 'V' || wav[11] != 'E') return null;
                
                int dataPos = -1;
                int dataSize = 0;
                int i = 12;
                
                while (i + 8 <= wav.Length)
                {
                    char c0 = (char)wav[i];
                    char c1 = (char)wav[i + 1];
                    char c2 = (char)wav[i + 2];
                    char c3 = (char)wav[i + 3];
                    string chunkId = new string(new[] { c0, c1, c2, c3 });
                    i += 4;
                    
                    if (i + 4 > wav.Length) break;
                    int chunkSize = wav[i] | (wav[i + 1] << 8) | (wav[i + 2] << 16) | (wav[i + 3] << 24);
                    i += 4;
                    
                    if (chunkId == "data")
                    {
                        dataPos = i;
                        dataSize = chunkSize;
                        break;
                    }
                    
                    i += chunkSize;
                    if ((chunkSize & 1) == 1) i++;
                }
                
                if (dataPos >= 0 && dataSize > 0)
                {
                    int pcmLen = Math.Min(dataSize, wav.Length - dataPos);
                    if (pcmLen > 0)
                    {
                        byte[] pcm = new byte[pcmLen];
                        Buffer.BlockCopy(wav, dataPos, pcm, 0, pcmLen);
                        return pcm;
                    }
                }
                
                // Fallback: 44-byte header
                if (wav.Length > 44)
                {
                    byte[] pcm = new byte[wav.Length - 44];
                    Buffer.BlockCopy(wav, 44, pcm, 0, pcm.Length);
                    return pcm;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// Encode bytes to base64 on background thread
        /// </summary>
        public static void EncodeBase64Async(byte[] data, Action<string> onComplete)
        {
            ThreadPoolDispatcher.Instance.EncodeBase64Async(data, onComplete);
        }
        
        /// <summary>
        /// Decode base64 on background thread
        /// </summary>
        public static void DecodeBase64Async(string base64, Action<byte[]> onComplete)
        {
            ThreadPoolDispatcher.Instance.DecodeBase64Async(base64, onComplete);
        }
        
        /// <summary>
        /// Read file and convert to base64 on background thread
        /// </summary>
        public static void ReadFileAsBase64Async(string path, Action<string> onComplete)
        {
            ThreadPoolDispatcher.Instance.RunAsync(
                () =>
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    return Convert.ToBase64String(bytes);
                },
                onComplete
            );
        }
        
        /// <summary>
        /// Read WAV file and extract PCM16 on background thread
        /// </summary>
        public static void ReadWavFileAsPcm16Async(string path, Action<byte[]> onComplete)
        {
            ThreadPoolDispatcher.Instance.RunAsync(
                () =>
                {
                    byte[] wav = File.ReadAllBytes(path);
                    return ExtractPcm16FromWavRobust(wav);
                },
                onComplete
            );
        }
        
        /// <summary>
        /// Write WAV file from PCM16 on background thread
        /// </summary>
        public static void WritePcm16AsWavFileAsync(string path, byte[] pcm16, int sampleRate, int channels, Action onComplete = null)
        {
            ThreadPoolDispatcher.Instance.RunAsync(
                () =>
                {
                    byte[] wav = BuildWavFromPcm16(pcm16, sampleRate, channels);
                    File.WriteAllBytes(path, wav);
                },
                onComplete
            );
        }
        
        /// <summary>
        /// Process audio response: decode base64, convert to WAV
        /// </summary>
        public static void ProcessAudioResponseAsync(string base64Pcm, int sampleRate, int channels, Action<byte[]> onWavComplete)
        {
            DecodeBase64Async(base64Pcm, pcmBytes =>
            {
                if (pcmBytes == null || pcmBytes.Length == 0)
                {
                    onWavComplete?.Invoke(null);
                    return;
                }
                
                BuildWavFromPcm16Async(pcmBytes, sampleRate, channels, onWavComplete);
            });
        }
    }
    
    /// <summary>
    /// Async JSON helpers optimized for Unity/XR
    /// Uses pooled StringBuilders and background processing
    /// </summary>
    public static class AsyncJsonHelper
    {
        /// <summary>
        /// Build JSON messages on background thread
        /// </summary>
        public static void BuildMessagesJsonAsync(
            string systemPrompt,
            System.Collections.Generic.List<(string role, string content)> history,
            string userInput,
            int maxTurns,
            Action<string> onComplete)
        {
            ThreadPoolDispatcher.Instance.RunAsync(() =>
            {
                var sb = StringBuilderPool.Instance.Get(2048);
                try
                {
                    sb.Append("[");
                    bool first = true;
                    
                    if (!string.IsNullOrEmpty(systemPrompt))
                    {
                        AppendMsg(sb, "system", systemPrompt, ref first);
                    }
                    
                    int start = Math.Max(0, history.Count - 2 * Math.Max(1, maxTurns));
                    for (int i = start; i < history.Count; i++)
                    {
                        AppendMsg(sb, history[i].role, history[i].content, ref first);
                    }
                    
                    AppendMsg(sb, "user", userInput, ref first);
                    sb.Append("]");
                    
                    return sb.ToString();
                }
                finally
                {
                    StringBuilderPool.Instance.Return(sb);
                }
            }, onComplete);
        }
        
        private static void AppendMsg(StringBuilder sb, string role, string content, ref bool first)
        {
            if (!first) sb.Append(",");
            first = false;
            sb.Append("{\"role\":\"").Append(role).Append("\",\"content\":\"")
              .Append(EscapeJson(content)).Append("\"}");
        }
        
        /// <summary>
        /// Escape JSON string (fast path for common cases)
        /// </summary>
        public static string EscapeJson(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            
            // Fast path: check if escaping is needed
            bool needsEscape = false;
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '\\' || c == '"' || c == '\n' || c == '\r' || c == '\t')
                {
                    needsEscape = true;
                    break;
                }
            }
            
            if (!needsEscape) return input;
            
            // Use pooled StringBuilder
            var sb = StringBuilderPool.Instance.Get(input.Length + 16);
            try
            {
                for (int i = 0; i < input.Length; i++)
                {
                    char c = input[i];
                    switch (c)
                    {
                        case '\\': sb.Append("\\\\"); break;
                        case '"': sb.Append("\\\""); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default: sb.Append(c); break;
                    }
                }
                return sb.ToString();
            }
            finally
            {
                StringBuilderPool.Instance.Return(sb);
            }
        }
        
        /// <summary>
        /// Extract a string value from JSON (simple, fast implementation)
        /// </summary>
        public static string ExtractString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return null;
            
            string pat = "\"" + key + "\":\"";
            int i = json.IndexOf(pat, StringComparison.Ordinal);
            if (i < 0) return null;
            
            int s = i + pat.Length;
            int e = -1;
            bool escaped = false;
            
            for (int j = s; j < json.Length; j++)
            {
                char c = json[j];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (c == '"')
                {
                    e = j;
                    break;
                }
            }
            
            if (e < 0) return null;
            return json.Substring(s, e - s);
        }
        
        /// <summary>
        /// Parse JSON on background thread
        /// </summary>
        public static void ParseJsonAsync<T>(string json, Action<T> onComplete) where T : class
        {
            ThreadPoolDispatcher.Instance.RunAsync(
                () => JsonUtility.FromJson<T>(json),
                onComplete
            );
        }
    }
}
