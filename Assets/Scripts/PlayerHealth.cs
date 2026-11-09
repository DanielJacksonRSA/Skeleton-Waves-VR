using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
using TMPro;
using System;
using System.Reflection;
using System.Linq;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;
    public InfiniteWaveController waveController;
    public PlayerMovement playerMovement;
    public WeaponManager weaponManager;
    public GemCollector gemCollector;
    public WishingWell wishingWell;
    [Header("Audio")]
    public AudioClip damageSound;
    public float damageSoundVolume = 1f;
    public AudioClip deathSound;
    public float deathSoundVolume = 1f;
    [Header("Responsive HUD")]
    public int lowHealthThreshold = 30;
    public float lowHealthPulseSpeed = 1.5f;
    public Transform compassReference;
    public float compassRefreshInterval = 0.1f;

    private Slider healthBar;
    private Text healthBarLabel;
    private TextMeshProUGUI compassArrow;
    private RectTransform compassRectTransform;
    private Vector2 compassArrowAnchoredPosition;
    private bool hasCompassArrowAnchoredPosition;
    private CanvasGroup deathPanel;
    private TextMeshProUGUI deathTimeLabel;
    private TextMeshProUGUI deathWavesLabel;
    private Button deathPlayAgainButton;
    private AudioSource audioSource;
    private float compassRefreshTimer;
    private float runStartTime;
    private bool isDead;
    private bool isPlayAgainHandlerRegistered;

    void Awake()
    {
        // Keep runtime health initialized before other Start() methods query it.
        currentHealth = maxHealth;
        runStartTime = Time.time;
        isDead = false;
    }

    void OnEnable()
    {
        CacheHudElements();
    }

    void OnDisable()
    {
        if (deathPlayAgainButton != null && isPlayAgainHandlerRegistered)
        {
            deathPlayAgainButton.onClick.RemoveListener(OnPlayAgainClicked);
            isPlayAgainHandlerRegistered = false;
        }

    }

    void Update()
    {
        if (isDead)
            return;

        UpdateLowHealthPulse();
        UpdateEnemyCompass();
    }

    void Start()
    {
        currentHealth = maxHealth;
        runStartTime = Time.time;
        isDead = false;
        ResolveGameplayReferences();
        CacheHudElements();
        // Cache optional AudioSource on the player for one-shot playback
        audioSource = GetComponent<AudioSource>();
        UpdateUI();
    }

    private void CacheHudElements()
    {
        // Cache health bar
        healthBar = FindObjectsByType<Slider>(FindObjectsSortMode.None)
            .FirstOrDefault(s => s.name == "PlayerHealthBar");
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBarLabel = healthBar.GetComponentInChildren<Text>();
            UpdateUI();
        }

        // Cache compass element
        compassArrow = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "Compas");
        if (compassArrow == null)
        {
            // Try with flexible name matching
            compassArrow = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.gameObject.name.Contains("Compass") || t.gameObject.name.Contains("compass"));
        }

        if (compassArrow != null)
        {
            compassRectTransform = compassArrow.rectTransform;
            if (compassRectTransform != null)
            {
                if (!hasCompassArrowAnchoredPosition)
                {
                    compassArrowAnchoredPosition = compassRectTransform.anchoredPosition;
                    hasCompassArrowAnchoredPosition = true;
                }

                compassRectTransform.pivot = new Vector2(0.5f, 0.5f);
                compassRectTransform.anchoredPosition = compassArrowAnchoredPosition;
            }
        }

        // Cache death screen canvas elements
        Canvas deathCanvas = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .FirstOrDefault(c => c.name == "YouDied_Canvas");
        
        if (deathCanvas != null)
        {
            deathPanel = deathCanvas.GetComponent<CanvasGroup>();
            if (deathPanel == null)
            {
                deathPanel = deathCanvas.gameObject.AddComponent<CanvasGroup>();
            }
            
            if (deathPanel != null && !isDead)
                deathPanel.alpha = 0;

            // Find death UI elements within the death canvas (TMP labels named Time and Wave)
            TextMeshProUGUI[] deathLabels = deathCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
            deathTimeLabel = deathLabels.FirstOrDefault(t => t.name == "Time" || t.name == "DeathTimeLabel");
            deathWavesLabel = deathLabels.FirstOrDefault(t => t.name == "Wave" || t.name == "DeathWavesLabel");
            deathPlayAgainButton = deathCanvas.GetComponentInChildren<Button>(true);
        }

        if (deathPlayAgainButton != null && !isPlayAgainHandlerRegistered)
        {
            deathPlayAgainButton.onClick.AddListener(OnPlayAgainClicked);
            isPlayAgainHandlerRegistered = true;
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateUI();

        // Play damage sound when the player is hurt
        if (damageSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(damageSound, damageSoundVolume);
            }
            else
            {
                AudioSource.PlayClipAtPoint(damageSound, transform.position, damageSoundVolume);
            }
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead)
            return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateUI();
    }

    void UpdateUI()
    {
        if (healthBar != null)
        {
            healthBar.value = currentHealth;
            if (healthBarLabel != null)
                healthBarLabel.text = $"{currentHealth} / {maxHealth}";
        }
    }

    void UpdateLowHealthPulse()
    {
        if (healthBar == null)
            return;

        Image fillImage = healthBar.fillRect?.GetComponent<Image>();
        if (fillImage == null)
            return;

        if (currentHealth <= lowHealthThreshold)
        {
            float t = Mathf.PingPong(Time.time * lowHealthPulseSpeed, 1f);
            Color c = fillImage.color;
            c.a = Mathf.Lerp(0.5f, 1f, t);
            fillImage.color = c;
            return;
        }

        Color normal = fillImage.color;
        normal.a = 0.75f;
        fillImage.color = normal;
    }

    void UpdateEnemyCompass()
    {
        if (compassArrow == null)
        {
            return;
        }

        compassRefreshTimer -= Time.deltaTime;
        if (compassRefreshTimer > 0f)
        {
            return;
        }

        compassRefreshTimer = compassRefreshInterval;

        EnemyAI nearestEnemy = FindNearestEnemy();
        if (nearestEnemy == null)
        {
            compassArrow.text = ".";
            compassArrow.gameObject.SetActive(true);
            RectTransform noTargetCompassRect = compassRectTransform != null ? compassRectTransform : compassArrow.rectTransform;
            if (noTargetCompassRect != null)
                noTargetCompassRect.localRotation = Quaternion.identity;
            return;
        }

        Transform reference = GetCompassReference();
        Vector3 referencePosition = reference != null ? reference.position : transform.position;
        Vector3 toEnemy = nearestEnemy.transform.position - referencePosition;
        toEnemy.y = 0f;

        float distance = toEnemy.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            compassArrow.text = ".";
            compassArrow.gameObject.SetActive(true);
            return;
        }

        Vector3 direction = toEnemy / distance;
        Vector3 referenceForward = reference != null ? reference.forward : transform.forward;
        Vector3 referenceRight = reference != null ? reference.right : transform.right;

        referenceForward.y = 0f;
        referenceRight.y = 0f;

        if (referenceForward.sqrMagnitude <= Mathf.Epsilon)
            referenceForward = transform.forward;

        if (referenceRight.sqrMagnitude <= Mathf.Epsilon)
            referenceRight = transform.right;

        referenceForward.Normalize();
        referenceRight.Normalize();

        // Positive angle means target is to the player's right, negative to the left.
        float angle = Mathf.Atan2(Vector3.Dot(referenceRight, direction), Vector3.Dot(referenceForward, direction)) * Mathf.Rad2Deg;

        compassArrow.text = GetCompassArrowGlyph(angle);
        compassArrow.gameObject.SetActive(true);
        RectTransform compassRect = compassRectTransform != null ? compassRectTransform : compassArrow.rectTransform;
        if (compassRect != null)
        {
            if (!hasCompassArrowAnchoredPosition)
            {
                compassArrowAnchoredPosition = compassRect.anchoredPosition;
                hasCompassArrowAnchoredPosition = true;
            }

            compassRect.anchoredPosition = compassArrowAnchoredPosition;
            // Keep the UI element stationary. Direction is shown by glyph only.
            compassRect.localRotation = Quaternion.identity;
            compassRect.anchoredPosition = compassArrowAnchoredPosition;
        }
    }

    private static string GetCompassArrowGlyph(float angle)
    {
        if (angle >= -45f && angle < 45f)
            return "↑";
        if (angle >= 45f && angle < 135f)
            return "→";
        if (angle >= 135f || angle < -135f)
            return "↓";
        return "←";
    }

    private EnemyAI FindNearestEnemy()
    {
        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        if (enemies == null || enemies.Length == 0)
        {
            return null;
        }

        Transform reference = GetCompassReference();
        Vector3 referencePosition = reference != null ? reference.position : transform.position;
        EnemyAI nearestEnemy = null;
        float bestDistance = float.MaxValue;

        foreach (EnemyAI enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled)
            {
                continue;
            }

            AiHealth aiHealth = enemy.GetComponent<AiHealth>();
            if (aiHealth != null && aiHealth.currentHealth <= 0)
            {
                continue;
            }

            // Use horizontal distance so target selection matches compass yaw-only guidance.
            Vector3 toEnemy = enemy.transform.position - referencePosition;
            toEnemy.y = 0f;
            float distance = toEnemy.sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearestEnemy = enemy;
            }
        }

        return nearestEnemy;
    }

    private Transform GetCompassReference()
    {
        if (IsHeadsetTrackingActive() && TryGetHeadsetTransform(out Transform headsetTransform))
        {
            return headsetTransform;
        }

        if (compassReference != null)
        {
            return compassReference;
        }

        if (TryGetHeadsetTransform(out Transform fallbackCameraTransform))
        {
            return fallbackCameraTransform;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera.transform;
        }

        return transform;
    }

    private bool TryGetHeadsetTransform(out Transform headsetTransform)
    {
        if (playerMovement != null && playerMovement.playerCamera != null)
        {
            headsetTransform = playerMovement.playerCamera;
            return true;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            headsetTransform = mainCamera.transform;
            return true;
        }

        GameObject namedMainCamera = GameObject.Find("Main Camera");
        if (namedMainCamera != null)
        {
            Camera namedCamera = namedMainCamera.GetComponent<Camera>();
            if (namedCamera != null)
            {
                headsetTransform = namedCamera.transform;
                return true;
            }
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.isActiveAndEnabled)
            {
                headsetTransform = camera.transform;
                return true;
            }
        }

        headsetTransform = null;
        return false;
    }

    private static bool IsHeadsetTrackingActive()
    {
        InputDevice centerEye = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        if (centerEye.isValid)
            return true;

        if (XRSettings.enabled && XRSettings.isDeviceActive)
            return true;

        return false;
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;
        currentHealth = 0;
        UpdateUI();

        // Play death sound if assigned
        if (deathSound != null)
        {
            if (audioSource != null)
                audioSource.PlayOneShot(deathSound, deathSoundVolume);
            else
                AudioSource.PlayClipAtPoint(deathSound, transform.position, deathSoundVolume);
        }

        ShowDeathPanel();
        DisablePlayerControl();
    }

    private void ResolveGameplayReferences()
    {
        if (waveController == null)
            waveController = FindFirstObjectByType<InfiniteWaveController>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (weaponManager == null)
            weaponManager = GetComponent<WeaponManager>();

        if (gemCollector == null)
            gemCollector = GetComponent<GemCollector>();

        if (wishingWell == null)
            wishingWell = FindFirstObjectByType<WishingWell>();
    }

    private void DisablePlayerControl()
    {
        ResolveGameplayReferences();

        if (playerMovement != null)
        {
            Rigidbody playerBody = playerMovement.GetComponent<Rigidbody>();
            if (playerBody != null)
            {
                playerBody.linearVelocity = Vector3.zero;
                playerBody.angularVelocity = Vector3.zero;
            }

            playerMovement.enabled = false;
        }

        if (weaponManager != null)
            weaponManager.enabled = false;

        if (gemCollector != null)
            gemCollector.enabled = false;

        if (wishingWell != null)
            wishingWell.enabled = false;

        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
    }

    private void ShowDeathPanel()
    {
        CacheHudElements();
        ResolveGameplayReferences();

        float survivedSeconds = Mathf.Max(0f, Time.time - runStartTime);
        int survivedWaves = waveController != null ? Mathf.Max(0, waveController.CompletedWaves) : 0;

        if (deathTimeLabel != null)
            deathTimeLabel.text = $"Time Survived: {FormatSurvivalTime(survivedSeconds)}";

        if (deathWavesLabel != null)
            deathWavesLabel.text = $"Waves Survived: {survivedWaves}";

        if (deathPanel != null)
        {
            deathPanel.gameObject.SetActive(true);
            deathPanel.alpha = 1;
        }
    }

    private static string FormatSurvivalTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return $"{minutes:00}:{remainingSeconds:00}";
    }

    private void OnPlayAgainClicked()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
            SceneManager.LoadScene(activeScene.buildIndex);
        else
            SceneManager.LoadScene(activeScene.name);
    }
}