using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using System.Linq;

public class GemCollector : MonoBehaviour
{
    private int gemsCollected = 0;
    public GameObject gemIconPrefab;
    public Color fallbackGemIconColor = new Color(0.3f, 0.9f, 0.45f, 1f);
    public Transform throwOrigin;
    public Transform throwLookReference;
    public float throwSpawnDistance = 1.2f;
    public float throwSpawnHeight = 0.2f;
    public float throwForce = 8f;
    public float throwUpwardForce = 1.25f;
    public float throwPickupBlockDuration = 0.25f;

    private Text gemCounterIcon;
    private TMP_Text gemsLabel;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    void OnEnable()
    {
        CacheHudElements();
        ApplyGemIconColor();
        UpdateGemCounterUI();
    }

    void Start()
    {
        if (throwLookReference == null && Camera.main != null)
            throwLookReference = Camera.main.transform;

        CacheHudElements();
        ApplyGemIconColor();
        UpdateGemCounterUI();
    }

    void Update()
    {
        ReadThrowInput();
    }

    public int GetGemsCollected()
    {
        return gemsCollected;
    }

    public void CollectGem()
    {
        gemsCollected++;
        UpdateGemCounterUI();
    }

    private void ReadThrowInput()
    {
        bool keyboardThrow = Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame;
        bool gamepadThrow = Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame;

        if (keyboardThrow || gamepadThrow)
            ThrowGem();
    }

    private void ThrowGem()
    {
        if (gemsCollected <= 0)
            return;

        if (gemIconPrefab == null)
        {
            Debug.LogWarning("Cannot throw gem: Gem prefab is not assigned.");
            return;
        }

        Transform look = ResolveThrowLookReference();
        Transform origin = throwOrigin != null ? throwOrigin : look;

        Vector3 lookDirection = look.forward;
        if (lookDirection.sqrMagnitude < 0.0001f)
            lookDirection = transform.forward;
        lookDirection.Normalize();

        Vector3 spawnPosition = origin.position + lookDirection * throwSpawnDistance + Vector3.up * throwSpawnHeight;
        Quaternion throwRotation = Quaternion.LookRotation(lookDirection, Vector3.up);

        GameObject thrownGem = Instantiate(gemIconPrefab, spawnPosition, throwRotation);
        Rigidbody gemRb = thrownGem.GetComponent<Rigidbody>();
        if (gemRb == null)
            gemRb = thrownGem.GetComponentInChildren<Rigidbody>();

        if (gemRb != null)
        {
            Vector3 launchVelocity = lookDirection * throwForce + Vector3.up * throwUpwardForce;
            gemRb.linearVelocity = Vector3.zero;
            gemRb.AddForce(launchVelocity, ForceMode.VelocityChange);
        }

        GemPickup pickup = thrownGem.GetComponent<GemPickup>();
        if (pickup == null)
            pickup = thrownGem.GetComponentInChildren<GemPickup>();

        if (pickup != null)
            pickup.BlockPickup(throwPickupBlockDuration);

        gemsCollected--;
        UpdateGemCounterUI();
    }

    private Transform ResolveThrowLookReference()
    {
        if (throwLookReference != null)
            return throwLookReference;

        if (Camera.main != null)
            return Camera.main.transform;

        if (throwOrigin != null)
            return throwOrigin;

        return transform;
    }

    private void CacheHudElements()
    {
        gemCounterIcon = FindObjectsByType<Text>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "GemCounter-Icon");

        gemsLabel = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == "Gems");

        if (gemsLabel == null)
        {
            Text legacyCounter = FindObjectsByType<Text>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "GemCounter-Value");

            if (legacyCounter != null)
                legacyCounter.text = "Gems: " + gemsCollected;
        }
    }

    private void UpdateGemCounterUI()
    {
        if (gemsLabel == null)
        {
            CacheHudElements();

            if (gemsLabel == null)
            {
                Text legacyCounter = FindObjectsByType<Text>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.name == "GemCounter-Value");
                if (legacyCounter != null)
                    legacyCounter.text = "Gems: " + gemsCollected;

                return;
            }
        }

        gemsLabel.text = "Gems: " + gemsCollected;
    }

    private void ApplyGemIconColor()
    {
        if (gemCounterIcon == null)
        {
            CacheHudElements();
            if (gemCounterIcon == null)
                return;
        }

        gemCounterIcon.color = ResolveGemIconColor();
    }

    private Color ResolveGemIconColor()
    {
        Renderer gemRenderer = FindGemRenderer();
        if (gemRenderer != null && gemRenderer.sharedMaterial != null)
        {
            Material mat = gemRenderer.sharedMaterial;
            if (mat.HasProperty(BaseColorId))
                return mat.GetColor(BaseColorId);
            if (mat.HasProperty(ColorId))
                return mat.GetColor(ColorId);
        }

        return fallbackGemIconColor;
    }

    private Renderer FindGemRenderer()
    {
        GameObject source = gemIconPrefab;
        if (source == null)
        {
            GameObject namedGem = GameObject.Find("Gemstone_Emerald");
            if (namedGem == null)
                namedGem = GameObject.Find("Gemstone_Emerald(Clone)");

            source = namedGem;
        }

        if (source == null)
        {
            Gem anyGem = FindFirstObjectByType<Gem>();
            if (anyGem != null)
                source = anyGem.gameObject;
        }

        if (source == null)
            return null;

        return source.GetComponentInChildren<Renderer>(true);
    }
}
