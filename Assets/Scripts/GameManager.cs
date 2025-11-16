using UnityEngine;
using KinematicCharacterController.Walkthrough.NoClipState;
using System.Collections.Generic;

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

    [Header("Enemy Settings")]
    public GameObject projectilePrefab;
    public Transform projectileSpawnPoint;
    public Vector3 projectileSpawnOffset = Vector3.zero;
    [Tooltip("Number of enemies to spawn and maintain at all times")]
    public int maxEnemies = 3;
    [Tooltip("Delay before respawning a destroyed enemy")]
    public float enemyRespawnDelay = 1f;

    [Header("Reset Settings")]
    public float resetDelay = 0.5f;
    public bool resetPlayerVelocity = true;
    public bool resetPlayerRotation = false;

    private List<GameObject> activeEnemies = new List<GameObject>();
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

        // Find existing enemies in scene or spawn new ones
        InitializeEnemies();
    }

    /// <summary>
    /// Finds existing enemies in the scene and spawns additional ones if needed
    /// </summary>
    private void InitializeEnemies()
    {
        // Find all existing enemies in the scene
        AIchase[] existingEnemies = FindObjectsOfType<AIchase>();

        foreach (AIchase enemy in existingEnemies)
        {
            if (!activeEnemies.Contains(enemy.gameObject))
            {
                RegisterEnemy(enemy.gameObject);
            }
        }

        // Set projectile spawn point based on first enemy if not set
        if (projectileSpawnPoint == null && activeEnemies.Count > 0)
        {
            GameObject projSpawnPointObj = new GameObject("EnemySpawnPoint");
            projectileSpawnPoint = projSpawnPointObj.transform;
            projectileSpawnPoint.position = activeEnemies[0].transform.position + projectileSpawnOffset;
            projectileSpawnPoint.rotation = activeEnemies[0].transform.rotation;
        }

        // Spawn additional enemies if we don't have enough
        int enemiesToSpawn = maxEnemies - activeEnemies.Count;
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnNewEnemy();
        }

        Debug.Log($"Enemy initialization complete. Active enemies: {activeEnemies.Count}/{maxEnemies}");
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

        // Reset all enemies
        ResetAllEnemies();

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
    /// Resets all enemies to their spawn positions
    /// </summary>
    private void ResetAllEnemies()
    {
        // Clean up null references
        activeEnemies.RemoveAll(enemy => enemy == null);

        // Reset all active enemies
        foreach (GameObject enemy in activeEnemies)
        {
            ResetEnemy(enemy);
        }

        // Spawn new enemies if we're below max count
        int enemiesToSpawn = maxEnemies - activeEnemies.Count;
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            SpawnNewEnemy();
        }

        Debug.Log($"All enemies reset. Active count: {activeEnemies.Count}/{maxEnemies}");
    }

    /// <summary>
    /// Resets a single enemy to spawn position
    /// </summary>
    private void ResetEnemy(GameObject enemy)
    {
        if (enemy == null || projectileSpawnPoint == null) return;

        enemy.transform.position = projectileSpawnPoint.position;
        enemy.transform.rotation = projectileSpawnPoint.rotation;

        // Reset enemy velocity
        Rigidbody rb = enemy.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Reset AIchase internal state
        AIchase aiChase = enemy.GetComponent<AIchase>();
        if (aiChase != null)
        {
            aiChase.ResetState();
        }

        Debug.Log($"Enemy reset to: {projectileSpawnPoint.position}");
    }

    /// <summary>
    /// Spawns a new enemy at the spawn point
    /// </summary>
    private void SpawnNewEnemy()
    {
        if (projectilePrefab == null || projectileSpawnPoint == null)
        {
            Debug.LogWarning("Cannot spawn enemy: prefab or spawn point not set!");
            return;
        }

        GameObject newEnemy = Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
        RegisterEnemy(newEnemy);
        Debug.Log($"New enemy spawned. Total active: {activeEnemies.Count}/{maxEnemies}");
    }

    /// <summary>
    /// Registers an enemy with the game manager
    /// </summary>
    public void RegisterEnemy(GameObject enemy)
    {
        if (!activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
            Debug.Log($"Enemy registered: {enemy.name}. Total active: {activeEnemies.Count}/{maxEnemies}");
        }
    }

    /// <summary>
    /// Unregisters an enemy from the game manager (called when enemy is destroyed)
    /// </summary>
    public void UnregisterEnemy(GameObject enemy)
    {
        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
            Debug.Log($"Enemy unregistered: {enemy.name}. Remaining: {activeEnemies.Count}/{maxEnemies}");

            // Automatically respawn a new enemy to maintain max count
            if (projectilePrefab != null && projectileSpawnPoint != null)
            {
                StartCoroutine(RespawnEnemyAfterDelay(enemyRespawnDelay));
            }
        }
    }

    /// <summary>
    /// Respawns an enemy after a delay (only if below max count)
    /// </summary>
    private System.Collections.IEnumerator RespawnEnemyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Only spawn if we're below max enemies
        if (activeEnemies.Count < maxEnemies)
        {
            SpawnNewEnemy();
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
    /// Updates the enemy spawn point
    /// </summary>
    public void SetProjectileSpawnPoint(Vector3 position, Quaternion rotation)
    {
        if (projectileSpawnPoint == null)
        {
            GameObject spawnPointObj = new GameObject("EnemySpawnPoint");
            projectileSpawnPoint = spawnPointObj.transform;
        }

        projectileSpawnPoint.position = position;
        projectileSpawnPoint.rotation = rotation;
        Debug.Log($"Enemy spawn point updated to: {position}");
    }

    // Legacy methods for backward compatibility

    /// <summary>
    /// Legacy method - use RegisterEnemy instead
    /// </summary>
    public void RegisterProjectile(GameObject projectile)
    {
        RegisterEnemy(projectile);
    }

    /// <summary>
    /// Legacy method - use UnregisterEnemy instead
    /// </summary>
    public void UnregisterProjectile(GameObject projectile)
    {
        UnregisterEnemy(projectile);
    }
}