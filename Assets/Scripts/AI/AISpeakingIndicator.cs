using UnityEngine;

public class AISpeakingIndicator : MonoBehaviour
{
    [Header("References")]
    public GPTConnector gptConnector;
    public TextToSpeechPlayer ttsPlayer;
    public Renderer targetRenderer;

    [Header("Behavior")]
    [Tooltip("If true, indicator is active while agent is generating OR speaking. If false, only during audio playback.")]
    public bool includeThinking = false;
    [Tooltip("How quickly the indicator blends on/off.")]
    public float blendSpeed = 8f;
    [Tooltip("If enabled, lightly tint the surface while speaking. Keep this OFF to preserve metallic texture exactly.")]
    public bool affectSurfaceTint = false;
    [Range(0f, 1f)]
    [Tooltip("Surface tint amount when affectSurfaceTint is enabled.")]
    public float speakingTintStrength = 0.15f;

    [Header("Visual")]
    public Color idleColor = new Color(0.15f, 0.2f, 0.25f, 1f);
    public Color speakingColor = new Color(0.35f, 0.62f, 0.95f, 1f);
    [Tooltip("Emission multiplier while speaking.")]
    [Range(0f, 1f)] public float maxEmission = 0.28f;
    public bool pulseScale = true;
    [Range(0f, 0.2f)] public float pulseAmount = 0.05f;
    public float pulseSpeed = 6f;

    private MaterialPropertyBlock _block;
    private Vector3 _baseScale;
    private float _blend;
    private bool _hasBaseColor;
    private bool _hasColor;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Reset()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (gptConnector == null) gptConnector = FindFirstObjectByType<GPTConnector>();
        if (ttsPlayer == null && gptConnector != null) ttsPlayer = gptConnector.ttsPlayer;
    }

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (gptConnector == null) gptConnector = FindFirstObjectByType<GPTConnector>();
        if (ttsPlayer == null && gptConnector != null) ttsPlayer = gptConnector.ttsPlayer;

        _block = new MaterialPropertyBlock();
        _baseScale = transform.localScale;

        if (targetRenderer != null)
        {
            var mat = targetRenderer.material;
            if (mat != null)
            {
                _hasBaseColor = mat.HasProperty(BaseColorId);
                _hasColor = mat.HasProperty(ColorId);
                if (mat.HasProperty(EmissionColorId)) mat.EnableKeyword("_EMISSION");
            }
        }

        ApplyVisuals(0f, false);
    }

    private void Update()
    {
        bool speaking = IsAgentSpeakingNow();
        float target = speaking ? 1f : 0f;
        _blend = Mathf.MoveTowards(_blend, target, blendSpeed * Time.deltaTime);
        ApplyVisuals(_blend, speaking);
    }

    private bool IsAgentSpeakingNow()
    {
        if (ttsPlayer == null && gptConnector != null && gptConnector.ttsPlayer != null)
            ttsPlayer = gptConnector.ttsPlayer;

        bool actuallySpeaking = ttsPlayer != null && ttsPlayer.IsSpeaking;
        if (!includeThinking) return actuallySpeaking;

        return actuallySpeaking || (gptConnector != null && gptConnector.IsAgentBusy);
    }

    private void ApplyVisuals(float blend, bool speaking)
    {
        if (targetRenderer != null)
        {
            Color emission = speakingColor * (blend * maxEmission);

            // Clear any stale overrides from earlier script versions so albedo/metallic stays intact.
            targetRenderer.SetPropertyBlock(null);

            _block.Clear();

            if (affectSurfaceTint)
            {
                Material sourceMat = targetRenderer.sharedMaterial != null
                    ? targetRenderer.sharedMaterial
                    : targetRenderer.material;

                Color materialBase = (_hasBaseColor && sourceMat != null)
                    ? sourceMat.GetColor(BaseColorId)
                    : idleColor;
                Color materialColor = (_hasColor && sourceMat != null)
                    ? sourceMat.GetColor(ColorId)
                    : idleColor;

                float tint = blend * speakingTintStrength;
                Color baseOut = Color.Lerp(materialBase, speakingColor, tint);
                Color colorOut = Color.Lerp(materialColor, speakingColor, tint);

                if (_hasBaseColor) _block.SetColor(BaseColorId, baseOut);
                if (_hasColor) _block.SetColor(ColorId, colorOut);
            }

            _block.SetColor(EmissionColorId, emission);
            targetRenderer.SetPropertyBlock(_block);
        }

        if (pulseScale)
        {
            float wave = (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f) * blend;
            float s = 1f + wave * pulseAmount;
            transform.localScale = _baseScale * s;
        }
        else
        {
            transform.localScale = _baseScale;
        }
    }

    private void OnDisable()
    {
        if (_block != null) _block.Clear();
        if (targetRenderer != null) targetRenderer.SetPropertyBlock(null);
        transform.localScale = _baseScale;
    }
}