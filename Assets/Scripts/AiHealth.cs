using UnityEngine;
using System.Collections;

public class AiHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth = 100;
    public bool destroyOnDeath = true;
    public float despawnDelay = 0f;
    public Animator animator;
    public string isDeadTriggerParameter = "IsDead";
    public bool freezeRigidbodyOnDeath = true;
    public bool waitForDeathAnimationEnd = true;
    public int deathAnimationLayer = 0;
    public string deathAnimationStateName = "Loose";
    
    [Header("Gem Drop")]
    public float gemDropChance = 30f; // 0-100 percentage chance
    public GameObject gemPrefab;

    [Header("Audio")]
    public AudioClip[] damageSounds;
    private AudioSource audioSource;

    private bool isDead = false;
    public bool testContinuousDamage = false;
    private float testDamageTimer = 0f;
    private float testDamageInterval = 1f;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    void Update()
    {
        if (testContinuousDamage)
        {
            testDamageTimer -= Time.deltaTime;
            if (testDamageTimer <= 0f)
            {
                TakeDamage(10);
                testDamageTimer = testDamageInterval;
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        PlayDamageSound();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        currentHealth = 0;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            if (freezeRigidbodyOnDeath)
                rb.isKinematic = true;
        }

        if (animator != null && HasAnimatorParameter(isDeadTriggerParameter, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(isDeadTriggerParameter);

        // Disable behaviour scripts so the AI stops moving/attacking immediately.
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != this)
                behaviour.enabled = false;
        }
        
        // Drop gem if chance succeeds
        DropGem();

        StartCoroutine(DespawnAfterDeathAnimation());
    }

    private IEnumerator DespawnAfterDeathAnimation()
    {
        if (waitForDeathAnimationEnd && animator != null)
            yield return WaitForDeathAnimationToFinish();

        if (despawnDelay > 0f)
            yield return new WaitForSeconds(despawnDelay);

        if (destroyOnDeath)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private IEnumerator WaitForDeathAnimationToFinish()
    {
        int layer = Mathf.Max(0, deathAnimationLayer);
        bool enteredDeathState = false;
        int deathStateHash = string.IsNullOrEmpty(deathAnimationStateName) ? 0 : Animator.StringToHash(deathAnimationStateName);

        while (true)
        {
            if (animator == null)
                yield break;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
            bool inDeathState = IsDeathState(state, deathStateHash);

            if (inDeathState)
            {
                enteredDeathState = true;
            }
            else if (enteredDeathState)
            {
                // Once the death state exits, the Animator has reached its next node (often Exit).
                yield break;
            }

            yield return null;
        }
    }

    private bool IsDeathState(AnimatorStateInfo state, int deathStateHash)
    {
        return deathStateHash != 0 && state.shortNameHash == deathStateHash;
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName)) return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == parameterType && parameter.name == parameterName)
                return true;
        }

        return false;
    }

    private void DropGem()
    {
        if (gemPrefab == null) return;
        
        // Roll for gem drop based on chance
        float roll = Random.Range(0f, 100f);
        if (roll < gemDropChance)
        {
            // Instantiate gem above the AI's position
            Vector3 dropPosition = transform.position + Vector3.up * 6f;
            Instantiate(gemPrefab, dropPosition, Quaternion.identity);
        }
    }

    private void PlayDamageSound()
    {
        if (audioSource == null || damageSounds == null || damageSounds.Length == 0)
            return;

        // Select a random audio clip from the array
        AudioClip randomClip = damageSounds[Random.Range(0, damageSounds.Length)];
        audioSource.PlayOneShot(randomClip);
    }
}
