using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System; // Keep this for Math.Sign if you prefer, but Unity's Mathf.Sign is more common

public class AIchase : MonoBehaviour
{
    public GameObject Player;

    [Header("Seeking")]
    public float speed;
    public float acceleration = 20f;
    public float rotationSpeed = 10f;
    public float zBias; // This is your "sling" amount. Make it a high value!
    public float yBias;
    public float yBiasCeiling;
    public float zBiasCeiling;

    [Header("Bounce Parameters")]
    public float timeToRebound;
    public float reboundSpeedMultiplier = 1f;

    [Header("Stabilization")]
    public float drag = 0.5f;
    public bool useInterpolation = true;

    // Private variables
    private Rigidbody _rb;
    private Transform _playerTransform;
    private Vector3 _bounceDirection;
    private float _reboundTime;
    private Vector3 _currentVelocity;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Configure Rigidbody for smooth movement
        _rb.interpolation = useInterpolation ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.drag = drag;

        // Freeze rotation to prevent tumbling
        _rb.freezeRotation = true;
    }

    void Start()
    {
        // Assign the player transform from the Player GameObject
        if (Player != null)
        {
            _playerTransform = Player.transform;
        }
        else
        {
            Debug.LogError("Player GameObject not assigned in AIchase script on " + gameObject.name);
        }
    }

    void Update()
    {
        if (_playerTransform == null)
        {
            // Try to find player if not assigned
            GameObject foundPlayer = GameObject.FindGameObjectWithTag("Player");
            if (foundPlayer != null)
            {
                _playerTransform = foundPlayer.transform;
                Debug.Log("Found player automatically");
            }
            return;
        }
    }

    private void FixedUpdate()
    {
        if (_playerTransform == null) return;

        Vector3 modPosition = _playerTransform.position;
        modPosition.y = modPosition.y + 1; // Kept your offset

        Vector3 directionToPlayer = (modPosition - _rb.position).normalized;
        Vector3 targetVelocity; // We will set this in the logic below

        if (_reboundTime > 0f)
        {
            // --- Rebound Logic (from wall collision) ---
            // This logic overrides the chase logic
            float reboundInfluence = _reboundTime / timeToRebound;
            Vector3 reboundVelocity = _bounceDirection * speed * reboundSpeedMultiplier;
            Vector3 chaseVelocity = directionToPlayer * speed; // Need this for the Lerp

            // Blend from pure rebound back to chasing
            targetVelocity = Vector3.Lerp(chaseVelocity, reboundVelocity, reboundInfluence);

            _reboundTime -= Time.fixedDeltaTime;
        }
        else
        {
            // --- Standard Chase & "Overshoot" Logic ---

            // Start with the basic chase velocity
            targetVelocity = directionToPlayer * speed;

            // 1. Get the enemy's current velocity
            Vector3 currentVelocity = _rb.velocity;

            // 2. Check if we are moving "past" the player
            // We do this by comparing the direction to the player with our current movement direction
            // If the dot product is negative, we are moving generally away from them.
            float dot = Vector3.Dot(currentVelocity.normalized, directionToPlayer);

            // 3. Check the result (using a small threshold for stability)
            // We also check that we have *some* velocity, otherwise dot product is unreliable
            if (dot < 0.1f && currentVelocity.sqrMagnitude > 1.0f)
            {
                // We are moving AWAY from the player (a "miss").
                // Now we apply the "sling" bias.

                // Get the sign of our *current* Z velocity (our momentum)
                float zMomentumSign = Mathf.Sign(currentVelocity.z);

                // If we are perfectly aligned (z-velocity is 0),
                // we need to decide which way to sling.
                // Let's sling in the direction *opposite* the player's Z.
                if (zMomentumSign == 0)
                {
                    zMomentumSign = 1; // Default to +Z if still zero
                }

                // Apply the bias.
                // This will *add* (zBias * +/-1) to the target Z.
                // If target Z is negative (go back) but momentum is positive (go forward),
                // this will add a positive bias, *fighting* the turn-around.
                // This creates the "sling" effect.
                if (Math.Abs(_rb.position.z) < zBiasCeiling)
                {
                    targetVelocity.z = targetVelocity.z + (zBias * zMomentumSign);
                }

                if (_rb.position.y < yBiasCeiling)
                {
                    targetVelocity.y = targetVelocity.y + yBias;
                }
            }
        }

        // --- Apply Velocity & Rotation (applies in all cases) ---

        // Smooth acceleration toward target velocity
        _currentVelocity = Vector3.MoveTowards(_rb.velocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        _rb.velocity = _currentVelocity;

        Debug.DrawRay(_rb.position, _currentVelocity, Color.red);
        Debug.DrawRay(_rb.position, directionToPlayer * 2f, Color.blue);

        // Smooth rotation toward movement direction
        if (_currentVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_currentVelocity);
            Quaternion smoothRotation = Quaternion.Slerp(_rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            _rb.MoveRotation(smoothRotation);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount > 0)
        {
            // Only trigger rebound if we are not *already* rebounding
            if (_reboundTime <= 0f)
            {
                ContactPoint contact = collision.GetContact(0);

                // Get direction of movement
                Vector3 incomingVelocity = _rb.velocity.normalized;

                // Reflect the incoming direction off the surface normal
                _bounceDirection = Vector3.Reflect(incomingVelocity, contact.normal).normalized;

                // Start rebound timer
                _reboundTime = timeToRebound;

                Debug.DrawRay(contact.point, contact.normal * 2f, Color.green, 1f);
                Debug.DrawRay(contact.point, _bounceDirection * 2f, Color.yellow, 1f);
            }
        }
    }
}