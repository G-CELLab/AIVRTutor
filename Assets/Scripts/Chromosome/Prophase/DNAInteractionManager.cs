using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;

[ExecuteInEditMode]
public class DNAInteractionManager : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float baseRadius = 0.04f; // 4cm world space - easier to grab
    public float condensedRadiusFactor = 1.5f; 
    public float colliderGrowthSpeed = 2.0f;

    [Header("Runtime Info")]
    public float currentLerp = 0f;
    
    private Animator animator;
    private SkinnedMeshRenderer smr;
    private XRGrabInteractable grabInteractable;
    
    private const string PREFIX = "_DNA_COLLIDER_";

    [ContextMenu("Setup Colliders")]
    public void InitializeColliders()
    {
        animator = GetComponent<Animator>();
        smr = GetComponentInChildren<SkinnedMeshRenderer>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        // 1. Cleanup
        var allChildren = GetComponentsInChildren<Transform>(true);
        for (int i = allChildren.Length - 1; i >= 0; i--)
        {
            Transform t = allChildren[i];
            if (t != null && (t.name.StartsWith(PREFIX) || t.name.StartsWith("_InteractionSegment")))
            {
                if (Application.isPlaying) Destroy(t.gameObject);
                else DestroyImmediate(t.gameObject);
            }
        }

        foreach (var t in allChildren)
        {
            if (t == null || t.name.StartsWith(PREFIX) || t.name.StartsWith("_InteractionSegment")) continue;
            var colliders = t.GetComponents<Collider>();
            foreach (var col in colliders)
            {
                if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
            }
        }

        List<Collider> newColliders = new List<Collider>();

        // 2. Setup based on structure
        if (smr != null && smr.bones != null && smr.bones.Length > 0)
        {
            SetupSkeletal(newColliders);
        }
        else
        {
            SetupMeshFallback(newColliders);
        }

        UpdateCollidersState(newColliders, 0);

        // 3. Sync Grab Interactable
        if (grabInteractable != null)
        {
            grabInteractable.colliders.Clear();
            foreach (var c in newColliders) grabInteractable.colliders.Add(c);
        }
    }

    void SetupSkeletal(List<Collider> newColliders)
    {
        foreach (var bone in smr.bones)
        {
            if (bone == null) continue;
            List<Transform> validChildren = new List<Transform>();
            for (int i = 0; i < bone.childCount; i++)
            {
                Transform child = bone.GetChild(i);
                if (!child.name.StartsWith("_")) validChildren.Add(child);
            }

            if (validChildren.Count == 0)
            {
                GameObject tip = new GameObject(PREFIX + "Tip");
                tip.transform.SetParent(bone, false);
                tip.transform.localPosition = Vector3.zero;
                SphereCollider sc = tip.AddComponent<SphereCollider>();
                sc.radius = baseRadius;
                newColliders.Add(sc);
            }
            else
            {
                foreach (var child in validChildren)
                {
                    Vector3 direction = child.localPosition;
                    float length = direction.magnitude;
                    if (length < 0.001f) continue;

                    GameObject segment = new GameObject(PREFIX + "Segment");
                    segment.transform.SetParent(bone, false);
                    segment.transform.localPosition = direction * 0.5f;
                    if (direction != Vector3.zero) segment.transform.localRotation = Quaternion.LookRotation(direction);

                    CapsuleCollider cc = segment.AddComponent<CapsuleCollider>();
                    cc.direction = 2; // Z axis
                    cc.height = length * 1.2f;
                    cc.center = Vector3.zero;
                    newColliders.Add(cc);
                }
            }
        }
    }

    void SetupMeshFallback(List<Collider> newColliders)
    {
        var renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (var rend in renderers)
        {
            if (rend.gameObject == gameObject) continue;
            GameObject segment = new GameObject(PREFIX + "Mesh");
            segment.transform.SetParent(rend.transform, false);
            segment.transform.localPosition = Vector3.zero;
            segment.transform.localRotation = Quaternion.identity;
            CapsuleCollider cc = segment.AddComponent<CapsuleCollider>();
            Bounds b = rend.localBounds;
            int dir = 1;
            if (b.size.x > b.size.y && b.size.x > b.size.z) dir = 0;
            else if (b.size.z > b.size.y && b.size.z > b.size.x) dir = 2;
            cc.direction = dir;
            cc.height = b.size[dir] * 1.2f;
            cc.center = b.center;
            newColliders.Add(cc);
        }
        
        if (newColliders.Count == 0)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = baseRadius * 5.0f;
            newColliders.Add(sc);
        }
    }

    void Start()
    {
        if (Application.isPlaying) InitializeColliders();
    }

    void Update()
    {
        if (animator == null) return;
        float targetLerp = 0;
        if (HasParam("CondenseProgress")) targetLerp = animator.GetFloat("CondenseProgress");
        else if (HasParam("isOpened")) targetLerp = animator.GetBool("isOpened") ? 1f : 0f;
        else if (HasParam("IsCondensed")) targetLerp = animator.GetBool("IsCondensed") ? 1f : 0f;
        currentLerp = Mathf.MoveTowards(currentLerp, targetLerp, Time.deltaTime * colliderGrowthSpeed);
        var cols = GetComponentsInChildren<Collider>(true);
        List<Collider> myCols = new List<Collider>();
        foreach(var c in cols) if (c != null && c.gameObject.name.StartsWith(PREFIX)) myCols.Add(c);
        UpdateCollidersState(myCols, currentLerp);
    }

    bool HasParam(string name)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (var param in animator.parameters) if (param.name == name) return true;
        return false;
    }

    void UpdateCollidersState(List<Collider> colliders, float lerp)
    {
        float targetWorldRadius = baseRadius * Mathf.Lerp(1f, condensedRadiusFactor, lerp);
        foreach (var col in colliders)
        {
            if (col == null) continue;
            if (col is CapsuleCollider cc)
            {
                float perpScale = 0;
                Vector3 ls = cc.transform.lossyScale;
                if (cc.direction == 0) perpScale = Mathf.Max(ls.y, ls.z);
                else if (cc.direction == 1) perpScale = Mathf.Max(ls.x, ls.z);
                else perpScale = Mathf.Max(ls.x, ls.y);
                if (perpScale > 0) cc.radius = targetWorldRadius / perpScale;
            }
            else if (col is SphereCollider sc)
            {
                float maxS = Mathf.Max(sc.transform.lossyScale.x, Mathf.Max(sc.transform.lossyScale.y, sc.transform.lossyScale.z));
                if (maxS > 0) sc.radius = targetWorldRadius / maxS;
            }
        }
    }
}