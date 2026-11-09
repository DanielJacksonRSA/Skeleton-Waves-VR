using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Linq;

public class WeaponManager : MonoBehaviour 
{
    public Transform weaponSocket;
    public Transform playerCamera;
    public float interactDistance = 3f;
    public LayerMask interactMask;
    private LayerMask raycastMaskNoPlayer;
    [Range(0f, 1f)] public float lowWeaponHealthThresholdPercent = 0.3f;
    public float lowWeaponPulseSpeed = 1.5f;

    private GameObject currentSword;
    private Sword equippedSword;
    private Sword highlightedSword;
    private Slider weaponHealthBar;
    private Text weaponHealthLabel;

    void OnEnable()
    {
        CacheHudElements();
    }

    void Start()
    {
        CacheHudElements();
        UpdateWeaponHealthUI();
        
        // Build layer mask that excludes the player layer
        if (interactMask == 0) interactMask = ~0;
        int playerLayer = LayerMask.NameToLayer("Player");
        raycastMaskNoPlayer = playerLayer >= 0 ? interactMask & ~(1 << playerLayer) : interactMask;
    }

    void Update()
    {
        SyncWeaponReferences();
        UpdatePickupHighlight();
        UpdateSwordRotation();
        UpdateWeaponHealthUI();
        UpdateWeaponLowHealthPulse();

        if (WasAttackPressed() && equippedSword != null)
        {
            Vector3 centerScreenWorldPos = GetCenterScreenWorldPosition();
            equippedSword.Attack(centerScreenWorldPos);
        }

        if (!WasInteractPressed()) return;
        if (IsLookingAtWishingWell()) return;

        if (currentSword != null)
        {
            DropSword();
            return;
        }

        TryPickupSwordFromLook();
    }

    void UpdateSwordRotation()
    {
        if (currentSword == null || playerCamera == null) return;

        Vector3 cameraEuler = playerCamera.localEulerAngles;
        currentSword.transform.localRotation = Quaternion.Euler(cameraEuler.x, 0f, 0f);
    }

    public Vector3 GetCenterScreenWorldPosition()
    {
        Transform lookOrigin = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);
        if (lookOrigin == null) return transform.position + transform.forward * interactDistance;
        return lookOrigin.position + lookOrigin.forward * interactDistance;
    }

    public void EquipSword(GameObject swordPrefab) 
    {
        if (swordPrefab == null || weaponSocket == null) return;

        if (currentSword != null)
            DropSword();

        GameObject newSword = Instantiate(swordPrefab, weaponSocket.position, weaponSocket.rotation);
        AttachSword(newSword);
    }

    bool WasInteractPressed()
    {
        bool keyboardPressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        bool gamepadPressed = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
        return keyboardPressed || gamepadPressed;
    }

    bool WasAttackPressed()
    {
        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool gamepadPressed = Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame;
        return mousePressed || gamepadPressed;
    }

    bool IsLookingAtWishingWell()
    {
        Transform lookOrigin = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);
        if (lookOrigin == null) return false;

        if (!Physics.Raycast(lookOrigin.position, lookOrigin.forward, out RaycastHit hit, interactDistance, raycastMaskNoPlayer, QueryTriggerInteraction.Collide))
            return false;

        return hit.collider.GetComponentInParent<WishingWell>() != null;
    }

    void TryPickupSwordFromLook()
    {
        Sword sword = GetLookedAtSword();
        if (sword == null) return;

        AttachSword(sword.gameObject);
    }

    Sword GetLookedAtSword()
    {
        Transform lookOrigin = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : null);
        if (lookOrigin == null || weaponSocket == null) return null;

        if (!Physics.Raycast(lookOrigin.position, lookOrigin.forward, out RaycastHit hit, interactDistance, raycastMaskNoPlayer, QueryTriggerInteraction.Collide))
            return null;

        Sword sword = hit.collider.GetComponentInParent<Sword>();
        if (sword == null) return null;

        if (currentSword != null && sword.gameObject == currentSword)
            return null;

        return sword;
    }

    void UpdatePickupHighlight()
    {
        Sword lookedSword = currentSword == null ? GetLookedAtSword() : null;
        if (lookedSword == highlightedSword) return;

        if (highlightedSword != null)
            highlightedSword.SetHighlighted(false);

        highlightedSword = lookedSword;

        if (highlightedSword != null)
            highlightedSword.SetHighlighted(true);
    }

    void AttachSword(GameObject swordObject)
    {
        if (swordObject == null || weaponSocket == null) return;

        currentSword = swordObject;
        equippedSword = currentSword.GetComponent<Sword>();
        currentSword.transform.SetParent(weaponSocket);
        currentSword.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        if (equippedSword != null)
        {
            equippedSword.SetWeaponManager(this);
            equippedSword.SetHeldPose();
        }

        Rigidbody swordBody = currentSword.GetComponent<Rigidbody>();
        if (swordBody != null)
        {
            swordBody.linearVelocity = Vector3.zero;
            swordBody.angularVelocity = Vector3.zero;
            swordBody.isKinematic = true;
            swordBody.useGravity = false;
        }

        Collider[] swordColliders = currentSword.GetComponentsInChildren<Collider>();
        for (int i = 0; i < swordColliders.Length; i++)
            swordColliders[i].isTrigger = true;

        UpdateWeaponHealthUI();
    }

    public void DropSword()
    {
        if (currentSword == null) return;

        GameObject swordToDrop = currentSword;

        if (equippedSword != null)
            equippedSword.SetWeaponManager(null);

        currentSword = null;
        equippedSword = null;

        swordToDrop.transform.SetParent(null);

        Rigidbody swordBody = swordToDrop.GetComponent<Rigidbody>();
        if (swordBody != null)
        {
            swordBody.isKinematic = false;
            swordBody.useGravity = true;
            swordBody.linearVelocity = Vector3.zero;
            swordBody.angularVelocity = Vector3.zero;
        }

        Collider[] swordColliders = swordToDrop.GetComponentsInChildren<Collider>();
        for (int i = 0; i < swordColliders.Length; i++)
            swordColliders[i].isTrigger = false;

        UpdatePickupHighlight();
        UpdateWeaponHealthUI();
    }

    public void OnEquippedSwordBroken(Sword brokenSword)
    {
        if (brokenSword == null || equippedSword != brokenSword)
            return;

        currentSword = null;
        equippedSword = null;

        UpdatePickupHighlight();
        UpdateWeaponHealthUI();
    }

    void SyncWeaponReferences()
    {
        if (currentSword == null && equippedSword != null)
            equippedSword = null;
        else if (equippedSword == null && currentSword != null)
            currentSword = null;
    }

    void CacheHudElements()
    {
        weaponHealthBar = FindObjectsByType<Slider>(FindObjectsSortMode.None)
            .FirstOrDefault(s => s.name == "WeaponHealthBar");
        if (weaponHealthBar != null)
        {
            weaponHealthLabel = weaponHealthBar.GetComponentInChildren<Text>();
        }
    }

    void UpdateWeaponHealthUI()
    {
        if (weaponHealthBar == null)
        {
            CacheHudElements();
            if (weaponHealthBar == null)
                return;
        }

        if (equippedSword == null)
        {
            weaponHealthBar.gameObject.SetActive(false);
            return;
        }

        weaponHealthBar.gameObject.SetActive(true);
        weaponHealthBar.maxValue = Mathf.Max(1, equippedSword.maxDurability);
        weaponHealthBar.value = Mathf.Clamp(equippedSword.CurrentDurability, 0, equippedSword.maxDurability);
        if (weaponHealthLabel != null)
            weaponHealthLabel.text = $"{equippedSword.CurrentDurability} / {equippedSword.maxDurability}";
    }

    void UpdateWeaponLowHealthPulse()
    {
        if (weaponHealthBar == null)
            return;

        Image fillImage = weaponHealthBar.fillRect?.GetComponent<Image>();
        if (fillImage == null)
            return;

        if (equippedSword == null)
        {
            Color c = fillImage.color;
            c.a = 0.75f;
            fillImage.color = c;
            return;
        }

        if (equippedSword.DurabilityFraction <= lowWeaponHealthThresholdPercent)
        {
            float t = Mathf.PingPong(Time.time * lowWeaponPulseSpeed, 1f);
            Color c = fillImage.color;
            c.a = Mathf.Lerp(0.5f, 1f, t);
            fillImage.color = c;
            return;
        }

        Color normal = fillImage.color;
        normal.a = 0.75f;
        fillImage.color = normal;
    }
}