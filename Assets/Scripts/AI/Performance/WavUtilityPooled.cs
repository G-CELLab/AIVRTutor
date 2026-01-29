using System;
using System.IO;
using UnityEngine;

namespace AI.Performance
{
    /// <summary>
    /// Thread-safe WAV utility that can be called from background threads.
    /// Uses pooled memory streams to minimize GC allocations.
    /// 
    /// Unlike WavUtility.FromAudioClip, these methods work with raw float/byte arrays
    /// and don't require Unity main thread access.
    /// </summary>
    public static class WavUtilityPooled
    {
        /// <summary>
        /// Convert float samples to WAV bytes. Safe to call from background thread.
        /// </summary>
        /// <param name="samples">Audio samples (-1.0 to 1.0)</param>
        /// <param name="sampleRate">Sample rate (e.g., 16000, 24000, 48000)</param>
        /// <param name="channels">Number of channels (1 for mono, 2 for stereo)</param>
        /// <returns>Complete WAV file bytes</returns>
        public static byte[] FromFloatArray(float[] samples, int sampleRate, int channels)
        {
            if (samples == null || samples.Length == 0)
            {
                return Array.Empty<byte>();
            }
            
            // Use pooled memory stream
            var stream = MemoryStreamPool.Instance.Get(44 + samples.Length * 2);
            
            try
            {
                int sampleCount = samples.Length;
                int byteRate = sampleRate * channels * 2;
                int blockAlign = channels * 2;
                int dataSize = sampleCount * 2;
                int chunkSize = 36 + dataSize;
                
                // RIFF header
                WriteString(stream, "RIFF");
                WriteInt32(stream, chunkSize);
                WriteString(stream, "WAVE");
                
                // fmt subchunk
                WriteString(stream, "fmt ");
                WriteInt32(stream, 16);           // Subchunk1Size (16 for PCM)
                WriteInt16(stream, 1);            // AudioFormat (1 = PCM)
                WriteInt16(stream, (short)channels);
                WriteInt32(stream, sampleRate);
                WriteInt32(stream, byteRate);
                WriteInt16(stream, (short)blockAlign);
                WriteInt16(stream, 16);           // BitsPerSample
                
                // data subchunk
                WriteString(stream, "data");
                WriteInt32(stream, dataSize);
                
                // Convert float samples to 16-bit PCM
                for (int i = 0; i < sampleCount; i++)
                {
                    float sample = Mathf.Clamp(samples[i], -1f, 1f);
                    short intSample = (short)(sample * 32767f);
                    WriteInt16(stream, intSample);
                }
                
                return stream.ToArray();
            }
            finally
            {
                MemoryStreamPool.Instance.Return(stream);
            }
        }
        
        /// <summary>
        /// Convert PCM16 bytes to WAV bytes. Safe to call from background thread.
        /// </summary>
        public static byte[] FromPcm16(byte[] pcm16, int sampleRate, int channels)
        {
            if (pcm16 == null || pcm16.Length == 0)
            {
                return Array.Empty<byte>();
            }
            
            var stream = MemoryStreamPool.Instance.Get(44 + pcm16.Length);
            
            try
            {
                int byteRate = sampleRate * channels * 2;
                int blockAlign = channels * 2;
                int dataSize = pcm16.Length;
                int chunkSize = 36 + dataSize;
                
                // RIFF header
                WriteString(stream, "RIFF");
                WriteInt32(stream, chunkSize);
                WriteString(stream, "WAVE");
                
                // fmt subchunk
                WriteString(stream, "fmt ");
                WriteInt32(stream, 16);
                WriteInt16(stream, 1);
                WriteInt16(stream, (short)channels);
                WriteInt32(stream, sampleRate);
                WriteInt32(stream, byteRate);
                WriteInt16(stream, (short)blockAlign);
                WriteInt16(stream, 16);
                
                // data subchunk
                WriteString(stream, "data");
                WriteInt32(stream, dataSize);
                stream.Write(pcm16, 0, pcm16.Length);
                
                return stream.ToArray();
            }
            finally
            {
                MemoryStreamPool.Instance.Return(stream);
            }
        }
        
        /// <summary>
        /// Extract PCM16 from WAV bytes. Safe to call from background thread.
        /// </summary>
        public static byte[] ExtractPcm16(byte[] wav)
        {
            return AsyncAudioProcessor.ExtractPcm16FromWavRobust(wav);
        }
        
        /// <summary>
        /// Convert PCM16 bytes to float samples. Safe to call from background thread.
        /// </summary>
        public static float[] Pcm16ToFloat(byte[] pcm16)
        {
            if (pcm16 == null || pcm16.Length < 2)
            {
                return Array.Empty<float>();
            }
            
            int sampleCount = pcm16.Length / 2;
            float[] samples = new float[sampleCount];
            
            for (int i = 0; i < sampleCount; i++)
            {
                int byteIndex = i * 2;
                short sample = (short)(pcm16[byteIndex] | (pcm16[byteIndex + 1] << 8));
                samples[i] = sample / 32768f;
            }
            
            return samples;
        }
        
        /// <summary>
        /// Convert float samples to PCM16 bytes. Safe to call from background thread.
        /// </summary>
        public static byte[] FloatToPcm16(float[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return Array.Empty<byte>();
            }
            
            byte[] pcm16 = ByteArrayPool.Instance.Rent(samples.Length * 2);
            
            for (int i = 0; i < samples.Length; i++)
            {
                float sample = Mathf.Clamp(samples[i], -1f, 1f);
                short intSample = (short)(sample * 32767f);
                int byteIndex = i * 2;
                pcm16[byteIndex] = (byte)(intSample & 0xFF);
                pcm16[byteIndex + 1] = (byte)((intSample >> 8) & 0xFF);
            }
            
            return pcm16;
        }
        
        /// <summary>
        /// Resample audio from one sample rate to another. Safe to call from background thread.
        /// Uses linear interpolation for speed (suitable for speech).
        /// </summary>
        public static float[] Resample(float[] samples, int fromRate, int toRate)
        {
            if (samples == null || samples.Length == 0 || fromRate == toRate)
            {
                return samples;
            }
            
            double ratio = (double)toRate / fromRate;
            int newLength = (int)(samples.Length * ratio);
            float[] resampled = new float[newLength];
            
            for (int i = 0; i < newLength; i++)
            {
                double srcIndex = i / ratio;
                int srcIndexInt = (int)srcIndex;
                double frac = srcIndex - srcIndexInt;
                
                float sample1 = srcIndexInt < samples.Length ? samples[srcIndexInt] : 0f;
                float sample2 = srcIndexInt + 1 < samples.Length ? samples[srcIndexInt + 1] : sample1;
                
                resampled[i] = (float)(sample1 * (1 - frac) + sample2 * frac);
            }
            
            return resampled;
        }
        
        // Helper methods for writing to stream
        private static void WriteString(MemoryStream stream, string value)
        {
            foreach (char c in value)
            {
                stream.WriteByte((byte)c);
            }
        }
        
        private static void WriteInt16(MemoryStream stream, short value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
        }
        
        private static void WriteInt32(MemoryStream stream, int value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 24) & 0xFF));
        }
    }
}
