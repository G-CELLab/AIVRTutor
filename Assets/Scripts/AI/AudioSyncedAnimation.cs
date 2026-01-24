using UnityEngine;

public class AudioSyncedAnimation : MonoBehaviour
{
    public AudioSource audioSource;
    public float threshold = 0.01f;
    public float animationSpeed = 2.0f;

    [Header("Timing Settings")]
    public float cooldownTime = 0.5f;
    [Range(0f, 1f)]
    public float restFrameTime = 0f;

    private Animator anim;
    private float lastTimeAudioDetected;

    void Start()
    {
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        if (audioSource == null || !audioSource.isPlaying)
        {
            StopAnimation();
            return;
        }

        float currentVolume = GetVolume();

        if (currentVolume > threshold)
        {
            lastTimeAudioDetected = Time.time;
            anim.speed = animationSpeed;
        }
        else
        {
            if (Time.time - lastTimeAudioDetected > cooldownTime)
            {
                StopAnimation();
            }
            else
            {
                anim.speed = animationSpeed;
            }
        }
    }

    // Call this function from your VR Controller Interactor
    public void ToggleAudio()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Pause();
        }
        else
        {
            audioSource.Play();
        }
    }

    void StopAnimation()
    {
        anim.speed = 0;
        anim.Play(0, -1, restFrameTime);
    }

    float GetVolume()
    {
        float[] data = new float[256];
        audioSource.GetOutputData(data, 0);
        float sum = 0;
        foreach (var s in data)
        {
            sum += s * s;
        }
        return Mathf.Sqrt(sum / 256);
    }
}