using UnityEngine;

public class GemPickup : MonoBehaviour
{
    private float pickupBlockedUntil;
    public AudioClip collectSound;
    [Range(0f, 1f)]
    public float collectVolume = 1f;

    public void BlockPickup(float duration)
    {
        pickupBlockedUntil = Time.time + Mathf.Max(0f, duration);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < pickupBlockedUntil)
            return;

        // Check if the colliding object is the player
        if (other.CompareTag("Player"))
        {
            // Try to find the GemCollector on the player (search parents too)
            GemCollector gemCollector = other.GetComponentInParent<GemCollector>();
            
            if (gemCollector != null)
            {
                gemCollector.CollectGem();

                // Play collection sound at the gem's position (only when collected)
                if (collectSound != null)
                    AudioSource.PlayClipAtPoint(collectSound, transform.position, collectVolume);
            }
            else
            {
                Debug.LogWarning("GemCollector component not found on player!");
            }
            
            // Destroy the gem immediately
            Destroy(gameObject);
        }
    }
}
