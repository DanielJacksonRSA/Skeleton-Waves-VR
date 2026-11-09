using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Sword : MonoBehaviour 
{
    public int damage = 20;
    [Header("Durability")]
    public int maxDurability = 100;
    public int currentDurability = 100;
    public int durabilityLossPerHit = 10;

    public Color highlightColor = new Color(1f, 0.9f, 0.2f, 1f);
    public float highlightEmissionStrength = 1.8f;
    public float stabDuration = 0.15f;
    public float returnDuration = 0.15f;
    public Vector3 stabLocalPositionOffset = new Vector3(0f, 0f, 1f);

    [Header("Audio")]
    public AudioClip[] stabSounds;
    private AudioSource audioSource;

    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock propertyBlock;
    private Vector3 heldLocalPosition;
    private Quaternion heldLocalRotation;
    private bool poseInitialized;
    private bool isSwinging;
    private bool isBroken;
    private Coroutine swingRoutine;
    private WeaponManager weaponManager;
    private readonly HashSet<int> hitEnemyIdsThisSwing = new HashSet<int>();

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    public int CurrentDurability => currentDurability;
    public float DurabilityFraction => maxDurability > 0 ? (float)currentDurability / maxDurability : 0f;

    void Awake()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();
        maxDurability = Mathf.Max(1, maxDurability);
        currentDurability = Mathf.Clamp(currentDurability, 0, maxDurability);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void SetHeldPose()
    {
        heldLocalPosition = transform.localPosition;
        heldLocalRotation = transform.localRotation;
        poseInitialized = true;
        isSwinging = false;

        if (swingRoutine != null)
        {
            StopCoroutine(swingRoutine);
            swingRoutine = null;
        }
    }

    public void SetWeaponManager(WeaponManager manager)
    {
        weaponManager = manager;
    }

    public void Attack(Vector3 targetWorldPosition)
    {
        if (isSwinging) return;

        if (!poseInitialized)
            SetHeldPose();

        swingRoutine = StartCoroutine(SwingRoutine(targetWorldPosition));
    }

    IEnumerator SwingRoutine(Vector3 initialTargetWorldPosition)
    {
        isSwinging = true;
        hitEnemyIdsThisSwing.Clear();
        PlayStabSound();

        Transform parent = transform.parent;
        Vector3 startWorldPosition = transform.position;

        float elapsedTime = 0f;
        while (elapsedTime < stabDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / stabDuration);

            Vector3 currentTarget = weaponManager != null 
                ? weaponManager.GetCenterScreenWorldPosition() 
                : initialTargetWorldPosition;

            Vector3 strikeDirection = (currentTarget - startWorldPosition).normalized;
            float strikeDistance = Vector3.Distance(startWorldPosition, currentTarget);

            Vector3 newWorldPosition = startWorldPosition + strikeDirection * strikeDistance * t;
            transform.position = newWorldPosition;
            yield return null;
        }

        elapsedTime = 0f;
        Vector3 finalTarget = weaponManager != null 
            ? weaponManager.GetCenterScreenWorldPosition() 
            : initialTargetWorldPosition;
        Vector3 finalStrikeDirection = (finalTarget - startWorldPosition).normalized;
        float finalStrikeDistance = Vector3.Distance(startWorldPosition, finalTarget);
        Vector3 strikeEndPosition = startWorldPosition + finalStrikeDirection * finalStrikeDistance;

        while (elapsedTime < returnDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / returnDuration);
            
            Vector3 currentReturnTarget = parent != null ? parent.TransformPoint(heldLocalPosition) : startWorldPosition;
            Vector3 newWorldPosition = Vector3.Lerp(strikeEndPosition, currentReturnTarget, t);
            transform.position = newWorldPosition;
            yield return null;
        }

        Vector3 finalReturnTarget = parent != null ? parent.TransformPoint(heldLocalPosition) : startWorldPosition;
        transform.position = finalReturnTarget;
        if (parent != null)
            transform.localPosition = heldLocalPosition;

        isSwinging = false;
        hitEnemyIdsThisSwing.Clear();
        swingRoutine = null;
    }

    public void SetHighlighted(bool isHighlighted)
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0) return;

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer rend = cachedRenderers[i];
            if (rend == null) continue;

            if (!isHighlighted)
            {
                rend.SetPropertyBlock(null);
                continue;
            }

            rend.GetPropertyBlock(propertyBlock);

            if (rend.sharedMaterial != null)
            {
                if (rend.sharedMaterial.HasProperty(BaseColorId))
                    propertyBlock.SetColor(BaseColorId, highlightColor);
                if (rend.sharedMaterial.HasProperty(ColorId))
                    propertyBlock.SetColor(ColorId, highlightColor);
                if (rend.sharedMaterial.HasProperty(EmissionColorId))
                    propertyBlock.SetColor(EmissionColorId, highlightColor * highlightEmissionStrength);
            }

            rend.SetPropertyBlock(propertyBlock);
        }
    }

    private void OnTriggerEnter(Collider other) 
    {
        if (!isSwinging || isBroken || currentDurability <= 0) return;

        // Check if the thing we hit has an Enemy component
        if (other.TryGetComponent(out AiHealth enemy)) 
        {
            if (enemy.currentHealth <= 0)
                return;

            int enemyId = enemy.GetInstanceID();
            if (hitEnemyIdsThisSwing.Contains(enemyId))
                return;

            hitEnemyIdsThisSwing.Add(enemyId);
            enemy.TakeDamage(damage);
            ApplyDurabilityDamage(durabilityLossPerHit);
        }
    }

    private void ApplyDurabilityDamage(int amount)
    {
        if (isBroken || amount <= 0 || currentDurability <= 0)
            return;

        currentDurability = Mathf.Clamp(currentDurability - amount, 0, maxDurability);

        if (currentDurability <= 0)
        {
            BreakWeapon();
        }
    }

    private void BreakWeapon()
    {
        if (isBroken)
            return;

        isBroken = true;
        isSwinging = false;
        hitEnemyIdsThisSwing.Clear();

        if (swingRoutine != null)
        {
            StopCoroutine(swingRoutine);
            swingRoutine = null;
        }

        WeaponManager manager = weaponManager;
        weaponManager = null;
        if (manager != null)
            manager.OnEquippedSwordBroken(this);

        Destroy(gameObject);
    }

    private void PlayStabSound()
    {
        if (audioSource == null || stabSounds == null || stabSounds.Length == 0)
            return;

        // Select a random audio clip from the array
        AudioClip randomClip = stabSounds[Random.Range(0, stabSounds.Length)];
        audioSource.PlayOneShot(randomClip);
    }
}