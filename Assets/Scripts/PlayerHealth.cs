using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages player health and damage
/// Attach this to your player character
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 3;
    public int currentHealth = 3;
    public bool invulnerable = false;

    [Header("Invincibility Settings")]
    public bool useInvincibilityFrames = true;
    public float invincibilityDuration = 2f;
    private float invincibilityTimer = 0f;

    [Header("Visual Feedback (Optional)")]
    public bool flashOnDamage = true;
    public Renderer[] renderersToFlash;
    public float flashDuration = 0.1f;
    public int flashCount = 3;

    [Header("Events")]
    public UnityEvent onDamaged;
    public UnityEvent onDeath;
    public UnityEvent onHealthChanged;

    private bool isDead = false;

    private void Start()
    {
        currentHealth = maxHealth;

        // Auto-find renderers if not assigned
        if (renderersToFlash == null || renderersToFlash.Length == 0)
        {
            renderersToFlash = GetComponentsInChildren<Renderer>();
        }
    }

    private void Update()
    {
        // Handle invincibility timer
        if (invincibilityTimer > 0f)
        {
            invincibilityTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Call this when the player takes damage
    /// </summary>
    public void TakeDamage(int damage = 1)
    {
        // Check if player can take damage
        if (isDead || invulnerable || (useInvincibilityFrames && invincibilityTimer > 0f))
        {
            Debug.Log("Player is invulnerable or dead, damage ignored");
            return;
        }

        // Apply damage
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);

        Debug.Log($"Player took {damage} damage. Health: {currentHealth}/{maxHealth}");

        // Invoke events
        onDamaged?.Invoke();
        onHealthChanged?.Invoke();

        // Visual feedback
        if (flashOnDamage)
        {
            StartCoroutine(FlashCoroutine());
        }

        // Set invincibility frames
        if (useInvincibilityFrames)
        {
            invincibilityTimer = invincibilityDuration;
        }

        // Check for death
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Trigger position reset via GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerDamaged();
            }
        }
    }

    /// <summary>
    /// Handles player death
    /// </summary>
    private void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log("Player died!");

        // Invoke death event
        onDeath?.Invoke();

        // Trigger position reset via GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerDamaged();
        }

        // Optional: Respawn after delay
        StartCoroutine(RespawnCoroutine());
    }

    /// <summary>
    /// Respawns the player after death
    /// </summary>
    private System.Collections.IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(1f);

        // Reset health
        currentHealth = maxHealth;
        isDead = false;
        invincibilityTimer = invincibilityDuration;

        onHealthChanged?.Invoke();
        Debug.Log("Player respawned");
    }

    /// <summary>
    /// Flash effect for damage feedback
    /// </summary>
    private System.Collections.IEnumerator FlashCoroutine()
    {
        if (renderersToFlash == null || renderersToFlash.Length == 0)
            yield break;

        for (int i = 0; i < flashCount; i++)
        {
            // Disable renderers
            foreach (Renderer rend in renderersToFlash)
            {
                if (rend != null)
                    rend.enabled = false;
            }

            yield return new WaitForSeconds(flashDuration);

            // Enable renderers
            foreach (Renderer rend in renderersToFlash)
            {
                if (rend != null)
                    rend.enabled = true;
            }

            yield return new WaitForSeconds(flashDuration);
        }

        // Ensure renderers are enabled at the end
        foreach (Renderer rend in renderersToFlash)
        {
            if (rend != null)
                rend.enabled = true;
        }
    }

    /// <summary>
    /// Heals the player
    /// </summary>
    public void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
        onHealthChanged?.Invoke();
        Debug.Log($"Player healed {amount}. Health: {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// Resets health to max
    /// </summary>
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isDead = false;
        onHealthChanged?.Invoke();
    }

    /// <summary>
    /// Returns whether the player is currently invulnerable
    /// </summary>
    public bool IsInvulnerable()
    {
        return invulnerable || (useInvincibilityFrames && invincibilityTimer > 0f) || isDead;
    }

    /// <summary>
    /// Returns whether the player is dead
    /// </summary>
    public bool IsDead()
    {
        return isDead;
    }
}