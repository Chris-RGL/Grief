using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System;

public class AIchase : MonoBehaviour
{
    [Header("Seeking")]
    public float speed;
    public float acceleration = 20f;
    public float rotationSpeed = 10f;
    public float zBias;
    public float yBias;

    [Header("Bounce Parameters")]
    public float timeToRebound;
    public float reboundSpeedMultiplier = 1f;

    [Header("Stabilization")]
    public float drag = 0.5f;
    public bool useInterpolation = true;

    [Header("Damage Settings")]
    public int damageAmount = 1;
    public bool destroyOnHit = false;
    public LayerMask playerLayer = -1; // Set to player layer for better performance

    [Header("Click Detection")]
    public bool enableClickHighlight = true;
    public Color hoverColor = new Color(1f, 0.5f, 0.5f, 1f); // Light red when hovering

    // Private variables
    private Rigidbody _rb;
    private Transform _playerTransform;
    private Vector3 _bounceDirection;
    private float _reboundTime;
    private Vector3 _currentVelocity;
    private GameObject _player;
    private PlayerHealth _playerHealth;
    private Renderer _renderer;
    private Color _originalColor;
    private bool _isHovering = false;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();

        // Configure Rigidbody for smooth movement
        _rb.interpolation = useInterpolation ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.drag = drag;

        // Freeze rotation to prevent tumbling
        _rb.freezeRotation = true;

        // Get renderer for highlight effect
        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.material != null)
        {
            _originalColor = _renderer.material.color;
        }

        // Register with GameManager if it exists
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterProjectile(gameObject);
        }
    }

    void Start()
    {
        StartCoroutine(FindPlayerDelayed());
    }

    IEnumerator FindPlayerDelayed()
    {
        yield return null;

        // Find and cache player transform by tag
        GameObject playerObject = GameObject.FindGameObjectWithTag("Character");
        if (playerObject != null)
        {
            _player = playerObject;
            _playerTransform = _player.transform;
            _playerHealth = _player.GetComponent<PlayerHealth>();

            if (_playerHealth == null)
            {
                Debug.LogWarning("Player found but PlayerHealth component is missing! Add PlayerHealth to player.", this);
            }
        }
        else
        {
            Debug.LogError("No GameObject with the 'Character' tag found (after delay)!", this);
            this.enabled = false;
            yield break;
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
                _playerHealth = foundPlayer.GetComponent<PlayerHealth>();
                Debug.Log("Found player automatically");
            }
            return;
        }
    }

    private void FixedUpdate()
    {
        if (_playerTransform == null) return;

        Vector3 modPosition = _playerTransform.position;
        modPosition.y = modPosition.y + 1;

        Vector3 directionToPlayer = (modPosition - _rb.position).normalized;
        Vector3 targetVelocity;

        if (_reboundTime > 0f)
        {
            // --- Rebound Logic (from wall collision) ---
            float reboundInfluence = _reboundTime / timeToRebound;
            Vector3 reboundVelocity = _bounceDirection * speed * reboundSpeedMultiplier;
            Vector3 chaseVelocity = directionToPlayer * speed;

            targetVelocity = Vector3.Lerp(chaseVelocity, reboundVelocity, reboundInfluence);

            _reboundTime -= Time.fixedDeltaTime;
        }
        else
        {
            // --- Standard Chase & "Overshoot" Logic ---
            targetVelocity = directionToPlayer * speed;

            Vector3 currentVelocity = _rb.velocity;
            float dot = Vector3.Dot(currentVelocity.normalized, directionToPlayer);

            if (dot < 0.1f && currentVelocity.sqrMagnitude > 1.0f)
            {
                float zMomentumSign = Mathf.Sign(currentVelocity.z);

                if (zMomentumSign == 0)
                {
                    zMomentumSign = 1;
                }

                targetVelocity.z = targetVelocity.z + (zBias * zMomentumSign);
                targetVelocity.y = targetVelocity.y + yBias;
            }
        }

        // --- Apply Velocity & Rotation ---
        _currentVelocity = Vector3.MoveTowards(_rb.velocity, targetVelocity, acceleration * Time.fixedDeltaTime);
        _rb.velocity = _currentVelocity;

        Debug.DrawRay(_rb.position, _currentVelocity, Color.red);
        Debug.DrawRay(_rb.position, directionToPlayer * 2f, Color.blue);

        if (_currentVelocity.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_currentVelocity);
            Quaternion smoothRotation = Quaternion.Slerp(_rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
            _rb.MoveRotation(smoothRotation);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Check if we hit the player
        if (collision.gameObject.CompareTag("Character") || collision.gameObject.CompareTag("Player"))
        {
            HandlePlayerHit(collision.gameObject);
            return;
        }

        // Check using layer mask if set
        if (playerLayer != -1 && ((1 << collision.gameObject.layer) & playerLayer) != 0)
        {
            HandlePlayerHit(collision.gameObject);
            return;
        }

        // Handle wall bounce
        if (collision.contactCount > 0)
        {
            if (_reboundTime <= 0f)
            {
                ContactPoint contact = collision.GetContact(0);
                Vector3 incomingVelocity = _rb.velocity.normalized;
                _bounceDirection = contact.normal;
                _reboundTime = timeToRebound;

                Debug.DrawRay(contact.point, contact.normal * 2f, Color.green, 1f);
                Debug.DrawRay(contact.point, _bounceDirection * 2f, Color.yellow, 1f);
            }
        }
    }

    /// <summary>
    /// Called when mouse enters the projectile's collider
    /// </summary>
    private void OnMouseEnter()
    {
        if (enableClickHighlight && _renderer != null && _renderer.material != null && Cursor.visible)
        {
            _isHovering = true;
            _renderer.material.color = hoverColor;
        }
    }

    /// <summary>
    /// Called when mouse exits the projectile's collider
    /// </summary>
    private void OnMouseExit()
    {
        if (enableClickHighlight && _renderer != null && _renderer.material != null)
        {
            _isHovering = false;
            _renderer.material.color = _originalColor;
        }
    }

    /// <summary>
    /// Called when the projectile is clicked - THIS IS THE FIX FOR YOUR BUG
    /// </summary>
    private void OnMouseDown()
    {
        if (enableClickHighlight && Cursor.visible)
        {
            Debug.Log("Projectile clicked and destroyed!");

            // Unregister from GameManager before destroying
            if (GameManager.Instance != null)
            {
                GameManager.Instance.UnregisterProjectile(gameObject);
            }

            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Handles collision with the player
    /// </summary>
    private void HandlePlayerHit(GameObject player)
    {
        Debug.Log("Projectile hit player!");

        // Deal damage to player
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damageAmount);
        }
        else
        {
            // Fallback: directly call GameManager if PlayerHealth is missing
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerDamaged();
            }
        }

        // Optionally destroy this projectile
        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Public method to reset projectile state (can be called by GameManager)
    /// </summary>
    public void ResetState()
    {
        _reboundTime = 0f;
        _currentVelocity = Vector3.zero;

        if (_rb != null)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // Reset color if it was changed
        if (_renderer != null && _renderer.material != null)
        {
            _renderer.material.color = _originalColor;
        }
        _isHovering = false;
    }

    private void OnDestroy()
    {
        // Reset material color when destroyed to prevent material leak
        if (_renderer != null && _renderer.material != null && !_isHovering)
        {
            _renderer.material.color = _originalColor;
        }
    }
}