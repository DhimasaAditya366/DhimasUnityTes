using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;  // Add this for Action

public class Ball : MonoBehaviour
{
    public event Action OnBallDestroyed;
    private Rigidbody rb;
    public bool isHeld = false;
    private Transform holdPoint;

    [Header("Physics Settings")]
    public float throwForce = 10f;
    public float maxVelocity = 20f;
    public float dragInAir = 0.5f;
    public float dragOnGround = 1f;

    [Header("Ground Check")]
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    private TrailRenderer trail;
    public bool IsHeld => isHeld;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Configure the rigidbody
        rb.mass = 1f;
        rb.drag = dragInAir;
        rb.angularDrag = 0.1f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (TryGetComponent<SphereCollider>(out SphereCollider collider))
        {
            collider.material = Resources.Load<PhysicMaterial>("BallPhysics");
        }
    }

    private void OnDestroy()
    {
        OnBallDestroyed?.Invoke();  // Trigger the event when ball is destroyed
    }

    private void FixedUpdate()
    {
        if (!isHeld)
        {
            // Limit maximum velocity
            if (rb.velocity.magnitude > maxVelocity)
            {
                rb.velocity = rb.velocity.normalized * maxVelocity;
            }

            // Apply different drag based on whether the ball is on the ground
            bool isGrounded = Physics.CheckSphere(transform.position - Vector3.up * 0.5f, groundCheckRadius, groundLayer);
            rb.drag = isGrounded ? dragOnGround : dragInAir;
        }
    }

    private void Update()
    {
        // If the ball is held, update its position to match the hold point exactly
        if (isHeld && holdPoint != null)
        {
            transform.position = holdPoint.position;
            transform.rotation = holdPoint.rotation;
        }
    }

    public void PickUp(Transform holder)
    {
        isHeld = true;
        holdPoint = holder;
        rb.isKinematic = true;
        rb.detectCollisions = false; // Disable collisions while held

        transform.SetParent(holder);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Throw(Vector3 direction)
    {
        isHeld = false;
        transform.SetParent(null);
        rb.isKinematic = false;
        rb.detectCollisions = true; // Re-enable collisions
        rb.velocity = Vector3.zero;
        rb.AddForce(direction.normalized * throwForce, ForceMode.Impulse);
    }

    public void EnableTrail()
    {
        if (trail == null)
        {
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.5f; // Trail duration
            trail.startWidth = 0.2f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
        }
        trail.enabled = true;
    }

    public void DisableTrail()
    {
        if (trail != null)
            trail.enabled = false;
    }
}
