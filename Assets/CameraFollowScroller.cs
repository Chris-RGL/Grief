using UnityEngine;
using System.Collections.Generic;

public class CameraFollowScroller : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform lastStartingPlatform;  // Drag your last manually placed platform here

    [Header("Prefab Settings")]
    public GameObject[] platformPrefabs;  // Your 3 platform prefabs

    [Header("Settings")]
    public float pieceLength = 10f;
    public float bufferDistance = 15f;  // How far behind player before recycling
    public int maxActivePlatforms = 6;  // Maximum platforms to keep active

    private List<GameObject> spawnedPlatforms = new List<GameObject>();
    private float nextSpawnX;
    private int prefabIndex = 0;

    void Start()
    {
        // Set next spawn position to be right after the last starting platform
        nextSpawnX = lastStartingPlatform.position.x + pieceLength;

        Debug.Log($"Starting to spawn platforms after X: {lastStartingPlatform.position.x}");
        Debug.Log($"Next platform will spawn at X: {nextSpawnX}");

        // Spawn a few initial platforms ahead
        for (int i = 0; i < 3; i++)
        {
            SpawnPlatform();
        }
    }

    void Update()
    {
        // Check if player is getting close to the last spawned platform
        if (spawnedPlatforms.Count > 0)
        {
            GameObject lastPlatform = spawnedPlatforms[spawnedPlatforms.Count - 1];

            // If player is getting close to the last platform, spawn a new one
            if (player.position.x > lastPlatform.transform.position.x - (pieceLength * 2))
            {
                SpawnPlatform();
            }
        }

        // Remove platforms that are far behind the player
        for (int i = spawnedPlatforms.Count - 1; i >= 0; i--)
        {
            if (spawnedPlatforms[i].transform.position.x < player.position.x - bufferDistance)
            {
                GameObject platformToRemove = spawnedPlatforms[i];
                spawnedPlatforms.RemoveAt(i);
                Destroy(platformToRemove);

                Debug.Log($"Removed platform that was at X: {platformToRemove.transform.position.x}");
            }
        }

        // Limit total active platforms
        while (spawnedPlatforms.Count > maxActivePlatforms)
        {
            GameObject oldPlatform = spawnedPlatforms[0];
            spawnedPlatforms.RemoveAt(0);
            Destroy(oldPlatform);
        }
    }

    void SpawnPlatform()
    {
        // Get the next prefab (cycles through your prefabs)
        GameObject prefabToUse = platformPrefabs[prefabIndex % platformPrefabs.Length];
        prefabIndex++;

        // Use the last starting platform's Y and Z position
        Vector3 spawnPos = new Vector3(
            nextSpawnX,
            lastStartingPlatform.position.y,
            lastStartingPlatform.position.z
        );

        // Spawn the platform
        GameObject newPlatform = Instantiate(prefabToUse, spawnPos, Quaternion.identity);
        newPlatform.transform.parent = transform;  // Organize under ScrollManager

        // Add to list
        spawnedPlatforms.Add(newPlatform);

        // Update next spawn position
        nextSpawnX += pieceLength;

        Debug.Log($"Spawned {prefabToUse.name} at X: {spawnPos.x}, Y: {spawnPos.y}, Z: {spawnPos.z}");
    }
}