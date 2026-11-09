using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 8f;
    public float runMultiplier = 1.5f;
    private Rigidbody playerRigidbody;
    public float jumpForce = 5f;
    public float groundCheckDistance = 1.1f;
    public float gamepadLookSensitivity = 100f;
    public float gamepadDeadzone = 0.15f;
    public float knockbackDecay = 6.5f;
    public float knockbackControlLockDuration = 0.16f;
    public float wishLookCheckDistance = 4f;
    public LayerMask wishLookMask;
    private LayerMask raycastMaskNoPlayer;

    [Header("Look Settings")]
    public Transform playerCamera;
    public float mouseSensitivity = 100f;
    private float xRotation = 0f;
    private float bodyYaw;

    private Vector2 movementInput;
    private bool isRunning;
    private bool jumpQueued;
    private Vector3 knockbackVelocity;
    private float knockbackControlLockTimer;

    void Start()
    {
        playerRigidbody = GetComponent<Rigidbody>();
        bodyYaw = transform.eulerAngles.y;

        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        // Lock the cursor to the center of the screen and hide it
        Cursor.lockState = CursorLockMode.Locked;

        // IMPORTANT: Rigidbody setup to prevent tipping over
        if (playerRigidbody != null)
        {
            // Keep the body upright while still allowing yaw rotation.
            playerRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            playerRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
        
        // Build layer mask that excludes the player layer
        if (wishLookMask == 0) wishLookMask = ~0;
        int playerLayer = LayerMask.NameToLayer("Player");
        raycastMaskNoPlayer = playerLayer >= 0 ? wishLookMask & ~(1 << playerLayer) : wishLookMask;
    }

    void Update()
    {
        EnsureCameraReference();
        ReadMovementInput();
        ReadJumpInput();

        if (IsXRActive())
            HandleVRTurn();
        else
            HandleLook();
    }

    void EnsureCameraReference()
    {
        if (playerCamera != null) return;
        if (Camera.main != null)
            playerCamera = Camera.main.transform;
    }

    void FixedUpdate()
    {
        if (knockbackControlLockTimer > 0f)
            knockbackControlLockTimer -= Time.fixedDeltaTime;

        ApplyBodyYaw();
        HandleMovement();
        HandleJump();
    }

    void ReadMovementInput()
    {
        Vector2 moveInput = Vector2.zero;

        if (Gamepad.current != null)
        {
            Vector2 gp = Gamepad.current.leftStick.ReadValue();
            if (gp.magnitude > gamepadDeadzone) moveInput = gp;
        }

        if (moveInput.magnitude <= gamepadDeadzone)
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
                if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
                if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
                if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
            }
        }

        bool running = false;
        if (Keyboard.current != null)
        {
            running = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
        if (!running && Gamepad.current != null)
        {
            running = Gamepad.current.leftStickButton.isPressed;
        }

        movementInput = Vector2.ClampMagnitude(moveInput, 1f);
        isRunning = running;
    }

    void HandleMovement()
    {
        if (playerRigidbody == null) return;

        float currentSpeed = speed * (isRunning ? runMultiplier : 1f);

        // Move relative to the camera/headset yaw so forward matches where the player looks.
        Vector2 effectiveInput = knockbackControlLockTimer > 0f ? Vector2.zero : movementInput;
        Transform moveReference = playerCamera != null ? playerCamera : transform;
        Vector3 referenceForward = moveReference.forward;
        Vector3 referenceRight = moveReference.right;

        referenceForward.y = 0f;
        referenceRight.y = 0f;

        // If the player is looking almost straight up/down, fallback to body forward for stable locomotion.
        if (referenceForward.sqrMagnitude < 0.0001f)
            referenceForward = transform.forward;
        if (referenceRight.sqrMagnitude < 0.0001f)
            referenceRight = transform.right;

        referenceForward.Normalize();
        referenceRight.Normalize();

        Vector3 moveDir = referenceRight * effectiveInput.x + referenceForward * effectiveInput.y;
        if (moveDir.sqrMagnitude > 1f)
            moveDir.Normalize();

        // Apply velocity instead of MovePosition to respect physics collisions
        // Note: Use 'velocity' instead of 'linearVelocity' if using Unity 2022 or older
        knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackDecay * Time.fixedDeltaTime);

        Vector3 targetVelocity = moveDir * currentSpeed + new Vector3(knockbackVelocity.x, 0f, knockbackVelocity.z);
        targetVelocity.y = playerRigidbody.linearVelocity.y; 

        playerRigidbody.linearVelocity = targetVelocity;
    }

    public void ApplyKnockback(Vector3 impulse)
    {
        if (playerRigidbody == null) return;

        knockbackVelocity = new Vector3(impulse.x, 0f, impulse.z);
        knockbackControlLockTimer = knockbackControlLockDuration;

        if (impulse.y > 0f)
        {
            Vector3 vel = playerRigidbody.linearVelocity;
            if (vel.y < 0f) vel.y = 0f;
            playerRigidbody.linearVelocity = vel;
            playerRigidbody.AddForce(Vector3.up * impulse.y, ForceMode.VelocityChange);
        }
    }

    void ReadJumpInput()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) jumpQueued = true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame && !IsLookingAtWishingWell()) jumpQueued = true;
    }

    void HandleJump()
    {
        if (playerRigidbody == null) return;

        if (jumpQueued && IsGrounded())
        {
            // Reset Y velocity before jumping for consistent height
            Vector3 vel = playerRigidbody.linearVelocity;
            vel.y = 0;
            playerRigidbody.linearVelocity = vel;

            playerRigidbody.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        }
        jumpQueued = false;
    }

    bool IsGrounded()
    {
        // Shoots a ray down from the center of the player to see if we are touching the floor
        return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance);
    }

    bool IsLookingAtWishingWell()
    {
        Transform lookOrigin = playerCamera != null ? playerCamera : (Camera.main != null ? Camera.main.transform : transform);

        if (!Physics.Raycast(lookOrigin.position, lookOrigin.forward, out RaycastHit hit, wishLookCheckDistance, raycastMaskNoPlayer, QueryTriggerInteraction.Collide))
            return false;

        return hit.collider.GetComponentInParent<WishingWell>() != null;
    }

    bool IsXRActive()
    {
        return XRSettings.enabled && XRSettings.isDeviceActive;
    }

    void AddYawInput(float deltaYaw)
    {
        if (Mathf.Approximately(deltaYaw, 0f))
            return;

        bodyYaw = Mathf.Repeat(bodyYaw + deltaYaw, 360f);
    }

    void ApplyBodyYaw()
    {
        Quaternion yawRotation = Quaternion.Euler(0f, bodyYaw, 0f);

        if (playerRigidbody != null)
        {
            // Prevent terrain/collision torque from randomly rotating the player.
            playerRigidbody.MoveRotation(yawRotation);
            playerRigidbody.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.rotation = yawRotation;
        }
    }

    void HandleVRTurn()
    {
        if (Gamepad.current == null)
            return;

        Vector2 rs = Gamepad.current.rightStick.ReadValue();
        float turnInput = Mathf.Abs(rs.x) > gamepadDeadzone ? rs.x : 0f;
        if (Mathf.Approximately(turnInput, 0f))
            return;

        float yawDelta = turnInput * gamepadLookSensitivity * Time.deltaTime;
        AddYawInput(yawDelta);
    }

    void HandleLook()
    {
        float lookX = 0f;
        float lookY = 0f;

        if (Gamepad.current != null)
        {
            Vector2 rs = Gamepad.current.rightStick.ReadValue();
            if (rs.magnitude > gamepadDeadzone)
            {
                lookX = rs.x * gamepadLookSensitivity * Time.deltaTime;
                lookY = rs.y * gamepadLookSensitivity * Time.deltaTime;
            }
        }

        if (lookX == 0f && lookY == 0f && Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            lookX = mouseDelta.x * mouseSensitivity * Time.deltaTime;
            lookY = mouseDelta.y * mouseSensitivity * Time.deltaTime;
        }

        AddYawInput(lookX);

        xRotation -= lookY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}