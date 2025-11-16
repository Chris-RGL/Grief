using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AIchase : MonoBehaviour
{
    public GameObject Player;

    [Header("Seeking")]
    public float speed;
    public float acceleration = 20f;
    public float rotationSpeed = 10f;

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

        Vector3 directionToPlayer = (_playerTransform.position - _rb.position).normalized;
        Vector3 targetVelocity;

        if (_reboundTime > 0f)
        {
            // Apply bounce with reduced influence over time
            float reboundInfluence = _reboundTime / timeToRebound;
            Vector3 reboundVelocity = _bounceDirection * speed * reboundSpeedMultiplier;
            Vector3 chaseVelocity = directionToPlayer * speed;

            targetVelocity = Vector3.Lerp(chaseVelocity, reboundVelocity, reboundInfluence);

            _reboundTime -= Time.fixedDeltaTime;
        }
        else
        {
            targetVelocity = directionToPlayer * speed;
        }

        // Smooth acceleration toward target velocity instead of instant change
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