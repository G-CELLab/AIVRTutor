using UnityEngine;

public class FreezeAtSecondsBehaviour : StateMachineBehaviour
{
    public float holdAtSeconds = 0f;
    [Tooltip("当 Animator Bool 参数为真时自动解除冻结")]
    public string resumeWhenBoolTrue = "isSpeaking";
    [Tooltip("在进入状态时立刻冻结，不播放任何动画")]
    public bool freezeOnEntry = true;

    bool paused;
    float prevSpeed = 1f;
    int resumeHash;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        paused = false;
        prevSpeed = Mathf.Max(0.0001f, animator.speed);
        resumeHash = Animator.StringToHash(resumeWhenBoolTrue);

        // 如果启用 freezeOnEntry，立刻暂停动画
        if (freezeOnEntry)
        {
            animator.speed = 0f;
            paused = true;
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 若外部已要求恢复（TTS 开声），立刻解冻
        if (paused && animator.GetBool(resumeHash))
        {
            animator.speed = prevSpeed;
            paused = false;
            return;
        }

        if (paused) { animator.speed = 0f; return; }

        float len = Mathf.Max(0.0001f, stateInfo.length);
        float tInLoop = (stateInfo.normalizedTime - Mathf.Floor(stateInfo.normalizedTime)) * len;
        if (tInLoop >= holdAtSeconds)
        {
            animator.speed = 0f; // 全局暂停（将由 isSpeaking 解冻）
            paused = true;
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (paused) animator.speed = prevSpeed;
        paused = false;
    }
}
