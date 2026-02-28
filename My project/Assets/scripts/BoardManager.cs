using UnityEngine;
using System.Collections.Generic;

public class BoardManager : MonoBehaviour
{
    [Header("Настройки доски")]
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    public float tileSize = 1.0f;
    public Vector3 boardOffset = Vector3.zero;

    [Header("Префабы и данные")]
    public GameObject[] chessPiecePrefabs;
    public ChessPiece[,] chessPieces;

    [Header("Камера")]
    public CameraController cameraController;   // ← СЮДА, внутрь класса

    private ChessPiece activePiece = null;
    private int hoverX = -1;
    private int hoverY = -1;

    // Очередность хода
    private bool isWhiteTurn = true;

    // Флаг конца игры
    private bool gameOver = false;

    private void Start()
    {
        SpawnAllPieces();
    }

    private void Update()
    {
        // Если игра окончена — ничего не делаем
        if (gameOver) return;

        if (!Camera.main) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane boardPlane = new Plane(Vector3.up, Vector3.zero);
        float distance;

        if (boardPlane.Raycast(ray, out distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            int x = Mathf.FloorToInt((hitPoint.x - boardOffset.x) / tileSize);
            int y = Mathf.FloorToInt((hitPoint.z - boardOffset.z) / tileSize);

            if (x >= 0 && x < TILE_COUNT_X && y >= 0 && y < TILE_COUNT_Y)
            {
                hoverX = x;
                hoverY = y;
            }
            else { hoverX = -1; hoverY = -1; }
        }
        else { hoverX = -1; hoverY = -1; }

        if (Input.GetMouseButtonDown(0) && hoverX != -1 && hoverY != -1)
        {
            if (activePiece == null)
                SelectPiece(hoverX, hoverY);
            else
                MovePiece(hoverX, hoverY);
        }
    }

    // =============================================
    // ВЫБОР ФИГУРЫ
    // =============================================
    private void SelectPiece(int x, int y)
    {
        if (chessPieces[x, y] == null) return;

        if (chessPieces[x, y].isWhite == isWhiteTurn)
        {
            activePiece = chessPieces[x, y];
            Debug.Log($"Выбрана фигура: {activePiece.type} на ({x},{y})");
        }
        else
        {
            Debug.Log("Сейчас не ваш ход!");
        }
    }

    // =============================================
    // ВЫПОЛНЕНИЕ ХОДА
    // =============================================
    private void MovePiece(int x, int y)
    {
        // Если кликнули на свою же фигуру — переключаем выбор
        if (chessPieces[x, y] != null && chessPieces[x, y].isWhite == activePiece.isWhite)
        {
            SelectPiece(x, y);
            return;
        }

        // Проверяем базовую легальность хода
        if (!IsValidMove(activePiece, x, y))
        {
            Debug.Log("Ход недопустим по правилам!");
            activePiece = null;
            return;
        }

        // Проверяем: не подставляем ли мы своего короля под шах?
        if (DoesMoveCauseCheck(activePiece, x, y, activePiece.isWhite))
        {
            Debug.Log("Нельзя! Ваш король окажется под шахом!");
            activePiece = null;
            return;
        }

        // Запоминаем взятую фигуру (если есть)
        ChessPiece capturedPiece = chessPieces[x, y];

        // Выполняем ход
        chessPieces[activePiece.currentX, activePiece.currentY] = null;
        chessPieces[x, y] = activePiece;
        activePiece.hasMoved = true;
        activePiece.PositionPiece(x, y, boardOffset, tileSize);

        // Уничтожаем взятую фигуру
        if (capturedPiece != null)
        {
            Debug.Log($"Взята фигура: {capturedPiece.type}");
            Destroy(capturedPiece.gameObject);
        }

        activePiece = null;

        // Передаём ход сопернику
        isWhiteTurn = !isWhiteTurn;

        if (cameraController != null)
    {
        if (isWhiteTurn)
        cameraController.SwitchToWhite();
      else
        cameraController.SwitchToBlack();
    }

        // Проверяем состояние игры после хода
        CheckGameState();
    }

    // =============================================
    // ПРОВЕРКА СОСТОЯНИЯ ИГРЫ (ШАХ / МАТ / ПАТ)
    // =============================================
    private void CheckGameState()
    {
        bool currentPlayerIsWhite = isWhiteTurn;
        bool kingInCheck = IsKingInCheck(currentPlayerIsWhite);
        bool hasLegalMoves = HasAnyLegalMove(currentPlayerIsWhite);

        if (kingInCheck && !hasLegalMoves)
        {
            // МАТ — игра окончена
            gameOver = true;
            string winner = currentPlayerIsWhite ? "Чёрные" : "Белые";
            Debug.Log($"ШАХ И МАТ! Победили {winner}!");
        }
        else if (!kingInCheck && !hasLegalMoves)
        {
            // ПАТ — ничья
            gameOver = true;
            Debug.Log("ПАТ! Ничья!");
        }
        else if (kingInCheck)
        {
            // ШАХ — игра продолжается, но предупреждаем
            Debug.Log($"ШАХ! {(currentPlayerIsWhite ? "Белый" : "Чёрный")} король под ударом!");
        }
        else
        {
            Debug.Log("Ход перешел к: " + (isWhiteTurn ? "Белым" : "Черным"));
        }
    }

    // =============================================
    // НАХОДИТСЯ ЛИ КОРОЛЬ ПОД ШАХОМ?
    // =============================================
    private bool IsKingInCheck(bool whiteKing)
    {
        // Находим позицию короля нужного цвета
        int kingX = -1, kingY = -1;
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                ChessPiece p = chessPieces[x, y];
                if (p != null && p.type == ChessPieceType.King && p.isWhite == whiteKing)
                {
                    kingX = x;
                    kingY = y;
                    break;
                }
            }
            if (kingX != -1) break;
        }

        if (kingX == -1) return false; // Король не найден

        // Атакует ли эту клетку хоть одна вражеская фигура?
        return IsSquareUnderAttack(kingX, kingY, whiteKing);
    }

    // =============================================
    // АТАКОВАНА ЛИ КЛЕТКА ВРАЖЕСКИМИ ФИГУРАМИ?
    // =============================================
    private bool IsSquareUnderAttack(int targetX, int targetY, bool defendingIsWhite)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                ChessPiece p = chessPieces[x, y];
                // Смотрим только на фигуры противника
                if (p == null || p.isWhite == defendingIsWhite) continue;
                // Может ли эта фигура атаковать нужную клетку?
                if (IsValidMove(p, targetX, targetY)) return true;
            }
        }
        return false;
    }

    // =============================================
    // ОСТАВЛЯЕТ ЛИ ХОД СВОЕГО КОРОЛЯ ПОД ШАХОМ?
    // Симулируем ход, проверяем шах, откатываем назад
    // =============================================
    private bool DoesMoveCauseCheck(ChessPiece piece, int toX, int toY, bool isWhitePiece)
    {
        int fromX = piece.currentX;
        int fromY = piece.currentY;
        ChessPiece targetBackup = chessPieces[toX, toY];

        // Симулируем ход
        chessPieces[fromX, fromY] = null;
        chessPieces[toX, toY] = piece;
        piece.currentX = toX;
        piece.currentY = toY;

        bool inCheck = IsKingInCheck(isWhitePiece);

        // Откатываем ход
        piece.currentX = fromX;
        piece.currentY = fromY;
        chessPieces[fromX, fromY] = piece;
        chessPieces[toX, toY] = targetBackup;

        return inCheck;
    }

    // =============================================
    // ЕСТЬ ЛИ ХОТЬ ОДИН ЛЕГАЛЬНЫЙ ХОД?
    // =============================================
    private bool HasAnyLegalMove(bool forWhite)
    {
        for (int fx = 0; fx < TILE_COUNT_X; fx++)
        {
            for (int fy = 0; fy < TILE_COUNT_Y; fy++)
            {
                ChessPiece piece = chessPieces[fx, fy];
                if (piece == null || piece.isWhite != forWhite) continue;

                // Проверяем все клетки доски как возможные цели
                for (int tx = 0; tx < TILE_COUNT_X; tx++)
                {
                    for (int ty = 0; ty < TILE_COUNT_Y; ty++)
                    {
                        if (!IsValidMove(piece, tx, ty)) continue;
                        // Ход валиден — не оставляет ли он короля под шахом?
                        if (!DoesMoveCauseCheck(piece, tx, ty, forWhite))
                            return true; // Нашли легальный ход!
                    }
                }
            }
        }
        return false; // Ни одного легального хода нет
    }

    // =============================================
    // ДИСПЕТЧЕР ПРАВИЛ
    // =============================================
    private bool IsValidMove(ChessPiece piece, int targetX, int targetY)
    {
        if (piece.currentX == targetX && piece.currentY == targetY) return false;

        if (chessPieces[targetX, targetY] != null && chessPieces[targetX, targetY].isWhite == piece.isWhite)
            return false;

        switch (piece.type)
        {
            case ChessPieceType.Pawn:   return IsPawnMoveValid(piece, targetX, targetY);
            case ChessPieceType.Rook:   return IsRookMoveValid(piece, targetX, targetY);
            case ChessPieceType.Knight: return IsKnightMoveValid(piece, targetX, targetY);
            case ChessPieceType.Bishop: return IsBishopMoveValid(piece, targetX, targetY);
            case ChessPieceType.Queen:  return IsRookMoveValid(piece, targetX, targetY) || IsBishopMoveValid(piece, targetX, targetY);
            case ChessPieceType.King:   return IsKingMoveValid(piece, targetX, targetY);
        }

        return false;
    }

    // =============================================
    // ЛОГИКА ПЕШКИ
    // =============================================
    private bool IsPawnMoveValid(ChessPiece piece, int targetX, int targetY)
    {
        int direction = piece.isWhite ? 1 : -1;

        // Ход вперёд на 1 клетку
        if (piece.currentX == targetX && targetY == piece.currentY + direction)
            return chessPieces[targetX, targetY] == null;

        // Ход вперёд на 2 клетки (с начальной позиции)
        bool isStartRow = (piece.isWhite && piece.currentY == 1) || (!piece.isWhite && piece.currentY == 6);
        if (piece.currentX == targetX && isStartRow && targetY == piece.currentY + direction * 2)
            return chessPieces[targetX, targetY] == null &&
                   chessPieces[targetX, piece.currentY + direction] == null;

        // Взятие по диагонали
        if (Mathf.Abs(targetX - piece.currentX) == 1 && targetY == piece.currentY + direction)
            if (chessPieces[targetX, targetY] != null && chessPieces[targetX, targetY].isWhite != piece.isWhite)
                return true;

        return false;
    }

    // =============================================
    // ЛОГИКА ЛАДЬИ
    // =============================================
    private bool IsRookMoveValid(ChessPiece piece, int targetX, int targetY)
    {
        if (piece.currentX != targetX && piece.currentY != targetY) return false;

        int xDir = (targetX > piece.currentX) ? 1 : (targetX < piece.currentX ? -1 : 0);
        int yDir = (targetY > piece.currentY) ? 1 : (targetY < piece.currentY ? -1 : 0);

        int checkX = piece.currentX + xDir;
        int checkY = piece.currentY + yDir;

        while (checkX != targetX || checkY != targetY)
        {
            if (chessPieces[checkX, checkY] != null) return false;
            checkX += xDir;
            checkY += yDir;
        }

        return true;
    }

    // =============================================
    // ЛОГИКА КОНЯ
    // =============================================
    private bool IsKnightMoveValid(ChessPiece piece, int targetX, int targetY)
    {
        int dx = Mathf.Abs(targetX - piece.currentX);
        int dy = Mathf.Abs(targetY - piece.currentY);
        return (dx == 2 && dy == 1) || (dx == 1 && dy == 2);
    }

    // =============================================
    // ЛОГИКА СЛОНА
    // =============================================
    private bool IsBishopMoveValid(ChessPiece piece, int targetX, int targetY)
    {
        if (Mathf.Abs(targetX - piece.currentX) != Mathf.Abs(targetY - piece.currentY)) return false;

        int xDir = (targetX > piece.currentX) ? 1 : -1;
        int yDir = (targetY > piece.currentY) ? 1 : -1;

        int checkX = piece.currentX + xDir;
        int checkY = piece.currentY + yDir;

        while (checkX != targetX && checkY != targetY)
        {
            if (chessPieces[checkX, checkY] != null) return false;
            checkX += xDir;
            checkY += yDir;
        }

        return true;
    }

    // =============================================
    // ЛОГИКА КОРОЛЯ
    // =============================================
    private bool IsKingMoveValid(ChessPiece piece, int targetX, int targetY)
    {
        int dx = Mathf.Abs(targetX - piece.currentX);
        int dy = Mathf.Abs(targetY - piece.currentY);
        return dx <= 1 && dy <= 1;
    }

    // =============================================
    // СПАВН ФИГУР
    // =============================================
    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];

        // --- БЕЛЫЕ ---
        for (int i = 0; i < 8; i++) SpawnSinglePiece(0, i, 1); // Пешки
        SpawnSinglePiece(1, 0, 0); SpawnSinglePiece(1, 7, 0);   // Ладьи
        SpawnSinglePiece(2, 1, 0); SpawnSinglePiece(2, 6, 0);   // Кони
        SpawnSinglePiece(3, 2, 0); SpawnSinglePiece(3, 5, 0);   // Слоны
        SpawnSinglePiece(4, 3, 0);                               // Ферзь
        SpawnSinglePiece(5, 4, 0);                               // Король

        // --- ЧЕРНЫЕ ---
        for (int i = 0; i < 8; i++) SpawnSinglePiece(6, i, 6); // Пешки
        SpawnSinglePiece(7, 0, 7); SpawnSinglePiece(7, 7, 7);   // Ладьи
        SpawnSinglePiece(8, 1, 7); SpawnSinglePiece(8, 6, 7);   // Кони
        SpawnSinglePiece(9, 2, 7); SpawnSinglePiece(9, 5, 7);   // Слоны
        SpawnSinglePiece(10, 3, 7);                              // Ферзь
        SpawnSinglePiece(11, 4, 7);                              // Король
    }

    private void SpawnSinglePiece(int prefabIndex, int x, int y)
    {
        GameObject go = Instantiate(chessPiecePrefabs[prefabIndex], transform);
        ChessPiece cp = go.GetComponent<ChessPiece>();
        cp.isWhite = (prefabIndex < 6);
        cp.PositionPiece(x, y, boardOffset, tileSize);
        chessPieces[x, y] = cp;
    }

    // =============================================
    // ГИЗМО (отладка в редакторе)
    // =============================================
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                Vector3 center = new Vector3(x * tileSize, 0, y * tileSize) + boardOffset;
                Gizmos.DrawWireCube(center, new Vector3(tileSize, 0.1f, tileSize));
            }

        if (hoverX != -1 && hoverY != -1)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(new Vector3(hoverX * tileSize, 0, hoverY * tileSize) + boardOffset,
                                new Vector3(tileSize, 0.1f, tileSize));
        }

        if (activePiece != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(new Vector3(activePiece.currentX * tileSize, 0, activePiece.currentY * tileSize) + boardOffset,
                                new Vector3(tileSize, 0.1f, tileSize));
        }
    }
}