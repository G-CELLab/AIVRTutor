using UnityEngine;

public class FreezeAtSecondsBehaviour : StateMachineBehaviour
{
    public float holdAtSeconds = 1f;
    [Tooltip("当 Animator Bool 参数为真时自动解除冻结")]
    public string resumeWhenBoolTrue = "isSpeaking";

    bool paused;
    float prevSpeed = 1f;
    int resumeHash;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        paused = false;
        prevSpeed = Mathf.Max(0.0001f, animator.speed);
        resumeHash = Animator.StringToHash(resumeWhenBoolTrue);
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
