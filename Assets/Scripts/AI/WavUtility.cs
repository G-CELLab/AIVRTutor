using System;
using System.IO;
using UnityEngine;

public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogError("[WavUtility] AudioClip is null in FromAudioClip.");
            return new byte[0];
        }
        if (clip.samples <= 0 || clip.channels <= 0)
        {
            Debug.LogError("[WavUtility] AudioClip has invalid samples or channels.");
            return new byte[0];
        }
        MemoryStream stream = new MemoryStream();
        int sampleCount = clip.samples * clip.channels;
        float[] samples = new float[sampleCount];
        try
        {
            clip.GetData(samples, 0);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[WavUtility] Failed to get AudioClip data: {ex.Message}");
            return new byte[0];
        }
        byte[] bytesData = ConvertAudioClipDataToInt16ByteArray(samples);

        stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
        stream.Write(BitConverter.GetBytes(36 + bytesData.Length), 0, 4);
        stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);
        stream.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
        stream.Write(BitConverter.GetBytes(16), 0, 4);
        stream.Write(BitConverter.GetBytes((ushort)1), 0, 2);
        stream.Write(BitConverter.GetBytes((ushort)clip.channels), 0, 2);
        stream.Write(BitConverter.GetBytes(clip.frequency), 0, 4);

        int byteRate = clip.frequency * clip.channels * 2;
        stream.Write(BitConverter.GetBytes(byteRate), 0, 4);
        ushort blockAlign = (ushort)(clip.channels * 2);
        stream.Write(BitConverter.GetBytes(blockAlign), 0, 2);
        stream.Write(BitConverter.GetBytes((ushort)16), 0, 2);
        stream.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
        stream.Write(BitConverter.GetBytes(bytesData.Length), 0, 4);
        stream.Write(bytesData, 0, bytesData.Length);

        return stream.ToArray();
    }

    private static byte[] ConvertAudioClipDataToInt16ByteArray(float[] data)
    {
        MemoryStream dataStream = new MemoryStream();
        if (data == null || data.Length == 0)
            return dataStream.ToArray();
        foreach (var sample in data)
        {
            // Clamp sample to [-1, 1] to avoid overflow
            float clamped = Mathf.Clamp(sample, -1f, 1f);
            short intData = (short)(clamped * short.MaxValue);
            byte[] byteArr = BitConverter.GetBytes(intData);
            dataStream.Write(byteArr, 0, byteArr.Length);
        }
        return dataStream.ToArray();
    }
}
