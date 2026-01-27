using UnityEngine;

public class AudioSyncedAnimation : MonoBehaviour
{
    public AudioSource audioSource;
    public float threshold = 0.01f;
    public float animationSpeed = 2.0f;
    public float cooldownTime = 0.5f;
    [Range(0f, 1f)]
    public float restFrameTime = 0f;

    private Animator anim;
    private float lastTimeAudioDetected;

    void Start() => anim = GetComponent<Animator>();

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
        else if (Time.time - lastTimeAudioDetected > cooldownTime)
        {
            StopAnimation();
        }
    }

    // VR HAND INTERACTION LOGIC
    // This triggers when your VR hand (with a collider) touches the sprite
    private void OnTriggerEnter(Collider healthcare)
    {
        // Check if the thing hitting us is a VR hand
        // (You can tag your hand objects as "Player" to be safe)
        if (healthcare.CompareTag("Player") || healthcare.name.Contains("Hand"))
        {
            ToggleAudio();
        }
    }

    public void ToggleAudio()
    {
        if (audioSource.isPlaying) audioSource.Pause();
        else audioSource.Play();
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
        foreach (var s in data) sum += s * s;
        return Mathf.Sqrt(sum / 256);
    }
}