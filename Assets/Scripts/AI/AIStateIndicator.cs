using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class AIStateIndicator : MonoBehaviour
{
    [Header("UI")]
    public Image indicatorImage;

    [Header("Provider")]
    [Tooltip("Assign the GameObject that has your AI agent component. If that component implements IAIStateProvider, the indicator will subscribe to its event. Otherwise the script will attempt to poll a 'CurrentState' property/field.")]
    public GameObject stateProviderGameObject;

    [Header("Colors")]
    public Color idleColor = Color.gray;
    public Color listeningColor = Color.cyan;
    public Color speakingColor = Color.green;

    [Header("Polling (fallback)")]
    public float pollInterval = 0.1f;

    // internal
    IAIStateProvider provider;
    Coroutine pollCoroutine;
    Component reflectedComponent;
    PropertyInfo reflectedProperty;
    FieldInfo reflectedField;

    void Awake()
    {
        if (indicatorImage == null)
            Debug.LogWarning($"{nameof(AIStateIndicator)}: indicatorImage not assigned.", this);

        if (stateProviderGameObject == null)
        {
            Debug.LogWarning($"{nameof(AIStateIndicator)}: stateProviderGameObject not assigned — indicator will remain at Idle color.", this);
            SetColorForState(AIState.Idle);
            return;
        }

        // Try interface-based provider first
        var any = stateProviderGameObject.GetComponents<MonoBehaviour>();
        foreach (var mb in any)
        {
            if (mb is IAIStateProvider p)
            {
                provider = p;
                break;
            }
        }

        if (provider != null)
        {
            provider.OnStateChanged += OnProviderStateChanged;
            // initialize visual with current state
            SetColorForState(provider.CurrentState);
        }
        else
        {
            // fallback: find a component with a 'CurrentState' property or field of type AIState
            foreach (var comp in stateProviderGameObject.GetComponents<Component>())
            {
                var t = comp.GetType();
                var prop = t.GetProperty("CurrentState", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.PropertyType == typeof(AIState))
                {
                    reflectedComponent = comp;
                    reflectedProperty = prop;
                    break;
                }

                var field = t.GetField("CurrentState", BindingFlags.Public | BindingFlags.Instance);
                if (field != null && field.FieldType == typeof(AIState))
                {
                    reflectedComponent = comp;
                    reflectedField = field;
                    break;
                }
            }

            if (reflectedComponent != null)
            {
                pollCoroutine = StartCoroutine(PollStateCoroutine());
            }
            else
            {
                Debug.LogWarning($"{nameof(AIStateIndicator)}: No IAIStateProvider found and no public CurrentState property/field detected on {stateProviderGameObject.name}. Indicator will remain Idle.", this);
                SetColorForState(AIState.Idle);
            }
        }
    }

    void OnDestroy()
    {
        if (provider != null)
            provider.OnStateChanged -= OnProviderStateChanged;

        if (pollCoroutine != null)
            StopCoroutine(pollCoroutine);
    }

    void OnProviderStateChanged(AIState newState)
    {
        SetColorForState(newState);
    }

    IEnumerator PollStateCoroutine()
    {
        while (true)
        {
            AIState state = AIState.Idle;
            if (reflectedProperty != null)
            {
                state = (AIState)reflectedProperty.GetValue(reflectedComponent);
            }
            else if (reflectedField != null)
            {
                state = (AIState)reflectedField.GetValue(reflectedComponent);
            }

            SetColorForState(state);
            yield return new WaitForSeconds(pollInterval);
        }
    }

    void SetColorForState(AIState state)
    {
        if (indicatorImage == null) return;

        switch (state)
        {
            case AIState.Listening:
                indicatorImage.color = listeningColor;
                break;
            case AIState.Speaking:
                indicatorImage.color = speakingColor;
                break;
            default:
                indicatorImage.color = idleColor;
                break;
        }
    }
}