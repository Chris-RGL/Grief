using UnityEngine;

public class CameraFollowScroller : MonoBehaviour
{
    [Header("References")]
    public Transform[] groundPieces;
    public Transform player;

    [Header("Settings")]
    public float pieceLength = 10f;
    public float bufferDistance = 5f;  // How far behind player before recycling

    void Start()
    {
        // Sort pieces by X position from left to right
        System.Array.Sort(groundPieces, (a, b) =>
            a.position.x.CompareTo(b.position.x));

        Debug.Log("Ground pieces sorted and ready");
    }

    void Update()
    {
        // Check each piece to see if it's behind the player
        foreach (Transform piece in groundPieces)
        {
            // If piece is behind the player by more than buffer distance
            if (piece.position.x < player.position.x - bufferDistance)
            {
                // Find the rightmost (furthest forward) piece
                Transform rightmostPiece = FindRightmostPiece();

                // Position this piece right after the rightmost one
                Vector3 newPos = rightmostPiece.position;
                newPos.x += pieceLength;
                piece.position = newPos;

                Debug.Log($"Moved {piece.name} from behind player to X: {newPos.x}");
            }
        }
    }

    Transform FindRightmostPiece()
    {
        Transform rightmost = groundPieces[0];

        foreach (Transform piece in groundPieces)
        {
            if (piece.position.x > rightmost.position.x)
            {
                rightmost = piece;
            }
        }

        return rightmost;
    }
}