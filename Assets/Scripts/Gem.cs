using UnityEngine;

public class Gem : MonoBehaviour
{
    public float despawnTime = 30f;
    private float timeRemaining;
    private Light pointLight;

    void Start()
    {
        timeRemaining = despawnTime;
        
        // Find the Point Light child
        pointLight = GetComponentInChildren<Light>();
        if (pointLight == null)
        {
            Debug.LogWarning("Point Light not found on gem or its children!");
        }
    }

    void Update()
    {
        timeRemaining -= Time.deltaTime;
        
        if (timeRemaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        
        // Flash the light, speeding up as time runs out
        if (pointLight != null)
        {
            // Calculate flash speed based on time remaining
            // At start (30s): slow flash, at end (0s): very fast flash
            float progress = 1f - (timeRemaining / despawnTime); // 0 to 1
            float flashSpeed = Mathf.Lerp(0.5f, 4f, progress); // 0.5 to 4 Hz
            
            // Use sine wave for smooth on/off
            float lightIntensity = Mathf.Sin(Time.time * flashSpeed * Mathf.PI) * 0.5f + 0.5f;
            pointLight.intensity = lightIntensity * 2f; // Scale to visible range
        }
    }
}

