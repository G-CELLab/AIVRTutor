using UnityEngine;

//
// 10 秒内 TTS 开声：normal↔speaking；
// 超时未开声：进 dz2 等待；并暴露若干 Trigger 方法用于语义反应（dz7/8/12/13/14/15/18/22）。
//
public class TTSAnimatorDriver : MonoBehaviour
{
    [Header("Refs")]
    public TextToSpeechPlayer tts;      // 可留空：自动 FindObjectOfType
    public Animator animator;           // 可留空：本物体或子物体自动查找

    [Header("Animator Bool Params")]
    public string isSpeakingParam = "isSpeaking";
    public string waitSpeakParam = "waitSpeak";

    [Header("Animator Trigger Params (reactions)")]
    public string trigDZ7 = "doDZ7";   // 柔和认可
    public string trigDZ8 = "doDZ8";   // 更强的认可
    public string trigDZ13 = "doDZ13";  // nutrient/energy/feed
    public string trigDZ14 = "doDZ14";  // ATP transfer
    public string trigDZ15 = "doDZ15";  // S-phase / split
    public string trigDZ18 = "doDZ18";  // X shape
    public string trigDZ12 = "doDZ12";  // row/line up
    public string trigDZ22 = "doDZ22";  // apart/separate

    [Header("Gesture Animation Speed Control")]
    [Tooltip("Animation speed multiplier for DZ12 (LINE UP gesture). Lower = slower/longer. 0.5 = twice as long.")]
    public float lineUpGestureSpeedMultiplier = 1.0f;  // Normal speed, no delay
    [Tooltip("Optional: Animator float parameter name for controlling animation speed. Leave empty if not using.")]
    public string animSpeedParam = "gestureSpeed";
    private int _animSpeedHash;
    private bool _hasAnimSpeed;

    [Header("Timing")]
    public float waitBeforeDZ2 = 10f;   // 超时阈值（秒）
    public float lingerAfterEnd = 0f; // 说完收尾延迟

    [Header("Debug")]
    public bool verbose = true;

    // 缓存 hash
    int _sHash, _wHash;
    int _dz7, _dz8, _dz13, _dz14, _dz15, _dz18, _dz12, _dz22;

    // 参数存在性标记（避免静默）
    bool _hasIsSpeaking, _hasWaitSpeak;
    bool _hasDZ7, _hasDZ8, _hasDZ13, _hasDZ14, _hasDZ15, _hasDZ18, _hasDZ12, _hasDZ22;

    bool _expectingSpeech;
    float _waitElapsed, _linger;

    void Awake()
    {
        if (!tts) tts = FindAnyObjectByType<TextToSpeechPlayer>();
        if (!animator) animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
        if (!animator) { Debug.LogError("[TTSAnimatorDriver] ❌ 没有 Animator"); enabled = false; return; }

        Rehash();
        ValidateParameters();

        if (verbose)
        {
            if (!tts) Debug.LogWarning("[TTSAnimatorDriver] ⚠️ 未找到 TextToSpeechPlayer，speaking 轮询可能一直为 false");
            if (!_hasIsSpeaking || !_hasWaitSpeak)
                Debug.LogWarning("[TTSAnimatorDriver] ⚠️ isSpeaking / waitSpeak 任一缺失，开口与等待状态将无法驱动状态机");
        }
    }

    void OnValidate()
    {
        // 在 Inspector 改了参数名时，自动刷新 Hash（编辑器中生效）
        if (animator) Rehash();
    }

    // —— GPT 请求发出时调用：开始“等待发声”计时 ——
    public void NotifyExpectSpeech(float delay = -1f)
    {
        _expectingSpeech = true;
        _waitElapsed = 0f;
        if (delay > 0f) waitBeforeDZ2 = delay;

        if (_hasWaitSpeak) animator.SetBool(_wHash, false);
        if (verbose) Debug.Log($"[TTSAnimatorDriver] ▶️ 期待发声，超时={waitBeforeDZ2:0.##}s");
    }

    public void CancelWait()
    {
        _expectingSpeech = false;
        if (_hasWaitSpeak) animator.SetBool(_wHash, false);
        if (verbose) Debug.Log("[TTSAnimatorDriver] ⏹️ 取消等待");
    }

    // —— 对外暴露的触发方法（由 GPTConnector 调用） ——
    public void TriggerAffirmationRandom()
    {
        if (Random.value < 0.5f) TriggerByHash(_dz7, _hasDZ7, trigDZ7);
        else TriggerByHash(_dz8, _hasDZ8, trigDZ8);
    }
    public void TriggerDZ13() { TriggerByHash(_dz13, _hasDZ13, trigDZ13); }
    public void TriggerDZ14() { TriggerByHash(_dz14, _hasDZ14, trigDZ14); }
    public void TriggerDZ15() { TriggerByHash(_dz15, _hasDZ15, trigDZ15); }
    public void TriggerDZ18() { TriggerByHash(_dz18, _hasDZ18, trigDZ18); }
    
    // DZ12 (LINE UP) with custom speed control for longer gesture duration
    public void TriggerDZ12() 
    { 
        // Set slower speed for line up gesture to make it play longer (research requirement)
        if (_hasAnimSpeed)
        {
            animator.SetFloat(_animSpeedHash, lineUpGestureSpeedMultiplier);
            if (verbose) Debug.Log($"[TTSAnimatorDriver] 🐌 Setting LINE UP gesture speed to {lineUpGestureSpeedMultiplier}x");
        }
        TriggerByHash(_dz12, _hasDZ12, trigDZ12); 
    }
    
    public void TriggerDZ22() { TriggerByHash(_dz22, _hasDZ22, trigDZ22); }

    void TriggerByHash(int hash, bool hasParam, string nameForLog)
    {
        if (!animator) return;
        if (!hasParam)
        {
            if (verbose) Debug.LogWarning($"[TTSAnimatorDriver] ❗ Animator 缺少 Trigger 参数: {nameForLog}");
            return;
        }
        animator.ResetTrigger(hash); // 防抖
        animator.SetTrigger(hash);
        if (verbose) Debug.Log($"[TTSAnimatorDriver] 🔔 Trigger: {nameForLog}");
    }

    void Update()
    {
        if (!animator) return;

        bool speaking = false;
        if (tts && tts.audioSource) speaking = tts.audioSource.isPlaying;

        if (speaking)
        {
            _expectingSpeech = false;
            _linger = lingerAfterEnd;
            if (_hasWaitSpeak) animator.SetBool(_wHash, false);
        }
        else
        {
            if (_linger > 0f) _linger -= Time.deltaTime;

            if (_expectingSpeech)
            {
                _waitElapsed += Time.deltaTime;
                bool timeout = _waitElapsed >= waitBeforeDZ2;
                if (_hasWaitSpeak) animator.SetBool(_wHash, timeout); // true→进 dz2
            }
            else
            {
                if (_hasWaitSpeak) animator.SetBool(_wHash, false);
            }
        }

        if (_hasIsSpeaking) animator.SetBool(_sHash, speaking || _linger > 0f);
    }

    // ======= 工具：参数校验 / 重建哈希 =======
    void Rehash()
    {
        _sHash = Animator.StringToHash(isSpeakingParam);
        _wHash = Animator.StringToHash(waitSpeakParam);
        _dz7 = Animator.StringToHash(trigDZ7);
        _dz8 = Animator.StringToHash(trigDZ8);
        _dz13 = Animator.StringToHash(trigDZ13);
        _dz14 = Animator.StringToHash(trigDZ14);
        _dz15 = Animator.StringToHash(trigDZ15);
        _dz18 = Animator.StringToHash(trigDZ18);
        _dz12 = Animator.StringToHash(trigDZ12);
        _dz22 = Animator.StringToHash(trigDZ22);
        _animSpeedHash = Animator.StringToHash(animSpeedParam);
    }

    void ValidateParameters()
    {
        _hasIsSpeaking = HasParam(isSpeakingParam, AnimatorControllerParameterType.Bool);
        _hasWaitSpeak = HasParam(waitSpeakParam, AnimatorControllerParameterType.Bool);

        _hasDZ7 = HasParam(trigDZ7, AnimatorControllerParameterType.Trigger);
        _hasDZ8 = HasParam(trigDZ8, AnimatorControllerParameterType.Trigger);
        _hasDZ13 = HasParam(trigDZ13, AnimatorControllerParameterType.Trigger);
        _hasDZ14 = HasParam(trigDZ14, AnimatorControllerParameterType.Trigger);
        _hasDZ15 = HasParam(trigDZ15, AnimatorControllerParameterType.Trigger);
        _hasDZ18 = HasParam(trigDZ18, AnimatorControllerParameterType.Trigger);
        _hasDZ12 = HasParam(trigDZ12, AnimatorControllerParameterType.Trigger);
        _hasDZ22 = HasParam(trigDZ22, AnimatorControllerParameterType.Trigger);
        
        _hasAnimSpeed = HasParam(animSpeedParam, AnimatorControllerParameterType.Float);

        if (verbose)
        {
            LogParamCheck(isSpeakingParam, _hasIsSpeaking, "Bool");
            LogParamCheck(waitSpeakParam, _hasWaitSpeak, "Bool");

            LogParamCheck(trigDZ7, _hasDZ7, "Trigger");
            LogParamCheck(trigDZ8, _hasDZ8, "Trigger");
            LogParamCheck(trigDZ13, _hasDZ13, "Trigger");
            LogParamCheck(trigDZ14, _hasDZ14, "Trigger");
            LogParamCheck(trigDZ15, _hasDZ15, "Trigger");
            LogParamCheck(trigDZ18, _hasDZ18, "Trigger");
            LogParamCheck(trigDZ12, _hasDZ12, "Trigger");
            LogParamCheck(trigDZ22, _hasDZ22, "Trigger");
            
            LogParamCheck(animSpeedParam, _hasAnimSpeed, "Float");
            if (!_hasAnimSpeed) 
                Debug.LogWarning("[TTSAnimatorDriver] ⚠️ gestureSpeed Float parameter not found - LINE UP gesture speed control disabled. Add 'gestureSpeed' Float parameter to Animator to enable.");
        }
    }

    bool HasParam(string name, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(name)) return false;
        foreach (var p in animator.parameters)
            if (p != null && p.type == type && p.name == name)
                return true;
        return false;
    }

    void LogParamCheck(string name, bool ok, string type)
    {
        if (ok) Debug.Log($"[TTSAnimatorDriver] ✅ Animator 参数存在: {type} {name}");
        else Debug.LogWarning($"[TTSAnimatorDriver] ❌ 缺少 Animator 参数: {type} {name}");
    }

    // ======= 右键菜单：一键自测（不依赖语音/识别） =======
    [ContextMenu("Debug/Trigger DZ7 (Affirm/soft)")] void _T_DZ7() => TriggerByHash(_dz7, _hasDZ7, trigDZ7);
    [ContextMenu("Debug/Trigger DZ8 (Affirm/strong)")] void _T_DZ8() => TriggerByHash(_dz8, _hasDZ8, trigDZ8);
    [ContextMenu("Debug/Trigger DZ13 (Nutrient)")] void _T_DZ13() => TriggerByHash(_dz13, _hasDZ13, trigDZ13);
    [ContextMenu("Debug/Trigger DZ14 (ATP)")] void _T_DZ14() => TriggerByHash(_dz14, _hasDZ14, trigDZ14);
    [ContextMenu("Debug/Trigger DZ15 (Split)")] void _T_DZ15() => TriggerByHash(_dz15, _hasDZ15, trigDZ15);
    [ContextMenu("Debug/Trigger DZ18 (X-Shape)")] void _T_DZ18() => TriggerByHash(_dz18, _hasDZ18, trigDZ18);
    [ContextMenu("Debug/Trigger DZ12 (Row)")] void _T_DZ12() => TriggerByHash(_dz12, _hasDZ12, trigDZ12);
    [ContextMenu("Debug/Trigger DZ22 (Apart)")] void _T_DZ22() => TriggerByHash(_dz22, _hasDZ22, trigDZ22);
}
