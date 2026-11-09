using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class WishingWell : MonoBehaviour
{
    [Header("References")]
    public Transform lookOrigin;
    public Light wellPointLight;
    public Transform spitOrigin;

    [Header("Sword Rewards")]
    public GameObject sword1Prefab;
    public GameObject sword2Prefab;
    public GameObject sword3Prefab;

    [Header("Wish Requirements")]
    public int minimumGemsForWish = 3;
    public float interactDistance = 4f;
    public LayerMask interactMask = ~0;

    [Header("Wish Input")]
    public bool allowKeyboardE = true;
    public bool allowGamepadX = true;
    [Tooltip("Useful fallback because X is often shared with jump.")]
    public bool allowGamepadY = true;

    [Header("Chance Tuning")]
    [Tooltip("Number of offered gems where best odds are reached.")]
    public int gemsForBestOdds = 20;
    [Range(0f, 1f)] public float sword2ChanceAtMinimum = 0.19f;
    [Range(0f, 1f)] public float sword2ChanceAtBest = 0.55f;
    [Range(0f, 1f)] public float sword3ChanceAtMinimum = 0.01f;
    [Range(0f, 1f)] public float sword3ChanceAtBest = 0.3f;

    [Header("Well Light")]
    public float baseLightIntensity = 0.5f;
    public float lightIncreasePerGem = 0.5f;
    public float maxLightIntensity = 6f;

    [Header("Reward Launch")]
    public float spitSpawnForwardOffset = 0.55f;
    public float spitSpawnUpOffset = 0.35f;
    public float spitForwardVelocity = 8f;
    public float spitUpVelocity = 4.5f;
    public float spitRandomSpin = 3f;
    public float ignoreWellCollisionDuration = 0.2f;

    [Header("Spawn Safety")]
    public float spawnClearanceRadius = 0.12f;
    public int spawnClearanceSteps = 8;
    public float spawnClearanceStepHeight = 0.2f;
    public float spawnSurfaceLift = 0.08f;

    [Header("Audio")]
    public AudioSource audioSource;
    [Tooltip("One of these will be played when a gem is thrown into the well.")]
    public AudioClip[] gemEntryClips;
    [Tooltip("Sound to play when the sword is ejected/spawned from the well.")]
    public AudioClip swordEjectClip;
    [Tooltip("If > 0 and the player reaches this many gems in the well, play the special clip.")]
    public int maxGems = 0;
    [Tooltip("Special sound to play when the well reaches its max gem count.")]
    public AudioClip maxGemsClip;

    private readonly HashSet<int> consumedGemIds = new HashSet<int>();
    private int gemsInWell;

    public int GemsInWell => gemsInWell;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    void Awake()
    {
        if (wellPointLight == null)
            wellPointLight = GetComponentInChildren<Light>();

        if (lookOrigin == null && Camera.main != null)
            lookOrigin = Camera.main.transform;

        // Ensure audio source exists for playing gem/eject sounds
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        ApplyWellLight();
    }

    void Update()
    {
        if (!WasWishPressed())
            return;

        if (!IsPlayerLookingAtWell())
            return;

        TryMakeWish();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!TryGetGemObject(other, out GameObject gemObject))
            return;

        int gemId = gemObject.GetInstanceID();
        if (consumedGemIds.Contains(gemId))
            return;

        consumedGemIds.Add(gemId);
        if (consumedGemIds.Count > 2048)
            consumedGemIds.Clear();

        gemsInWell++;
        ApplyWellLight();

        // Play a special clip if we've reached max gems; otherwise play a random gem-entry sound
        if (maxGems > 0 && gemsInWell >= maxGems && audioSource != null && maxGemsClip != null)
        {
            audioSource.PlayOneShot(maxGemsClip);
        }
        else
        {
            PlayRandomGemSound();
        }

        Destroy(gemObject);
    }

    public bool IsPlayerLookingAtWell()
    {
        Transform rayOrigin = ResolveLookOrigin();
        if (rayOrigin == null)
            return false;

        if (!Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Collide))
            return false;

        return hit.collider != null && hit.collider.transform.IsChildOf(transform);
    }

    private void TryMakeWish()
    {
        if (gemsInWell < minimumGemsForWish)
        {
            Debug.Log($"Need at least {minimumGemsForWish} gems in the well before making a wish.");
            return;
        }

        GameObject rewardSword = RollRewardSword(gemsInWell);
        if (rewardSword == null)
        {
            Debug.LogWarning("WishingWell: Sword reward prefabs are not assigned.");
            return;
        }

        int gemsSpent = gemsInWell;
        gemsInWell = 0;
        ApplyWellLight();

        SpawnAndSpitSword(rewardSword);
        Debug.Log($"Wish granted with {gemsSpent} gems.");
    }

    private void SpawnAndSpitSword(GameObject swordPrefab)
    {
        if (swordPrefab == null)
            return;

        Transform launchOrigin = spitOrigin != null ? spitOrigin : transform;
        Vector3 spawnPosition = ResolveSafeSpawnPosition(launchOrigin);

        Quaternion spawnRotation = Quaternion.LookRotation(launchOrigin.forward, Vector3.up);
        
        // Instantiate with default transform to maintain prefab's internal hierarchy structure
        GameObject spawnedSword = Instantiate(swordPrefab);
        spawnedSword.transform.position = spawnPosition;
        spawnedSword.transform.rotation = spawnRotation;

        Collider[] swordColliders = spawnedSword.GetComponentsInChildren<Collider>(true);
        EnsureSolidColliders(spawnedSword, ref swordColliders);

        Rigidbody swordBody = spawnedSword.GetComponent<Rigidbody>();
        if (swordBody == null)
            swordBody = spawnedSword.AddComponent<Rigidbody>();

        swordBody.isKinematic = false;
        swordBody.useGravity = true;
        swordBody.detectCollisions = true;
        swordBody.interpolation = RigidbodyInterpolation.Interpolate;
        swordBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        swordBody.linearVelocity = Vector3.zero;
        swordBody.angularVelocity = Vector3.zero;

        Physics.SyncTransforms();

        if (ignoreWellCollisionDuration > 0f)
            IgnoreSwordCollisionsWithWellTemporarily(swordColliders, ignoreWellCollisionDuration);

        Vector3 launchVelocity = launchOrigin.forward * spitForwardVelocity + Vector3.up * spitUpVelocity;
        swordBody.AddForce(launchVelocity, ForceMode.VelocityChange);

        if (spitRandomSpin > 0f)
            swordBody.AddTorque(Random.insideUnitSphere * spitRandomSpin, ForceMode.VelocityChange);

        // Play a specific eject sound when the sword is launched
        if (audioSource != null && swordEjectClip != null)
            audioSource.PlayOneShot(swordEjectClip);
    }

    private Vector3 ResolveSafeSpawnPosition(Transform launchOrigin)
    {
        Vector3 basePosition = launchOrigin.position
            + launchOrigin.forward * spitSpawnForwardOffset
            + Vector3.up * spitSpawnUpOffset;

        Vector3 candidate = basePosition;

        if (spawnClearanceRadius > 0f)
        {
            for (int i = 0; i <= Mathf.Max(0, spawnClearanceSteps); i++)
            {
                Vector3 testPosition = basePosition + Vector3.up * (i * spawnClearanceStepHeight);
                bool blocked = Physics.CheckSphere(testPosition, spawnClearanceRadius, ~0, QueryTriggerInteraction.Ignore);
                if (!blocked)
                {
                    candidate = testPosition;
                    break;
                }
            }
        }

        float rayStartOffset = Mathf.Max(2f, spawnClearanceSteps * spawnClearanceStepHeight + 1f);
        Vector3 rayStart = candidate + Vector3.up * rayStartOffset;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit surfaceHit, rayStartOffset + 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            float minY = surfaceHit.point.y + spawnSurfaceLift;
            if (candidate.y < minY)
                candidate.y = minY;
        }

        return candidate;
    }

    private void EnsureSolidColliders(GameObject spawnedSword, ref Collider[] swordColliders)
    {
        int solidColliderCount = 0;

        for (int i = 0; i < swordColliders.Length; i++)
        {
            Collider collider = swordColliders[i];
            if (collider == null)
                continue;

            collider.enabled = true;
            collider.isTrigger = false;

            if (!collider.isTrigger)
                solidColliderCount++;
        }

        if (solidColliderCount > 0)
            return;

        Renderer[] renderers = spawnedSword.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        BoxCollider fallback = spawnedSword.AddComponent<BoxCollider>();
        fallback.isTrigger = false;
        fallback.center = spawnedSword.transform.InverseTransformPoint(combined.center);
        fallback.size = spawnedSword.transform.InverseTransformVector(combined.size);

        swordColliders = spawnedSword.GetComponentsInChildren<Collider>(true);
        Debug.LogWarning("WishingWell: Spawned sword had no usable colliders, added a fallback BoxCollider.");
    }

    private void IgnoreSwordCollisionsWithWellTemporarily(Collider[] swordColliders, float duration)
    {
        if (swordColliders == null || swordColliders.Length == 0)
            return;

        Collider[] wellColliders = GetComponentsInChildren<Collider>(true);
        if (wellColliders == null || wellColliders.Length == 0)
            return;

        for (int i = 0; i < swordColliders.Length; i++)
        {
            Collider swordCollider = swordColliders[i];
            if (swordCollider == null)
                continue;

            for (int j = 0; j < wellColliders.Length; j++)
            {
                Collider wellCollider = wellColliders[j];
                if (wellCollider == null)
                    continue;

                Physics.IgnoreCollision(swordCollider, wellCollider, true);
                StartCoroutine(RestoreCollisionAfterDelay(swordCollider, wellCollider, duration));
            }
        }
    }

    private IEnumerator RestoreCollisionAfterDelay(Collider swordCollider, Collider wellCollider, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (swordCollider != null && wellCollider != null)
            Physics.IgnoreCollision(swordCollider, wellCollider, false);
    }

    private void PlayRandomGemSound()
    {
        if (audioSource == null || gemEntryClips == null || gemEntryClips.Length == 0)
            return;

        AudioClip clip = gemEntryClips[Random.Range(0, gemEntryClips.Length)];
        if (clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void PlaySwordEjectSound()
    {
        if (audioSource == null || swordEjectClip == null)
            return;

        audioSource.PlayOneShot(swordEjectClip);
    }

    private GameObject RollRewardSword(int offeredGems)
    {
        GetSwordOdds(offeredGems, out float chance1, out float chance2, out float chance3);

        float weight1 = sword1Prefab != null ? chance1 : 0f;
        float weight2 = sword2Prefab != null ? chance2 : 0f;
        float weight3 = sword3Prefab != null ? chance3 : 0f;

        float totalWeight = weight1 + weight2 + weight3;
        if (totalWeight <= 0f)
            return null;

        float roll = Random.value * totalWeight;
        if (roll <= weight1)
            return sword1Prefab;

        roll -= weight1;
        if (roll <= weight2)
            return sword2Prefab;

        return sword3Prefab;
    }

    private void GetSwordOdds(int offeredGems, out float chance1, out float chance2, out float chance3)
    {
        int bestOddsGemCount = Mathf.Max(minimumGemsForWish + 1, gemsForBestOdds);
        float t = Mathf.InverseLerp(minimumGemsForWish, bestOddsGemCount, offeredGems);

        chance2 = Mathf.Lerp(sword2ChanceAtMinimum, sword2ChanceAtBest, t);

        // Cubic growth keeps sword 3 rare until high gem counts.
        float rareT = t * t * t;
        chance3 = Mathf.Lerp(sword3ChanceAtMinimum, sword3ChanceAtBest, rareT);

        chance1 = Mathf.Clamp01(1f - chance2 - chance3);

        float total = chance1 + chance2 + chance3;
        if (total <= 0f)
        {
            chance1 = 1f;
            chance2 = 0f;
            chance3 = 0f;
            return;
        }

        chance1 /= total;
        chance2 /= total;
        chance3 /= total;
    }

    private bool TryGetGemObject(Collider other, out GameObject gemObject)
    {
        gemObject = null;
        if (other == null)
            return false;

        GemPickup pickup = other.GetComponentInParent<GemPickup>();
        if (pickup != null)
        {
            gemObject = pickup.transform.root.gameObject;
            return true;
        }

        Gem gem = other.GetComponentInParent<Gem>();
        if (gem != null)
        {
            gemObject = gem.transform.root.gameObject;
            return true;
        }

        return false;
    }

    private bool WasWishPressed()
    {
        bool keyboardPressed = allowKeyboardE && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool gamepadPressed = false;

        if (Gamepad.current != null)
        {
            bool xPressed = allowGamepadX && Gamepad.current.buttonWest.wasPressedThisFrame;
            bool yPressed = allowGamepadY && Gamepad.current.buttonNorth.wasPressedThisFrame;
            gamepadPressed = xPressed || yPressed;
        }

        return keyboardPressed || gamepadPressed;
    }

    private Transform ResolveLookOrigin()
    {
        if (lookOrigin != null)
            return lookOrigin;

        if (Camera.main != null)
            return Camera.main.transform;

        return null;
    }

    private void ApplyWellLight()
    {
        if (wellPointLight == null)
            return;

        float desiredIntensity = baseLightIntensity + gemsInWell * lightIncreasePerGem;
        wellPointLight.intensity = Mathf.Clamp(desiredIntensity, baseLightIntensity, maxLightIntensity);
    }
}
