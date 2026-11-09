using System.Collections.Generic;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public float speed = 4f;
    public float maxSpeed = 4f;
    public float turnSpeed = 360f;
    public bool hiveMindEnabled = false;

    public Collider[] hitColliders;
    public RaycastHit hit;

    public float sightRange = 20f;
    public LayerMask enemySightMask = ~0; // Excludes the enemy's own layer
    public float attackRange = 2f;
    public Rigidbody rb;
    public GameObject target;
    public Animator anim;
    public string attackTriggerName = "Attack";
    public int attackDamage = 10;
    public float attackKnockbackForce = 11f;
    public float attackKnockbackUpward = 1f;
    public SkinnedMeshRenderer faceMesh;
    public string angerBlendShapeName = "Angry";
    public int angerBlendShapeIndex = -1;
    public float angryFaceWeight = 100f;
    public float calmFaceWeight = 0f;
    private bool seePlayer = false;
    private bool hasActualSight = false;
    private int resolvedAngerBlendShapeIndex = -1;

    private static readonly HashSet<EnemyAI> hiveMindMembers = new HashSet<EnemyAI>();
    private static GameObject hiveMindTarget;
    private static int hiveMindSightedEnemyCount = 0;

    private bool searchingLastPos = false;
    private Vector3 lastKnownPos;
    private float currentSpeed = 0f;
    public float deceleration = 2f;
    public float attackInterval = 1f;
    private float nextAttackTime = 0f;
    private bool wasInAttackRange = false;

    // Roaming behavior
    private Vector3 roamDirection;
    private float roamTimer = 0f;
    private bool isOnBreak = false;
    private float currentBreakDuration = 0f;
    public float roamSpeed = 2f;
    public float roamDuration = 3f;

    void OnEnable()
    {
        hiveMindMembers.Add(this);
    }

    void OnDisable()
    {
        hiveMindMembers.Remove(this);

        if (hasActualSight)
        {
            hasActualSight = false;
            ClearHiveMindTarget();
        }
    }

    void Start() {
        speed = Mathf.Min(speed, maxSpeed);
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (anim == null) anim = GetComponent<Animator>();
        EnsureRoamDirection();
        AutoAssignFaceMeshIfNeeded();
        ApplyAngerFace(false);
    }

    void LateUpdate()
    {
        // Re-apply the value each frame so Animator/imported clips cannot override it.
        ApplyAngerFace(seePlayer);
    }

    void Update() {
        AiHealth aiHealth = GetComponent<AiHealth>();
        if (aiHealth != null && aiHealth.currentHealth <= 0)
            return;

        ApplyHiveMindTargetIfAvailable();
        RefreshActualSightStatus();

        if (hiveMindTarget == null && !hasActualSight && seePlayer)
        {
            SetSeePlayer(false);
            target = null;
            searchingLastPos = false;
        }

        if (!seePlayer)
        {
            hitColliders = Physics.OverlapSphere(transform.position, sightRange);
            foreach (var HitColliders in hitColliders)
            {
                if (HitColliders.CompareTag("Player"))
                {
                    target = HitColliders.gameObject;
                    SetActualSight(true);
                    searchingLastPos = false;
                }
            }

            if (!seePlayer && searchingLastPos)
            {
                var toTarget = lastKnownPos - transform.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;

                if (dist > 0.05f)
                {
                    currentSpeed = Mathf.Max(0f, currentSpeed - deceleration * Time.deltaTime);
                }
                else
                {
                    currentSpeed = Mathf.Max(0f, currentSpeed - deceleration * Time.deltaTime);
                }

                if (currentSpeed > 0.01f)
                {
                    Vector3 moveDir = dist > 0.05f ? (toTarget / dist) : transform.forward;
                    if (rb != null)
                        rb.linearVelocity = new Vector3(moveDir.x * currentSpeed, rb.linearVelocity.y, moveDir.z * currentSpeed);
                    else
                        transform.position += moveDir * currentSpeed * Time.deltaTime;
                }
                else
                {
                    currentSpeed = 0f;
                    searchingLastPos = false;
                    if (rb != null)
                        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                }
            }
            else if (!seePlayer && !searchingLastPos)
            {
                // Roaming behavior
                roamTimer += Time.deltaTime;

                if (isOnBreak)
                {
                    if (roamTimer >= currentBreakDuration)
                    {
                        // Break over, start roaming
                        isOnBreak = false;
                        roamTimer = 0f;
                        roamDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                    }
                    currentSpeed = 0f;
                }
                else
                {
                    // Roaming
                    if (roamTimer >= roamDuration)
                    {
                        // Start break
                        isOnBreak = true;
                        roamTimer = 0f;
                        currentBreakDuration = Random.Range(5f, 30f);
                    }
                    else
                    {
                        currentSpeed = roamSpeed;
                        EnsureRoamDirection();
                        // Face the roaming direction
                        Quaternion roamRotation = Quaternion.LookRotation(roamDirection, Vector3.up);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, roamRotation, turnSpeed * Time.deltaTime);
                        
                        if (rb != null)
                            rb.linearVelocity = new Vector3(roamDirection.x * roamSpeed, rb.linearVelocity.y, roamDirection.z * roamSpeed);
                        else
                            transform.position += roamDirection * roamSpeed * Time.deltaTime;
                    }
                }
            }
        }
        else
        {
            if (target == null) { SetSeePlayer(false); return; }
            if (hiveMindEnabled && hiveMindTarget != null && target == hiveMindTarget)
            {
                ChaseTarget(target.transform.position);
            }
            else if (Physics.Raycast(transform.position, (target.transform.position - transform.position), out hit, sightRange, enemySightMask))
            {
                if (!hit.collider.CompareTag("Player"))
                {
                    lastKnownPos = target.transform.position;
                    searchingLastPos = true;
                    currentSpeed = Mathf.Min(speed, maxSpeed);
                    SetActualSight(false);
                }
                else
                {
                    ChaseTarget(target.transform.position);
                }
            }
        }

        if (anim != null) anim.SetFloat("AiSpeed", currentSpeed);
    }

    private void Attack()
    {
        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackInterval;

        // Hook attack logic or animation event here.
        if (anim != null && HasAnimatorParameter(anim, attackTriggerName, AnimatorControllerParameterType.Trigger))
            anim.SetTrigger(attackTriggerName);

        if (target == null) return;

        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            playerHealth = target.GetComponentInParent<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.TakeDamage(attackDamage);

        PlayerMovement playerMovement = target.GetComponent<PlayerMovement>();
        if (playerMovement == null)
            playerMovement = target.GetComponentInParent<PlayerMovement>();

        if (playerMovement != null)
        {
            Vector3 knockbackDirection = target.transform.position - transform.position;
            knockbackDirection.y = 0f;

            if (knockbackDirection.sqrMagnitude <= 0.0001f)
                knockbackDirection = target.transform.forward;
            else
                knockbackDirection.Normalize();

            Vector3 knockbackImpulse = knockbackDirection * attackKnockbackForce + Vector3.up * attackKnockbackUpward;
            playerMovement.ApplyKnockback(knockbackImpulse);
        }
    }

    private bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName)) return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == parameterType && parameter.name == parameterName)
                return true;
        }

        return false;
    }

    private void ChaseTarget(Vector3 targetPosition)
    {
        searchingLastPos = false;

        Vector3 heading = targetPosition - transform.position;
        heading.y = 0f;
        float distance = heading.magnitude;

        if (distance <= attackRange)
        {
            if (rb != null)
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

            currentSpeed = 0f;

            if (!wasInAttackRange)
            {
                Attack();
                nextAttackTime = Time.time + attackInterval;
                wasInAttackRange = true;
            }
            else if (Time.time >= nextAttackTime)
            {
                Attack();
                nextAttackTime = Time.time + attackInterval;
            }

            return;
        }

        wasInAttackRange = false;
        if (distance <= 0.0001f)
            return;

        Vector3 direction = heading / distance;
        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

        float useSpeed = Mathf.Min(speed, maxSpeed);
        currentSpeed = useSpeed;
        Vector3 moveVelocity = new Vector3(direction.x * useSpeed, rb != null ? rb.linearVelocity.y : 0f, direction.z * useSpeed);

        if (rb != null)
        {
            rb.linearVelocity = moveVelocity;
        }
        else
        {
            transform.position += new Vector3(direction.x, 0f, direction.z) * useSpeed * Time.deltaTime;
        }
    }

    private void ApplyHiveMindTargetIfAvailable()
    {
        if (!hiveMindEnabled || hiveMindTarget == null)
            return;

        target = hiveMindTarget;
        searchingLastPos = false;

        if (!seePlayer)
            SetSeePlayer(true);
    }

    private void RefreshActualSightStatus()
    {
        if (target == null)
        {
            if (hasActualSight)
                SetActualSight(false);

            return;
        }

        Vector3 toTarget = target.transform.position - transform.position;
        if (toTarget.sqrMagnitude > sightRange * sightRange)
        {
            if (hasActualSight)
                SetActualSight(false);

            return;
        }

        if (Physics.Raycast(transform.position, toTarget, out hit, sightRange, enemySightMask) && hit.collider.CompareTag("Player"))
        {
            SetActualSight(true);
            return;
        }

        if (hasActualSight)
            SetActualSight(false);
    }

    private void BroadcastHiveMindTarget(GameObject playerTarget)
    {
        if (playerTarget == null)
            return;

        hiveMindTarget = playerTarget;
        hiveMindSightedEnemyCount = Mathf.Max(0, hiveMindSightedEnemyCount + 1);
    }

    private void ClearHiveMindTarget()
    {
        hiveMindSightedEnemyCount = Mathf.Max(0, hiveMindSightedEnemyCount - 1);

        if (hiveMindSightedEnemyCount == 0)
            hiveMindTarget = null;
    }

    private void SetActualSight(bool canSeePlayer)
    {
        if (hasActualSight == canSeePlayer)
            return;

        hasActualSight = canSeePlayer;
        SetSeePlayer(canSeePlayer);

        if (canSeePlayer)
            BroadcastHiveMindTarget(target);
        else
            ClearHiveMindTarget();
    }

    private void EnsureRoamDirection()
    {
        if (roamDirection.sqrMagnitude > 0.0001f) return;

        Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        if (randomDirection.sqrMagnitude <= 0.0001f)
            randomDirection = Vector3.forward;

        roamDirection = randomDirection.normalized;
    }

    private void SetSeePlayer(bool canSeePlayer)
    {
        if (seePlayer == canSeePlayer) return;
        seePlayer = canSeePlayer;
        ApplyAngerFace(canSeePlayer);
    }

    private void ApplyAngerFace(bool isAngry)
    {
        if (faceMesh == null || faceMesh.sharedMesh == null) return;

        int blendShapeToUse = ResolveAngerBlendShapeIndex();
        if (blendShapeToUse < 0) return;

        faceMesh.SetBlendShapeWeight(blendShapeToUse, isAngry ? angryFaceWeight : calmFaceWeight);
    }

    private void AutoAssignFaceMeshIfNeeded()
    {
        if (faceMesh != null) return;

        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer smr in renderers)
        {
            if (smr == null || smr.sharedMesh == null || smr.sharedMesh.blendShapeCount == 0)
                continue;

            for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
            {
                if (string.Equals(smr.sharedMesh.GetBlendShapeName(i), angerBlendShapeName, System.StringComparison.OrdinalIgnoreCase))
                {
                    faceMesh = smr;
                    return;
                }
            }
        }

        foreach (SkinnedMeshRenderer smr in renderers)
        {
            if (smr != null && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
            {
                faceMesh = smr;
                return;
            }
        }
    }

    private int ResolveAngerBlendShapeIndex()
    {
        if (faceMesh == null)
        {
            AutoAssignFaceMeshIfNeeded();
            if (faceMesh == null)
                return -1;
        }

        if (resolvedAngerBlendShapeIndex >= 0)
            return resolvedAngerBlendShapeIndex;

        Mesh faceSharedMesh = faceMesh.sharedMesh;
        if (faceSharedMesh == null) return -1;

        if (angerBlendShapeIndex >= 0 && angerBlendShapeIndex < faceSharedMesh.blendShapeCount)
        {
            resolvedAngerBlendShapeIndex = angerBlendShapeIndex;
            return resolvedAngerBlendShapeIndex;
        }

        for (int i = 0; i < faceSharedMesh.blendShapeCount; i++)
        {
            if (string.Equals(faceSharedMesh.GetBlendShapeName(i), angerBlendShapeName, System.StringComparison.OrdinalIgnoreCase))
            {
                resolvedAngerBlendShapeIndex = i;
                return resolvedAngerBlendShapeIndex;
            }
        }

        return -1;
    }
}