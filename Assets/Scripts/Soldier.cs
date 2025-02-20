using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HighlightPlus;

public class Soldier : MonoBehaviour
{
    [Header("References")]
    private Rigidbody rb;
    private TeamElement teamElement;
    private Transform targetFence;
    private Transform targetGate;
    private Ball nearestBall;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float stoppingDistance = 0.1f;
    public float currentSpeed = 0f;
    public float autoMoveSpeed = 1.5f;
    public float carryingBallSpeed = 0.75f;

    [Header("Attacker Settings")]
    public bool isActive = true;
    public bool hasBall = false;
    public bool isMovingToFence = false;
    public bool isMovingToGate = false;
    public bool isChasingBall = false;
    private Ball heldBall;
    [SerializeField] private bool isPaused = false;

    [Header("Defender Settings")]
    public float detectionRadius = 5f;
    public float chaseSpeed = 7f;
    public float defenderInactiveTime = 3f;    // Inactive time for defender
    public float attackerInactiveTime = 2f;    // Inactive time for attacker caught
    public bool isDefending = false;
    public bool isInactive = false;
    [SerializeField] private Material detectionZoneMaterial;
    [SerializeField] private GameObject detectionZone;
    private Soldier nearestPlayerWithBall;
    private Soldier nearestAllyPlayer;

    [Header("Ball Passing Settings")]
    [Tooltip("How quickly to pass the ball")]
    public float passForce = 5f;
    [Tooltip("How far to look for allies to pass to")]
    public float maxPassDistance = 15f;
    [Tooltip("Minimum distance required to pass")]
    public float minPassDistance = 3f;
    [Tooltip("How close the ball needs to be to be caught")]
    public float catchRadius = 1.5f;
    [Tooltip("Maximum time to wait for pass completion")]
    public float maxPassTime = 0.5f;
    [Tooltip("How often to check for successful catch")]
    public float catchCheckInterval = 0.05f;

    [Header("Defender Return Behavior")]
    private Vector3 spawnPosition;
    private bool isReturningToPosition = false;
    public float returnSpeed = 5f;
    public float returnPositionThreshold = 0.1f; // How close the defender needs to get to consider it "returned"

    [Header("Ground Check")]
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.1f;
    private float groundedYPosition;

    [Header("Ground Stabilization")]
    public float groundStabilizationRate = 5f;
    public float maxGroundDistance = 0.5f;
    private bool shouldStabilizeGround = true;

    [Header("Interaction")]
    public Transform ballHoldPoint;
    public float pickupRange = 2f;
    public float ballDetectionRange = 5f;
    public LayerMask fenceLayer;
    public LayerMask gateLayer;

    [Header("Ball and Holder References")]
    private static GameObject currentBallInPlay;    // Reference to the current ball that's being held
    private static GameObject currentBallHolder;    // Reference to the soldier holding the ball

    [Header("Animation")]
    private Animator animator;
    private static readonly int SpawnHash = Animator.StringToHash("Spawn");
    private static readonly int RunHash = Animator.StringToHash("Run");
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int DieHash = Animator.StringToHash("Die");
    private static readonly int CaughtHash = Animator.StringToHash("Caught");
    private static readonly int CatchAttackerHash = Animator.StringToHash("CatchAttacker");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int IsStandbyHash = Animator.StringToHash("IsStandby");
    private static readonly int IsChasingHash = Animator.StringToHash("IsChasing");

    private void Start()
    {
        // Get required components
        rb = GetComponent<Rigidbody>();
        teamElement = GetComponent<TeamElement>();
        spawnPosition = transform.position;

        if (rb == null || teamElement == null)
        {
            Debug.LogError("Required components missing on " + gameObject.name);
            return;
        }

        // Configure physics and collisions
        ConfigureCollisions();

        // Subscribe to events
        teamElement.OnActivated += HandleActivation;
        teamElement.OnRoleChangedWithRole += HandleRoleChange;

        if (GetComponentInParent<TeamManager>() != null)
        {
            UpdateTargetStructures();
        }
        else
        {
            Debug.LogError("TeamManager not found in parent hierarchy for " + gameObject.name);
        }

        UpdateGroundedPosition();

        // Get the Animator component
        animator = transform.Find("Soldier Mesh")?.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator component missing on Soldier Mesh child of " + gameObject.name);
            return;
        }

        // Play spawn animation based on role
        PlaySpawnAnimation();
    }

    private void PlaySpawnAnimation()
    {
        if (animator != null)
        {
            animator.SetTrigger(SpawnHash);

            // Set initial state based on role
            if (teamElement.CurrentRole == TeamRole.Defender)
            {
                animator.SetBool(IsStandbyHash, true);
            }
        }
    }

    private void UpdateGroundedPosition()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, 2f, groundLayer))
        {
            groundedYPosition = hit.point.y;
        }
    }

    private void CreateDetectionZone()
    {
        if (detectionZone != null)
        {
            Destroy(detectionZone);
        }

        detectionZone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        detectionZone.transform.parent = transform;
        detectionZone.transform.localPosition = Vector3.zero;
        detectionZone.transform.localScale = new Vector3(detectionRadius * 2, 0.1f, detectionRadius * 2);

        // Remove the collider, we don't need it
        Destroy(detectionZone.GetComponent<Collider>());

        // Set material
        MeshRenderer meshRenderer = detectionZone.GetComponent<MeshRenderer>();
        if (meshRenderer != null && detectionZoneMaterial != null)
        {
            meshRenderer.material = new Material(detectionZoneMaterial);
            Color color = meshRenderer.material.color;
            color.a = 0.3f;
            meshRenderer.material.color = color;
        }
    }

    private void Update()
    {

        if (rb == null || teamElement == null) return;
        if (isPaused) return;

        // Ground position stabilization
        if (shouldStabilizeGround && !isInactive)
        {
            StabilizeGroundPosition();
        }

        // Rest of the existing Update logic...
        if (isInactive)
        {
            Vector3 currentPos = transform.position;
            currentPos.y = groundedYPosition;
            transform.position = currentPos;
            return;
        }

        // Rest of the Update logic...
        if (teamElement.currentRole == TeamRole.Defender)
        {
            if (isReturningToPosition)
            {
                ReturnToSpawnPosition();
            }
            else
            {
                HandleDefenderBehavior();
            }
        }
        else if (teamElement.currentRole == TeamRole.Attacker)
        {
            // Original attacker behavior
            if (!hasBall)
            {
                Ball[] balls = FindObjectsOfType<Ball>();
                foreach (Ball ball in balls)
                {
                    if (ball != null && !ball.isHeld)
                    {
                        float distance = Vector3.Distance(transform.position, ball.transform.position);
                        if (distance < ballDetectionRange)
                        {
                            nearestBall = ball;
                            isChasingBall = true;
                            isMovingToFence = false;
                            break;
                        }
                    }
                }
            }

            // Then handle movement based on state
            if (hasBall && isMovingToGate && targetGate != null)
            {
                MoveTowardGate();
            }
            else if (isChasingBall && nearestBall != null)
            {
                FindAndChaseBall();
            }
            else if (isMovingToFence && targetFence != null)
            {
                MoveTowardFence();
            }
            else
            {
                HandleMovement();
            }
        }

        // Update animation states
        UpdateAnimationStates();
    }

    private void UpdateAnimationStates()
    {
        if (animator == null) return;

        if (teamElement.CurrentRole == TeamRole.Attacker)
        {
            // Set running animation when moving
            bool isMoving = isChasingBall || isMovingToGate || isMovingToFence;
            animator.SetBool(IsMovingHash, isMoving);
        }
        else if (teamElement.CurrentRole == TeamRole.Defender)
        {
            // Update defender animation states
            animator.SetBool(IsStandbyHash, !nearestPlayerWithBall);
            animator.SetBool(IsChasingHash, nearestPlayerWithBall != null);
        }
    }

    private void StabilizeGroundPosition()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, Vector3.down, out hit, maxGroundDistance + 1f, groundLayer))
        {
            float targetY = hit.point.y;
            Vector3 currentPos = transform.position;

            // Smoothly adjust height
            if (Mathf.Abs(currentPos.y - targetY) > 0.01f)
            {
                currentPos.y = Mathf.Lerp(currentPos.y, targetY, Time.deltaTime * groundStabilizationRate);
                transform.position = currentPos;
            }
        }
    }

    private void HandleDefenderBehavior()
    {
        if (!isDefending)
        {
            if (detectionZone == null && detectionZoneMaterial != null)
            {
                CreateDetectionZone();
            }
            isDefending = true;
        }

        // Look for player with ball in detection radius
        Soldier[] allSoldiers = FindObjectsOfType<Soldier>();
        float nearestDistance = float.MaxValue;
        nearestPlayerWithBall = null;

        foreach (Soldier soldier in allSoldiers)
        {
            if (soldier != null && soldier.teamElement != null &&
                soldier.teamElement.isPlayerTeam != teamElement.isPlayerTeam &&
                soldier.hasBall)
            {
                float distance = Vector3.Distance(transform.position, soldier.transform.position);
                if (distance < detectionRadius && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestPlayerWithBall = soldier;
                }
            }
        }

        if (nearestPlayerWithBall != null)
        {
            // Turn off detection zone when starting to chase
            if (detectionZone != null)
            {
                detectionZone.SetActive(false);
            }
            ChasePlayer(nearestPlayerWithBall);
        }
        else
        {
            // Turn on detection zone when in standby
            if (detectionZone != null)
            {
                detectionZone.SetActive(true);
            }
        }
    }

    private void ChasePlayer(Soldier target)
    {
        // Ensure detection zone is off during chase
        if (detectionZone != null)
        {
            detectionZone.SetActive(false);
        }

        Vector3 directionToPlayer = (target.transform.position - transform.position).normalized;
        directionToPlayer.y = 0f;

        rb.MovePosition(transform.position + directionToPlayer * chaseSpeed * Time.deltaTime);

        if (directionToPlayer != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(directionToPlayer, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }

        float currentDistance = Vector3.Distance(transform.position, target.transform.position);
        if (currentDistance <= 1f)
        {
            CatchPlayer(target);
        }
    }


    private void ReturnToSpawnPosition()
    {
        Vector3 directionToSpawn = (spawnPosition - transform.position).normalized;
        directionToSpawn.y = 0f;

        rb.MovePosition(transform.position + directionToSpawn * returnSpeed * Time.deltaTime);

        if (directionToSpawn != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(directionToSpawn, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }

        float distanceToSpawn = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                                               new Vector3(spawnPosition.x, 0, spawnPosition.z));

        if (distanceToSpawn <= returnPositionThreshold)
        {
            isReturningToPosition = false;
            transform.rotation = Quaternion.Euler(0, 0, 0);

            // Ensure the detection zone is created and activated
            CreateAndActivateDetectionZone();

            isDefending = true;
        }
    }

    private void CreateAndActivateDetectionZone()
    {
        if (detectionZone == null)
        {
            CreateDetectionZone();
        }

        if (detectionZone != null)
        {
            detectionZone.SetActive(true);

            // Ensure proper positioning and scale
            detectionZone.transform.localPosition = Vector3.zero;
            detectionZone.transform.localScale = new Vector3(detectionRadius * 2, 0.1f, detectionRadius * 2);

            // Refresh material
            MeshRenderer meshRenderer = detectionZone.GetComponent<MeshRenderer>();
            if (meshRenderer != null && detectionZoneMaterial != null)
            {
                meshRenderer.material = new Material(detectionZoneMaterial);
                Color color = meshRenderer.material.color;
                color.a = 0.3f;
                meshRenderer.material.color = color;
            }
        }
    }

    private void ConfigureCollisions()
    {
        // Set the layer based on role instead of team
        gameObject.layer = teamElement.currentRole == TeamRole.Attacker ?
            LayerMask.NameToLayer("Attacker") :
            LayerMask.NameToLayer("Defender");

        // Configure Rigidbody for stable movement
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Only freeze rotations we want to prevent, keep Y rotation free
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // Adjust mass and drag for better stability
            rb.mass = 1f;
            rb.drag = 1f;
        }

        // Configure all colliders on the soldier
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            // Create a physics material with no friction
            PhysicMaterial noFriction = new PhysicMaterial("NoFriction");
            noFriction.dynamicFriction = 0f;
            noFriction.staticFriction = 0f;
            noFriction.bounciness = 0f;
            noFriction.frictionCombine = PhysicMaterialCombine.Minimum;
            noFriction.bounceCombine = PhysicMaterialCombine.Minimum;

            col.material = noFriction;
        }
    }

    private void CatchPlayer(Soldier caughtPlayer)
    {
        shouldStabilizeGround = false;  // Temporarily disable ground stabilization

        // Update grounded positions
        UpdateGroundedPosition();
        caughtPlayer.UpdateGroundedPosition();

        // Freeze both soldiers
        FreezeRigidbody(rb);
        FreezeRigidbody(caughtPlayer.rb);

        // Play catch animations
        if (animator != null)
        {
            animator.SetTrigger(CatchAttackerHash);
        }
        if (caughtPlayer.animator != null)
        {
            caughtPlayer.animator.SetTrigger(CaughtHash);
        }

        // Ensure both soldiers are at ground level
        Vector3 pos = transform.position;
        pos.y = groundedYPosition;
        transform.position = pos;

        Vector3 caughtPos = caughtPlayer.transform.position;
        caughtPos.y = caughtPlayer.groundedYPosition;
        caughtPlayer.transform.position = caughtPos;

        // Rest of catch logic...
        StartCoroutine(BecomeInactiveForTime(defenderInactiveTime));
        caughtPlayer.StartCoroutine(caughtPlayer.BecomeInactiveForTime(attackerInactiveTime));

        if (teamElement != null)
        {
            teamElement.SetTemporaryInactive(defenderInactiveTime);
        }
        if (caughtPlayer.teamElement != null)
        {
            caughtPlayer.teamElement.SetTemporaryInactive(attackerInactiveTime);
        }

        if (caughtPlayer.hasBall && caughtPlayer.heldBall != null)
        {
            HandleBallThrow(caughtPlayer);
        }
    }

    private void FreezeRigidbody(Rigidbody targetRb)
    {
        if (targetRb != null)
        {
            targetRb.velocity = Vector3.zero;
            targetRb.angularVelocity = Vector3.zero;
            targetRb.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    // In Soldier.cs, modify the HandleBallThrow method:

    private void HandleBallThrow(Soldier caughtPlayer)
    {
        Soldier[] allSoldiers = FindObjectsOfType<Soldier>();
        float nearestDistance = float.MaxValue;
        nearestAllyPlayer = null;

        foreach (Soldier soldier in allSoldiers)
        {
            if (soldier.teamElement.isPlayerTeam == caughtPlayer.teamElement.isPlayerTeam &&
                soldier != caughtPlayer && !soldier.isInactive)
            {
                float distance = Vector3.Distance(caughtPlayer.transform.position, soldier.transform.position);
                if (distance >= minPassDistance && distance <= maxPassDistance && distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestAllyPlayer = soldier;
                }
            }
        }

        if (nearestAllyPlayer != null)
        {
            Vector3 throwDirection = (nearestAllyPlayer.transform.position - caughtPlayer.transform.position).normalized;
            Ball thrownBall = caughtPlayer.heldBall;

            thrownBall.throwForce = passForce;
            caughtPlayer.ThrowBall(throwDirection);

            StartCoroutine(DirectPassCheck(thrownBall, nearestAllyPlayer));
        }
        else
        {
            // No nearby allies to pass to - check if caught player was an attacker
            if (caughtPlayer.teamElement.CurrentRole == TeamRole.Attacker)
            {
                // End match with the defending team winning
                GameManager gameManager = GameManager.Instance;
                if (gameManager != null)
                {
                    // If player team caught the enemy, player wins
                    // If enemy team caught the player, enemy wins
                    bool isPlayerTeamDefending = !caughtPlayer.teamElement.isPlayerTeam;
                    gameManager.HandleDefenderCatch(isPlayerTeamDefending);
                }
            }
            else
            {
                // For non-attacker catches, just throw the ball forward as before
                caughtPlayer.ThrowBall(caughtPlayer.transform.forward);
            }
        }
    }

    private IEnumerator DirectPassCheck(Ball ball, Soldier targetSoldier)
    {
        float remainingTime = maxPassTime;

        while (remainingTime > 0 && ball != null && !ball.isHeld)
        {
            float distanceToBall = Vector3.Distance(ball.transform.position, targetSoldier.transform.position);

            if (distanceToBall < catchRadius)
            {
                targetSoldier.PickupBall(ball);
                targetSoldier.isMovingToGate = true;
                targetSoldier.isChasingBall = false;
                break;
            }

            remainingTime -= catchCheckInterval;
            yield return new WaitForSeconds(catchCheckInterval);
        }
    }


    private IEnumerator CheckBallCatch(Ball ball, Soldier targetSoldier)
    {
        float checkDuration = 2f;
        float checkInterval = 0.1f;
        float catchRadius = 2f; // Increased catch radius for better catching

        while (checkDuration > 0 && ball != null && !ball.isHeld)
        {
            if (Vector3.Distance(ball.transform.position, targetSoldier.transform.position) < catchRadius)
            {
                targetSoldier.PickupBall(ball);
                targetSoldier.isMovingToGate = true;
                targetSoldier.isChasingBall = false;
                break;
            }

            checkDuration -= checkInterval;
            yield return new WaitForSeconds(checkInterval);
        }
    }

    private IEnumerator BecomeInactiveForTime(float time)
    {
        isInactive = true;
        shouldStabilizeGround = false;  // Disable stabilization during inactive state

        if (detectionZone != null)
        {
            detectionZone.SetActive(false);
        }

        UpdateGroundedPosition();

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;

            Vector3 pos = transform.position;
            pos.y = groundedYPosition;
            transform.position = pos;
        }

        // Reset animation states
        if (animator != null)
        {
            animator.SetBool(IsMovingHash, false);
            animator.SetBool(IsStandbyHash, false);
            animator.SetBool(IsChasingHash, false);
        }

        yield return new WaitForSeconds(time);

        isInactive = false;
        shouldStabilizeGround = true;  // Re-enable ground stabilization

        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (teamElement != null && teamElement.currentRole == TeamRole.Defender)
        {
            isReturningToPosition = true;
        }

        if (teamElement != null)
        {
            teamElement.UpdateColor();
        }

        // Resume animations based on role
        if (animator != null)
        {
            if (teamElement.CurrentRole == TeamRole.Defender)
            {
                animator.SetBool(IsStandbyHash, true);
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up detection zone if it exists
        if (detectionZone != null)
        {
            MeshRenderer meshRenderer = detectionZone.GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.material != null)
            {
                Destroy(meshRenderer.material);
            }
            Destroy(detectionZone);
        }

        // Clear references if this soldier was the ball holder
        if (currentBallHolder == this.gameObject)
        {
            currentBallInPlay = null;
            currentBallHolder = null;
        }
    }

    private void HandleRoleChange(TeamRole newRole)
    {
        // Update the layer when the role changes
        gameObject.layer = newRole == TeamRole.Attacker ?
            LayerMask.NameToLayer("Attacker") :
            LayerMask.NameToLayer("Defender");

        // Rest of your existing HandleRoleChange code...
        UpdateTargetStructures();
    }

    private void UpdateTargetStructures()
    {
        if (teamElement == null)
        {
            Debug.LogError("TeamElement is null in UpdateTargetStructures!");
            return;
        }

        // Get the TeamManager component from the parent
        TeamManager teamManager = GetComponentInParent<TeamManager>();
        if (teamManager == null)
        {
            Debug.LogError("TeamManager not found in parent hierarchy!");
            return;
        }

        // Find appropriate gate and fence based on current role
        targetGate = TeamStructure.FindTargetStructure(
            teamElement.CurrentRole,
            teamManager.isPlayerTeam,
            TeamStructure.StructureType.Gate
        );

        targetFence = TeamStructure.FindTargetStructure(
            teamElement.CurrentRole,
            teamManager.isPlayerTeam,
            TeamStructure.StructureType.Fence
        );

        if (targetGate == null)
        {
            Debug.LogWarning("No target gate found for " + (teamManager.isPlayerTeam ? "player" : "enemy") + " team!");
        }

        if (targetFence == null)
        {
            Debug.LogWarning("No target fence found for " + (teamManager.isPlayerTeam ? "player" : "enemy") + " team!");
        }
    }

    private void HandleActivation()
    {
        UpdateTargetStructures();

        if (teamElement.CurrentRole == TeamRole.Attacker)
        {
            // Look for nearest ball using FindObjectsOfType instead of a separate method
            Ball[] balls = FindObjectsOfType<Ball>();
            foreach (Ball ball in balls)
            {
                if (ball != null && !ball.isHeld)
                {
                    float distance = Vector3.Distance(transform.position, ball.transform.position);
                    if (distance < ballDetectionRange)
                    {
                        nearestBall = ball;
                        isChasingBall = true;
                        isMovingToFence = false;
                        break;
                    }
                }
            }

            if (!hasBall && nearestBall == null)
            {
                isMovingToFence = true;
            }
        }
    }

    private void FindNearestFence()
    {
        GameObject[] fences = GameObject.FindGameObjectsWithTag("Fence");
        float nearestDistance = float.MaxValue;

        foreach (GameObject fence in fences)
        {
            float distance = Vector3.Distance(transform.position, fence.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                targetFence = fence.transform;
            }
        }
    }

    private void FindNearestGate()
    {
        GameObject[] gates = GameObject.FindGameObjectsWithTag("Gate");
        float nearestDistance = float.MaxValue;

        foreach (GameObject gate in gates)
        {
            float distance = Vector3.Distance(transform.position, gate.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                targetGate = gate.transform;
            }
        }
    }

    private void FindAndChaseBall()
    {
        if (nearestBall == null || !isChasingBall)
        {
            isChasingBall = false;
            return;
        }

        // Check if within pickup range
        float distanceToBall = Vector3.Distance(transform.position, nearestBall.transform.position);
        if (distanceToBall <= pickupRange)
        {
            PickupBall(nearestBall);  // This will now trigger OnBallPickup
            return;
        }

        // Chase the ball
        Vector3 directionToBall = (nearestBall.transform.position - transform.position).normalized;
        directionToBall.y = 0f;

        // Move towards ball
        rb.MovePosition(transform.position + directionToBall * autoMoveSpeed * Time.deltaTime);

        // Rotate towards ball
        if (directionToBall != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(directionToBall, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void MoveTowardGate()
    {
        if (targetGate == null) return;

        Vector3 direction = (targetGate.position - transform.position).normalized;
        direction.y = 0f;

        // Debug log to check if this method is being called
        Debug.Log("Moving toward gate: " + direction);

        // Move towards gate at carrying speed
        rb.MovePosition(transform.position + direction * carryingBallSpeed * Time.deltaTime);

        // Rotate towards movement direction
        if (direction != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void MoveTowardFence()
    {
        Vector3 direction = (targetFence.position - transform.position).normalized;
        direction.y = 0f;

        rb.MovePosition(transform.position + direction * autoMoveSpeed * Time.deltaTime);

        if (direction != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void HandleMovement()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");
        Vector3 movement = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        if (movement != Vector3.zero)
        {
            rb.MovePosition(transform.position + movement * moveSpeed * Time.deltaTime);
            Quaternion toRotation = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Fence") && isMovingToFence)
        {
            StartCoroutine(PlayDeathAnimation());
        }
        else if (collision.gameObject.CompareTag("Gate") && isMovingToGate && hasBall)
        {
            if (heldBall != null)
            {
                Destroy(heldBall.gameObject);
            }
            Destroy(gameObject);

            GameManager gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.HandleBallReachedGate(teamElement.isPlayerTeam);
            }
        }
    }

    private System.Collections.IEnumerator PlayDeathAnimation()
    {
        if (animator != null)
        {
            // Play death animation
            animator.SetTrigger(DieHash);

            // Wait for animation to complete
            yield return new WaitForSeconds(4.0f); // Adjust time based on animation length

            // Destroy the object after animation
            Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void PickupBall(Ball ball)
    {
        hasBall = true;
        heldBall = ball;
        ball.PickUp(ballHoldPoint);
        isMovingToGate = true;  // Add this line
        isChasingBall = false;  // Add this to ensure we stop chasing

        OnBallPickup();
    }

    private void OnBallPickup()
    {
        if (teamElement.CurrentRole == TeamRole.Attacker)
        {
            // Store references to the current ball and holder
            currentBallInPlay = heldBall.gameObject;
            currentBallHolder = this.gameObject;

            Debug.Log($"Ball {currentBallInPlay.name} picked up by {(teamElement.isPlayerTeam ? "Player" : "Enemy")} attacker: {currentBallHolder.name}");

            // You can access these references anywhere using:
            //Soldier.currentBallInPlay
            GameObject currentBall = Soldier.GetCurrentBallInPlay();
            GameObject currentHolder = Soldier.GetCurrentBallHolder();

            //modify something after grab a ball
            //Debug.Log($"Current ball: {currentBall.name} is held by: {currentHolder.name}");
            currentBall.GetComponent<HighlightEffect>().SetHighlighted(false);
            currentHolder.GetComponent<HighlightEffect>().SetHighlighted(true);

        }
    }

    public static GameObject GetCurrentBallInPlay()
    {
        return currentBallInPlay;
    }

    public static GameObject GetCurrentBallHolder()
    {
        return currentBallHolder;
    }

    private void ThrowBall(Vector3 direction)
    {
        if (heldBall != null)
        {
            hasBall = false;
            heldBall.Throw(direction);

            GameObject currentHolder = Soldier.GetCurrentBallHolder();
            currentHolder.GetComponent<HighlightEffect>().SetHighlighted(false);

            // Clear references when ball is thrown
            if (currentBallInPlay == heldBall.gameObject)
            {
                currentBallInPlay = null;
                currentBallHolder = null;
            }

            heldBall = null;

            // Reset states after throwing
            isMovingToGate = false;
            isChasingBall = false;
            isMovingToFence = true;
        }
    }

    public void Deactivate()
    {
        isActive = false;
        if (hasBall)
        {
            ThrowBall(transform.forward);
        }
    }

    public void Activate()
    {
        isActive = true;
    }

    public void SetPauseState(bool pauseState)
    {
        isPaused = pauseState;
        if (rb != null)
        {
            rb.velocity = Vector3.zero;  // Stop movement when paused
        }
    }

    private void OnDrawGizmos()
    {
        DrawAllGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawAllGizmos();
    }

    private void DrawAllGizmos()
    {
        // Ball detection range (Green)
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, ballDetectionRange);

        // Pickup range (Yellow)
        Gizmos.color = new Color(1, 1, 0, 0.3f);
        Gizmos.DrawWireSphere(transform.position, pickupRange);

        // Always draw passing ranges, even if no ball (for better visualization in editor)
        // Minimum pass distance (Red)
        Gizmos.color = new Color(1, 0, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, minPassDistance);

        // Maximum pass distance (Blue)
        Gizmos.color = new Color(0, 0, 1, 0.2f);
        Gizmos.DrawWireSphere(transform.position, maxPassDistance);

        // Draw catch radius preview (Cyan)
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Gizmos.DrawWireSphere(transform.position, catchRadius);

        // Get team element if not already cached
        if (teamElement == null)
        {
            teamElement = GetComponent<TeamElement>();
        }

        // Defender detection radius (if applicable)
        if (teamElement != null && teamElement.currentRole == TeamRole.Defender)
        {
            // Detection zone outline (Red)
            Gizmos.color = new Color(1, 0, 0, 0.4f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            // Filled detection zone
            Gizmos.color = new Color(1, 0, 0, 0.1f);
            Gizmos.DrawSphere(transform.position, detectionRadius);
        }

        // Only draw these lines in play mode since they depend on runtime state
        if (Application.isPlaying)
        {
            // Draw line to ball being chased
            if (isChasingBall && nearestBall != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, nearestBall.transform.position);
            }

            // Draw line to gate when moving with ball
            if (isMovingToGate && targetGate != null && hasBall)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(transform.position, targetGate.position);
            }

            // Draw line to nearest ally for passing
            if (hasBall && nearestAllyPlayer != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, nearestAllyPlayer.transform.position);

                // Draw catch radius around nearest ally
                Gizmos.color = new Color(0, 1, 1, 0.3f);
                Gizmos.DrawWireSphere(nearestAllyPlayer.transform.position, catchRadius);
            }
        }

        // Optional: Draw a small sphere at the soldier's position for better visibility
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}