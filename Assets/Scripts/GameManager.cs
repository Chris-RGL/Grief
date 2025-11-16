using UnityEngine;
using KinematicCharacterController.Walkthrough.NoClipState;

/// <summary>
/// Manages game state, spawn points, and reset functionality
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Player References")]
    public MyCharacterController playerCharacter;
    public Transform playerTransform;

    [Header("Spawn Settings")]
    public Transform playerSpawnPoint;
    public Vector3 playerSpawnOffset = Vector3.zero;

    [Header("Projectile Settings")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public Vector3 projectileSpawnOffset = Vector3.zero;

    [Header("Reset Settings")]
    public float resetDelay = 0.5f;
    public bool resetPlayerVelocity = true;
    public bool resetPlayerRotation = false;

    private GameObject currentProjectile;
    private bool isResetting = false;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Auto-find player if not assigned
        if (playerCharacter == null)
        {
            playerCharacter = FindObjectOfType<MyCharacterController>();
        }

        if (playerTransform == null && playerCharacter != null)
        {
            playerTransform = playerCharacter.transform;
        }

        // Cache PlayerHealth component
        if (playerTransform != null)
        {
            playerHealth = playerTransform.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                Debug.LogWarning("PlayerHealth component not found on player!");
            }
        }

        // Set initial spawn point if not set
        if (playerSpawnPoint == null && playerTransform != null)
        {
            GameObject spawnPointObj = new GameObject("PlayerSpawnPoint");
            playerSpawnPoint = spawnPointObj.transform;
            playerSpawnPoint.position = playerTransform.position + playerSpawnOffset;
            playerSpawnPoint.rotation = playerTransform.rotation;
        }

        // Find initial projectile if it exists in scene
        if (currentProjectile == null)
        {
            AIchase existingProjectile = FindObjectOfType<AIchase>();
            if (existingProjectile != null)
            {
                currentProjectile = existingProjectile.gameObject;

                // Set projectile spawn point if not set
                if (projectileSpawnPoint == null)
                {
                    GameObject projSpawnPointObj = new GameObject("ProjectileSpawnPoint");
                    projectileSpawnPoint = projSpawnPointObj.transform;
                    projectileSpawnPoint.position = currentProjectile.transform.position + projectileSpawnOffset;
                    projectileSpawnPoint.rotation = currentProjectile.transform.rotation;
                }
            }
        }
    }

    /// <summary>
    /// Call this when player dies to reset positions
    /// </summary>
    public void OnPlayerDeath()
    {
        if (isResetting) return;

        StartCoroutine(ResetPositionsCoroutine());
    }

    /// <summary>
    /// Legacy method for backward compatibility - redirects to OnPlayerDeath
    /// </summary>
    public void OnPlayerDamaged()
    {
        // For backward compatibility, this now does nothing
        // Only death triggers reset now
        Debug.Log("Player damaged but not resetting - only death triggers reset");
    }

    private System.Collections.IEnumerator ResetPositionsCoroutine()
    {
        isResetting = true;

        // Optional: Add any visual/audio feedback here
        Debug.Log("Player died! Resetting positions...");

        // Wait for specified delay
        yield return new WaitForSeconds(resetDelay);

        // Reset player position
        ResetPlayer();

        // Reset projectile
        ResetProjectile();

        // IMPORTANT: Restore player health AFTER position reset
        if (playerHealth != null)
        {
            playerHealth.Respawn();
        }
        else
        {
            Debug.LogWarning("PlayerHealth reference lost! Trying to find it again...");
            if (playerTransform != null)
            {
                playerHealth = playerTransform.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.Respawn();
                }
            }
        }

        isResetting = false;
    }

    /// <summary>
    /// Resets the player to spawn position
    /// </summary>
    private void ResetPlayer()
    {
        if (playerTransform == null || playerSpawnPoint == null) return;

        // Move player to spawn point
        playerTransform.position = playerSpawnPoint.position;

        if (resetPlayerRotation)
        {
            playerTransform.rotation = playerSpawnPoint.rotation;
        }

        // Reset velocity if using KinematicCharacterMotor
        if (resetPlayerVelocity && playerCharacter != null && playerCharacter.Motor != null)
        {
            playerCharacter.Motor.SetPosition(playerSpawnPoint.position);
            playerCharacter.Motor.SetRotation(resetPlayerRotation ? playerSpawnPoint.rotation : playerCharacter.Motor.TransientRotation);

            // Force velocity to zero
            playerCharacter.Motor.BaseVelocity = Vector3.zero;
        }

        Debug.Log($"Player reset to: {playerSpawnPoint.position}");
    }

    /// <summary>
    /// Resets the projectile to its spawn position
    /// </summary>
    private void ResetProjectile()
    {
        if (currentProjectile == null)
        {
            // Try to spawn a new projectile if we have a prefab
            if (projectilePrefab != null && projectileSpawnPoint != null)
            {
                SpawnNewProjectile();
            }
            return;
        }

        // Move existing projectile to spawn point
        if (projectileSpawnPoint != null)
        {
            currentProjectile.transform.position = projectileSpawnPoint.position;
            currentProjectile.transform.rotation = projectileSpawnPoint.rotation;

            // Reset projectile velocity
            Rigidbody rb = currentProjectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Reset AIchase internal state
            AIchase aiChase = currentProjectile.GetComponent<AIchase>();
            if (aiChase != null)
            {
                aiChase.ResetState();
            }

            Debug.Log($"Projectile reset to: {projectileSpawnPoint.position}");
        }
    }

    /// <summary>
    /// Spawns a new projectile (useful if projectiles are destroyed on hit)
    /// </summary>
    private void SpawnNewProjectile()
    {
        if (projectilePrefab == null || projectileSpawnPoint == null) return;

        currentProjectile = Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
        Debug.Log("New projectile spawned");
    }

    /// <summary>
    /// Registers a projectile with the game manager
    /// </summary>
    public void RegisterProjectile(GameObject projectile)
    {
        currentProjectile = projectile;
        Debug.Log($"Projectile registered: {projectile.name}");
    }

    /// <summary>
    /// Unregisters a projectile from the game manager (called when projectile is destroyed)
    /// </summary>
    public void UnregisterProjectile(GameObject projectile)
    {
        if (currentProjectile == projectile)
        {
            currentProjectile = null;
            Debug.Log($"Projectile unregistered: {projectile.name}");

            // Optionally spawn a new one after a delay
            if (projectilePrefab != null && projectileSpawnPoint != null)
            {
                StartCoroutine(RespawnProjectileAfterDelay(1f));
            }
        }
    }

    /// <summary>
    /// Respawns a projectile after a delay
    /// </summary>
    private System.Collections.IEnumerator RespawnProjectileAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (currentProjectile == null) // Make sure one wasn't spawned already
        {
            SpawnNewProjectile();
        }
    }

    /// <summary>
    /// Updates the player spawn point (useful for checkpoints)
    /// </summary>
    public void SetPlayerSpawnPoint(Vector3 position, Quaternion rotation)
    {
        if (playerSpawnPoint == null)
        {
            GameObject spawnPointObj = new GameObject("PlayerSpawnPoint");
            playerSpawnPoint = spawnPointObj.transform;
        }

        playerSpawnPoint.position = position;
        playerSpawnPoint.rotation = rotation;
        Debug.Log($"Player spawn point updated to: {position}");
    }

    /// <summary>
    /// Updates the projectile spawn point
    /// </summary>
    public void SetProjectileSpawnPoint(Vector3 position, Quaternion rotation)
    {
        if (projectileSpawnPoint == null)
        {
            GameObject spawnPointObj = new GameObject("ProjectileSpawnPoint");
            projectileSpawnPoint = spawnPointObj.transform;
        }

        projectileSpawnPoint.position = position;
        projectileSpawnPoint.rotation = rotation;
        Debug.Log($"Projectile spawn point updated to: {position}");
    }
}