using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

// ChestController: makes a chest become available at a random interval (15s - 4min),
// plays a looping audio while available, disables the point light, and allows
// the player to press A (gamepad) or E (keyboard) to loot 1-3 gems.
public class Barrel : MonoBehaviour
{
    [Header("Timer (seconds)")]
    public float minAvailableTime = 15f;
    public float maxAvailableTime = 240f; // 4 minutes

    [Header("Interaction")]
    public GameObject gemPrefab;
    public int minGems = 1;
    public int maxGems = 3;

    [Header("Audio / Light")]
    public AudioClip availableClip;
    public float audioVolume = 1f;
    public Light pointLight;
    public AudioSource audioSource; // optional: if not set, a lightweight one will be added at runtime

    [Header("Trigger / Range")]
    // chest should have a trigger collider (e.g. SphereCollider) set to "Is Trigger"
    // this flag is set when the player is inside that trigger
    private bool playerInRange = false;
    [Header("Interaction Range")]
    public float interactDistance = 8f;
    [Tooltip("Maximum angle (degrees) between camera forward and barrel direction to allow interaction")]
    public float lookAngleThreshold = 25f;

    private PlayerMovement cachedPlayerMovement;
    private Transform cachedPlayerCamera;

    private bool isAvailable = false;
    // When set (>0) the availability routine will use this value for the next wait
    private float nextWait = -1f;
    private Coroutine audioFadeCoroutine;
    public float audioFadeDuration = 0.5f;

    void Start()
    {
        // Ensure sensible bounds
        minAvailableTime = Mathf.Max(0.1f, minAvailableTime);
        maxAvailableTime = Mathf.Max(minAvailableTime, maxAvailableTime);

        // Ensure an AudioSource is present if we have a clip
        if (audioSource == null && availableClip != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            // Use 3D spatial audio so we can limit audible range
            audioSource.spatialBlend = 1f; // 1 = fully 3D
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 0.1f;
            audioSource.maxDistance = 35f; // audible up to 35 meters
            audioSource.dopplerLevel = 0f;
            audioSource.clip = availableClip;
        }
        else if (audioSource != null && audioSource.clip == null && availableClip != null)
        {
            audioSource.clip = availableClip;
            audioSource.loop = true;
            // If an existing AudioSource was provided, ensure it's configured for 3D short-range playback
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 0.1f;
            audioSource.maxDistance = 35f;
            audioSource.dopplerLevel = 0f;
        }

        // Start availability loop
        StartCoroutine(AvailabilityRoutine());

        // Ensure the point light starts off until the barrel becomes available
        if (pointLight != null)
            pointLight.enabled = false;

        // Cache player camera early so looting works by distance/look without entering trigger
        if (cachedPlayerCamera == null)
        {
            PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null)
                cachedPlayerCamera = pm.playerCamera != null ? pm.playerCamera : (Camera.main != null ? Camera.main.transform : null);
            else if (Camera.main != null)
                cachedPlayerCamera = Camera.main.transform;
        }
    }

    IEnumerator AvailabilityRoutine()
    {
        while (true)
        {
            // Wait a random amount before the chest becomes available.
            // Use a nextWait explicitly set after looting when present.
            float wait = (nextWait > 0f) ? nextWait : Random.Range(minAvailableTime, maxAvailableTime);
            nextWait = -1f;
            yield return new WaitForSeconds(wait);

            // Make chest available
            SetAvailable(true);

            // Remain available until looted. This yields each frame until isAvailable becomes false.
            while (isAvailable)
                yield return null;

            // After looted, loop to choose a new wait time
        }
    }

    void SetAvailable(bool avail)
    {
        isAvailable = avail;

        if (isAvailable)
        {
            if (audioSource != null)
            {
                audioSource.loop = true;
                // Fade in to configured volume
                if (audioFadeCoroutine != null)
                    StopCoroutine(audioFadeCoroutine);
                audioFadeCoroutine = StartCoroutine(FadeAudioIn(audioFadeDuration));
            }

            if (pointLight != null)
                pointLight.enabled = true;

            // Optional: add visual feedback here (animation, particle, UI, etc.)
        }
        else
        {
            if (audioSource != null)
            {
                if (audioFadeCoroutine != null)
                    StopCoroutine(audioFadeCoroutine);
                audioFadeCoroutine = StartCoroutine(FadeAudioOutAndStop(audioFadeDuration));
            }

            if (pointLight != null)
                pointLight.enabled = false;
        }
    }

    IEnumerator FadeAudioIn(float duration)
    {
        if (audioSource == null)
            yield break;

        float start = 0f;
        float target = Mathf.Clamp01(audioVolume);
        audioSource.volume = 0f;
        if (!audioSource.isPlaying) audioSource.Play();

        if (duration <= 0f)
        {
            audioSource.volume = target;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        audioSource.volume = target;
        audioFadeCoroutine = null;
    }

    IEnumerator FadeAudioOutAndStop(float duration)
    {
        if (audioSource == null)
            yield break;

        float start = audioSource.volume;
        float target = 0f;

        if (duration <= 0f)
        {
            audioSource.Stop();
            audioSource.volume = Mathf.Clamp01(audioVolume);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        audioSource.volume = Mathf.Clamp01(audioVolume);
        audioSource.Stop();
        audioFadeCoroutine = null;
    }

    void Update()
    {
        if (!isAvailable)
            return;

        // Ensure we have a player camera reference even if the player never entered the trigger
        if (cachedPlayerCamera == null)
        {
            PlayerMovement pmf = FindFirstObjectByType<PlayerMovement>();
            if (pmf != null)
            {
                cachedPlayerMovement = pmf;
                cachedPlayerCamera = pmf.playerCamera != null ? pmf.playerCamera : (Camera.main != null ? Camera.main.transform : null);
            }
            else if (Camera.main != null)
            {
                cachedPlayerCamera = Camera.main.transform;
            }
        }

        // Check for interaction input while available and looking at barrel
        bool interact = false;

        // New Input System checks
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame)
                interact = true;
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.E))
                interact = true;
        }

        if (!interact)
        {
            if (Gamepad.current != null)
            {
                // Use Xbox "X" button (buttonWest) for interaction
                if (Gamepad.current.buttonWest.wasPressedThisFrame)
                    interact = true;
            }
            else
            {
                // Fallback joystick mapping: X is often JoystickButton2
                if (Input.GetKeyDown(KeyCode.JoystickButton2))
                    interact = true;
            }
        }

        if (interact && IsPlayerLookingAtBarrel())
        {
            Loot();
        }
    }

    private void Loot()
    {
        int count = Random.Range(minGems, maxGems + 1);

        for (int i = 0; i < count; i++)
        {
            if (gemPrefab != null)
            {
                Vector3 spawnPos = transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0.6f + i * 0.1f, Random.Range(-0.5f, 0.5f));
                GameObject g = Instantiate(gemPrefab, spawnPos, Quaternion.identity);
                Rigidbody rb = g.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 impulse = new Vector3(Random.Range(-0.5f, 0.5f), 1f, Random.Range(-0.5f, 0.5f)) * 3f;
                    rb.AddForce(impulse, ForceMode.VelocityChange);
                }
            }
            else
            {
                // Fallback placeholder gem if none assigned
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.localScale = Vector3.one * 0.25f;
                sphere.transform.position = transform.position + new Vector3(Random.Range(-0.5f, 0.5f), 0.6f, Random.Range(-0.5f, 0.5f));
                Rigidbody rb = sphere.AddComponent<Rigidbody>();
                rb.mass = 0.2f;
                rb.AddForce(new Vector3(Random.Range(-0.5f, 0.5f), 1f, Random.Range(-0.5f, 0.5f)) * 3f, ForceMode.VelocityChange);
                sphere.name = "Gem_Placeholder";
            }
        }

        // Chest has been looted; disable availability and revert audio/light
        SetAvailable(false);

        // Ensure the next availability is randomized now that the barrel was used
        nextWait = Random.Range(minAvailableTime, maxAvailableTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Accept either player-tagged objects or objects with PlayerMovement component
        PlayerMovement pm = other.GetComponentInParent<PlayerMovement>();
        if (pm != null || other.CompareTag("Player"))
        {
            playerInRange = true;
            if (pm != null)
            {
                cachedPlayerMovement = pm;
                if (pm.playerCamera != null)
                    cachedPlayerCamera = pm.playerCamera;
                else if (Camera.main != null)
                    cachedPlayerCamera = Camera.main.transform;
            }
            else
            {
                if (Camera.main != null)
                    cachedPlayerCamera = Camera.main.transform;
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        PlayerMovement pm = other.GetComponentInParent<PlayerMovement>();
        if (pm != null || other.CompareTag("Player"))
        {
            playerInRange = false;
            cachedPlayerMovement = null;
            cachedPlayerCamera = null;
        }
    }

    private bool IsPlayerLookingAtBarrel()
    {
        Transform cam = cachedPlayerCamera != null ? cachedPlayerCamera : (Camera.main != null ? Camera.main.transform : null);
        if (cam == null) return false;
        Vector3 toBarrel = transform.position - cam.position;
        float distance = toBarrel.magnitude;
        if (distance > interactDistance)
            return false;

        Vector3 dir = toBarrel.normalized;

        // Allow if camera forward is within a reasonable cone towards the barrel
        float angle = Vector3.Angle(cam.forward, dir);
        if (angle > lookAngleThreshold)
            return false;

        // Check line of sight: raycast towards barrel center and allow if nothing blocks
        if (Physics.Raycast(cam.position, dir, out RaycastHit hitInfo, distance, ~0, QueryTriggerInteraction.Collide))
        {
            if (hitInfo.collider != null)
            {
                return hitInfo.collider.transform.IsChildOf(transform) || hitInfo.collider.transform == transform;
            }
        }

        // If raycast didn't hit anything, assume clear line-of-sight
        return true;
    }
}
