using UnityEngine;

public class HealingTower : MonoBehaviour
{
    public int healAmount = 8;
    public float healInterval = 2f;
    public AudioClip healSound;
    public float healVolume = 0.5f;

    private PlayerHealth currentPlayer;
    private float nextHealTime;
    private AudioSource audioSource;

    void Update()
    {
        if (currentPlayer == null) return;

        if (Time.time >= nextHealTime)
        {
            currentPlayer.Heal(healAmount);
            if (healSound != null && audioSource != null)
                audioSource.PlayOneShot(healSound, healVolume);
            nextHealTime = Time.time + healInterval;
        }
    }

    void Start()
    {
        if (healSound != null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null) return;

        currentPlayer = playerHealth;
        nextHealTime = Time.time + healInterval;
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = other.GetComponentInParent<PlayerHealth>();

        if (playerHealth == null) return;

        if (currentPlayer == playerHealth)
            currentPlayer = null;
    }
}
