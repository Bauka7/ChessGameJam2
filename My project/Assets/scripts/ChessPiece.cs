using UnityEngine;

// Перечисление всех типов фигур
public enum ChessPieceType
{
    None = 0,
    Pawn = 1,
    Rook = 2,
    Knight = 3,
    Bishop = 4,
    Queen = 5,
    King = 6
}

public class ChessPiece : MonoBehaviour
{
    public ChessPieceType type;
    public int currentX;
    public int currentY;
    public bool isWhite;
    public bool hasMoved = false; // Пригодится для рокировки

    public void PositionPiece(int x, int y, Vector3 offset, float tileSize)
    {
        currentX = x;
        currentY = y;
        transform.position = new Vector3(x * tileSize + 2.04f, 1f, y * tileSize + 2f) + offset;
    }
}