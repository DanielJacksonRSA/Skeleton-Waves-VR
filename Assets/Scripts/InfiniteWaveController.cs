using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class InfiniteWaveController : MonoBehaviour
{
    [Header("Core References")]
    public GameObject skeletonPrefab;
    public PlayerHealth playerHealth;
    public PlayerMovement playerMovement;

    [Header("Spawn Setup")]
    public Transform[] spawnPoints;
    public bool autoUseExistingEnemyPositions = true;
    public bool clearExistingEnemiesOnStart = true;
    public float fallbackSpawnRadius = 18f;
    public float spawnHeightOffset = 0.5f;
    public float timeBetweenWaves = 2f;
    public float spawnInterval = 0.08f;

    [Header("Wave Audio")]
    public AudioClip waveStartClip;
    public AudioSource waveAudioSource;
    [Tooltip("Seconds to wait before each new wave starts (plays wave start sound at the end of the delay).")]
    public float preWaveDelay = 5f;

    [Header("Music")]
    [Tooltip("Music audio source used for background tracks. Will be created if missing.")]
    public AudioSource musicSource;
    [Tooltip("Three (or more) clips to play sequentially. The player will hear them at a constant volume.")]
    public AudioClip[] musicClips;
    [Range(0f,1f)] public float musicVolume = 0.25f;

    [Header("Wave Scaling")]
    public int startingSkeletons = 1;
    public float skeletonGrowthExponent = 1.65f;
    public float skeletonGrowthMultiplier = 1.7f;

    public float aiSpeedStart = 2.8f;
    public float aiSpeedIncreasePerWave = 0.4f;
    [Range(0.5f, 0.99f)] public float aiMaxSpeedAsPlayerSprintRatio = 0.9f;

    public int startingDamage = 8;
    public int damageIncreasePerWave = 2;

    [Range(30f, 100f)] public float startingGemDropChance = 85f;
    public float gemDropDecreasePerWave = 1f;

    public int hiveMindStartsOnWave = 3;

    [Header("Runtime (Read Only)")]
    [SerializeField] private int currentWave = 0;
    [SerializeField] private int completedWaves = 0;
    [SerializeField] private int enemiesAlive = 0;

    [Header("UI")]
    [SerializeField] private TMP_Text nowWaveLabel;

    private readonly List<GameObject> aliveWaveEnemies = new List<GameObject>();
    private readonly List<Vector3> cachedSpawnPositions = new List<Vector3>();
    private Coroutine waveLoopRoutine;
    private Coroutine musicLoopRoutine;
    private bool wasPlayerAliveAtStart = false;

    public int CompletedWaves => completedWaves;

    private struct WaveSettings
    {
        public int waveIndex;
        public int skeletonCount;
        public float aiSpeed;
        public bool hiveMindEnabled;
        public int attackDamage;
        public float gemDropChance;
    }

    private void Start()
    {
        ResolveReferences();
        CacheWaveLabel();
        UpdateWaveLabel();

        if (skeletonPrefab == null)
        {
            Debug.LogError("InfiniteWaveController: Assign a Skeleton prefab before starting the game.");
            enabled = false;
            return;
        }

        CacheSpawnPositionsFromExistingEnemies();

        if (clearExistingEnemiesOnStart)
            ClearExistingEnemies();

        // Start wave loop
        waveLoopRoutine = StartCoroutine(WaveLoop());

        // Initialize and start background music loop (if clips assigned)
        if (musicClips != null && musicClips.Length > 0)
        {
            if (musicSource == null)
            {
                musicSource = GetComponent<AudioSource>();
                if (musicSource == null)
                {
                    musicSource = gameObject.AddComponent<AudioSource>();
                    musicSource.playOnAwake = false;
                }
            }

            // Ensure 2D audio so volume stays constant regardless of listener position
            musicSource.spatialBlend = 0f;
            musicSource.volume = musicVolume;
            musicSource.loop = false;

            wasPlayerAliveAtStart = IsPlayerAlive();
            if (wasPlayerAliveAtStart)
                musicLoopRoutine = StartCoroutine(MusicLoop());
        }
    }

    private void Update()
    {
        // Monitor player death to stop music immediately when player dies
        bool currentlyAlive = IsPlayerAlive();
        if (wasPlayerAliveAtStart && !currentlyAlive)
        {
            StopMusic();
        }
        wasPlayerAliveAtStart = currentlyAlive;
    }

    private void OnDisable()
    {
        if (waveLoopRoutine != null)
            StopCoroutine(waveLoopRoutine);

        StopMusic();
    }

    private void ResolveReferences()
    {
        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();

        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<PlayerMovement>();
    }

    private void CacheWaveLabel()
    {
        if (nowWaveLabel != null)
            return;

        nowWaveLabel = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "NowWave");
    }

    private void UpdateWaveLabel()
    {
        if (nowWaveLabel == null)
        {
            CacheWaveLabel();
            if (nowWaveLabel == null)
                return;
        }

        nowWaveLabel.text = "Wave: " + Mathf.Max(0, currentWave).ToString();
    }

    private IEnumerator WaveLoop()
    {
        while (IsPlayerAlive())
        {
            if (preWaveDelay > 0f)
            {
                yield return new WaitForSeconds(preWaveDelay);
            }

            if (waveStartClip != null)
            {
                if (waveAudioSource == null)
                {
                    waveAudioSource = GetComponent<AudioSource>();
                    if (waveAudioSource == null)
                    {
                        waveAudioSource = gameObject.AddComponent<AudioSource>();
                        waveAudioSource.playOnAwake = false;
                    }
                }

                waveAudioSource.PlayOneShot(waveStartClip);
            }
            currentWave++;
            UpdateWaveLabel();
            WaveSettings settings = BuildSettingsForWave(currentWave);

            yield return StartCoroutine(SpawnWave(settings));
            yield return StartCoroutine(WaitForWaveClear());

            if (!IsPlayerAlive())
                yield break;

            completedWaves = currentWave;

            if (timeBetweenWaves > 0f)
                yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    private IEnumerator SpawnWave(WaveSettings settings)
    {
        aliveWaveEnemies.Clear();
        enemiesAlive = 0;

        Debug.Log(
            $"Wave {settings.waveIndex} | Count: {settings.skeletonCount} | " +
            $"AI Speed: {settings.aiSpeed:F2} | HiveMind: {settings.hiveMindEnabled} | " +
            $"Damage: {settings.attackDamage} | GemDrop: {settings.gemDropChance:F1}%");

        for (int i = 0; i < settings.skeletonCount; i++)
        {
            if (!IsPlayerAlive())
                yield break;

            SpawnSkeleton(settings);
            enemiesAlive = aliveWaveEnemies.Count;

            if (spawnInterval > 0f)
                yield return new WaitForSeconds(spawnInterval);
        }
    }

    private IEnumerator WaitForWaveClear()
    {
        while (IsPlayerAlive())
        {
            CleanupDeadEnemies();
            enemiesAlive = aliveWaveEnemies.Count;

            if (enemiesAlive == 0)
                yield break;

            yield return null;
        }
    }

    private void SpawnSkeleton(WaveSettings settings)
    {
        Vector3 spawnPosition = GetSpawnPosition();
        Quaternion spawnRotation = skeletonPrefab.transform.rotation;
        GameObject spawnedEnemy = Instantiate(skeletonPrefab, spawnPosition, spawnRotation);

        EnemyAI enemyAI = spawnedEnemy.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.maxSpeed = settings.aiSpeed;
            enemyAI.speed = settings.aiSpeed;
            enemyAI.hiveMindEnabled = settings.hiveMindEnabled;
            enemyAI.attackDamage = settings.attackDamage;
        }

        AiHealth aiHealth = spawnedEnemy.GetComponent<AiHealth>();
        if (aiHealth != null)
        {
            aiHealth.currentHealth = aiHealth.maxHealth;
            aiHealth.gemDropChance = settings.gemDropChance;
        }

        aliveWaveEnemies.Add(spawnedEnemy);
    }

    private WaveSettings BuildSettingsForWave(int wave)
    {
        int skeletonCount = GetSkeletonCountForWave(wave);

        float playerSprintSpeed = GetPlayerSprintSpeed();
        float maxAiSpeedAllowed = Mathf.Max(0.1f, playerSprintSpeed * aiMaxSpeedAsPlayerSprintRatio);
        float desiredSpeed = aiSpeedStart + (wave - 1) * aiSpeedIncreasePerWave;
        float aiSpeed = Mathf.Min(desiredSpeed, maxAiSpeedAllowed);

        int attackDamage = Mathf.Clamp(startingDamage + (wave - 1) * damageIncreasePerWave, 1, 40);
        float gemDropChance = Mathf.Clamp(startingGemDropChance - (wave - 1) * gemDropDecreasePerWave, 30f, 100f);

        // Alternates between coordinated and non-coordinated waves so hive mind toggles on/off.
        bool hiveMindEnabled = wave >= hiveMindStartsOnWave && (wave % 3 != 0);

        return new WaveSettings
        {
            waveIndex = wave,
            skeletonCount = skeletonCount,
            aiSpeed = aiSpeed,
            hiveMindEnabled = hiveMindEnabled,
            attackDamage = attackDamage,
            gemDropChance = gemDropChance
        };
    }

    private int GetSkeletonCountForWave(int wave)
    {
        if (wave <= 1)
            return Mathf.Max(1, startingSkeletons);

        float growth = Mathf.Pow(wave - 1, skeletonGrowthExponent) * skeletonGrowthMultiplier;
        return Mathf.Max(1, Mathf.RoundToInt(startingSkeletons + growth));
    }

    private float GetPlayerSprintSpeed()
    {
        if (playerMovement == null)
            return 12f;

        float sprint = playerMovement.speed * playerMovement.runMultiplier;
        return Mathf.Max(1f, sprint);
    }

    private bool IsPlayerAlive()
    {
        if (playerHealth == null)
            return false;

        if (!playerHealth.gameObject.activeInHierarchy)
            return false;

        return playerHealth.currentHealth > 0;
    }

    private void CleanupDeadEnemies()
    {
        for (int i = aliveWaveEnemies.Count - 1; i >= 0; i--)
        {
            GameObject enemy = aliveWaveEnemies[i];
            if (enemy == null || !enemy.activeInHierarchy)
            {
                aliveWaveEnemies.RemoveAt(i);
                continue;
            }

            AiHealth aiHealth = enemy.GetComponent<AiHealth>();
            if (aiHealth == null || aiHealth.currentHealth <= 0)
                aliveWaveEnemies.RemoveAt(i);
        }
    }

    private Vector3 GetSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform point = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (point != null)
                return point.position + Vector3.up * spawnHeightOffset;
        }

        if (cachedSpawnPositions.Count > 0)
        {
            Vector3 cachedPoint = cachedSpawnPositions[Random.Range(0, cachedSpawnPositions.Count)];
            return cachedPoint + Vector3.up * spawnHeightOffset;
        }

        Vector2 randomCircle = Random.insideUnitCircle * fallbackSpawnRadius;
        return transform.position + new Vector3(randomCircle.x, spawnHeightOffset, randomCircle.y);
    }

    private void CacheSpawnPositionsFromExistingEnemies()
    {
        cachedSpawnPositions.Clear();

        if (!autoUseExistingEnemyPositions)
            return;

        EnemyAI[] existingEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in existingEnemies)
        {
            if (enemy == null)
                continue;

            cachedSpawnPositions.Add(enemy.transform.position);
        }
    }

    private void ClearExistingEnemies()
    {
        EnemyAI[] existingEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (EnemyAI enemy in existingEnemies)
        {
            if (enemy == null)
                continue;

            Destroy(enemy.gameObject);
        }
    }

    private IEnumerator MusicLoop()
    {
        if (musicClips == null || musicClips.Length == 0 || musicSource == null)
            yield break;

        int index = 0;
        while (IsPlayerAlive())
        {
            AudioClip clip = musicClips[index % musicClips.Length];
            index++;

            if (clip != null)
            {
                musicSource.clip = clip;
                musicSource.volume = musicVolume;
                musicSource.spatialBlend = 0f; // ensure 2D
                musicSource.Play();

                // Wait for clip to finish or stop earlier if player dies
                float remaining = clip.length;
                while (remaining > 0f)
                {
                    if (!IsPlayerAlive())
                        yield break;

                    remaining -= Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                // Avoid tight loop on null clip
                yield return null;
            }
        }
    }

    private void StopMusic()
    {
        if (musicLoopRoutine != null)
        {
            StopCoroutine(musicLoopRoutine);
            musicLoopRoutine = null;
        }

        if (musicSource != null && musicSource.isPlaying)
            musicSource.Stop();
    }
}