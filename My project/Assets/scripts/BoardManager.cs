using UnityEngine;
using TMPro;
using UnityEngine.UI;

// ✅ enum наружу
public enum RuleType
{
    None,

    PawnForwardCapture,     // Rule 1
    AllPiecesMoveAsPawn,    // Rule 2
    KnightsSlide,           // Rule 3
    BishopPhase             // Rule 4
}

public class BoardManager : MonoBehaviour
{
    public GameObject turnsBadge;

    [Header("Настройки доски")]
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    public float tileSize = 1.0f;
    public Vector3 boardOffset = Vector3.zero;

    [Header("Префабы и данные")]
    public GameObject[] chessPiecePrefabs;
    public ChessPiece[,] chessPieces;

    [Header("Камера")]
    public CameraController cameraController;

    // ❌ СТАРЫЕ ПОЛЯ Rules Shift больше не используются:
    // public int roundsPerShift
    // public int ruleDurationRounds
    // private int ruleHalfMovesLeft

    [Header("UI")]
    public TMP_Text turnsText;              // TurnsText (в углу)
    public TMP_Text rulePanelTitle;         // RulePanelTitle
    public TMP_Text rulePanelText;          // RulePanelText
    public Button rulePanelOkButton;        // RulePanel OK button
    public RulePanelAnimator rulePanelAnimator; // ✅ RulePanelAnimator на RulePanel
    public float rulePanelTitleFontSize = 40f;
    public float rulePanelBodyFontSize = 30f;
    public float turnsFontSize = 28f;

    private int halfMoveCount = 0;          // полуходы (каждый ход = +1)
    private RuleType currentRule = RuleType.None;
    private bool waitingForRulePanelConfirm = false;

    private ChessPiece activePiece = null;
    private int hoverX = -1;
    private int hoverY = -1;

    // Очередность хода
    private bool isWhiteTurn = true;

    // Флаг конца игры
    private bool gameOver = false;

    // ===== RULE ORDER SYSTEM =====
    private RuleType[] orderedRules = new RuleType[]
    {
        RuleType.PawnForwardCapture,
        RuleType.AllPiecesMoveAsPawn,
        RuleType.KnightsSlide,
        RuleType.BishopPhase
    };

    private int orderedRuleIndex = 0;
    private bool randomModeUnlocked = false;

    // ===== RULE TIMELINE SYSTEM (3 start delay → 3 active → 2 cooldown) =====
    private enum RulePhase
    {
        StartDelay,     // 3 раунда без правила в начале
        ActiveRule,     // правило активно 3 раунда
        Cooldown        // пауза 2 раунда
    }

    [Header("Rule Timeline (Rounds)")]
    public int startDelayRounds = 3;   // 1️⃣ старт: 3 раунда без правила
    public int activeRuleRounds = 3;   // правило действует 3 раунда
    public int cooldownRounds = 2;     // пауза 2 раунда

    private RulePhase rulePhase = RulePhase.StartDelay;
    private int phaseHalfMovesLeft = 0; // сколько полуходов осталось в текущей фазе

    private void Start()
    {
        SpawnAllPieces();

        // На старте: правило не активно
        currentRule = RuleType.None;

        // Стартовая задержка: 3 раунда без правила
        rulePhase = RulePhase.StartDelay;
        phaseHalfMovesLeft = startDelayRounds * 2;

        // Прячем панель аккуратно (аниматор сам держит alpha=0)
        if (rulePanelAnimator != null)
            rulePanelAnimator.gameObject.SetActive(false);

        if (rulePanelOkButton != null)
        {
            rulePanelOkButton.onClick.RemoveListener(OnRulePanelOkPressed);
            rulePanelOkButton.onClick.AddListener(OnRulePanelOkPressed);
            rulePanelOkButton.gameObject.SetActive(false);
        }

        ApplyUiFontSizes();
        UpdateTurnsText();
    }

    private void Update()
    {
        if (gameOver) return;
        if (waitingForRulePanelConfirm) return;
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
        if (activePiece == null) return;

        // Если кликнули на свою же фигуру — переключаем выбор
        if (chessPieces[x, y] != null && chessPieces[x, y].isWhite == activePiece.isWhite)
        {
            SelectPiece(x, y);
            return;
        }

        // Проверяем легальность
        if (!IsValidMove(activePiece, x, y))
        {
            Debug.Log("Ход недопустим по правилам!");
            activePiece = null;
            return;
        }

        // Проверяем шах себе
        if (DoesMoveCauseCheck(activePiece, x, y, activePiece.isWhite))
        {
            Debug.Log("Нельзя! Ваш король окажется под шахом!");
            activePiece = null;
            return;
        }

        // Запоминаем взятую фигуру
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

        // Передаём ход
        isWhiteTurn = !isWhiteTurn;

        if (cameraController != null)
        {
            if (isWhiteTurn) cameraController.SwitchToWhite();
            else cameraController.SwitchToBlack();
        }

        // ✅ полуход сделан
        halfMoveCount++;

        // ✅ обновляем таймлайн правил (старт → правило → пауза → ...)
        UpdateRuleTimeline();

        // Проверяем состояние игры
        CheckGameState();
    }

    // =============================================
    // RULE TIMELINE (3 start delay → 3 active → 2 cooldown)
    // =============================================
    private void UpdateRuleTimeline()
    {
        if (phaseHalfMovesLeft > 0)
            phaseHalfMovesLeft--;

        switch (rulePhase)
        {
            case RulePhase.StartDelay:
                if (phaseHalfMovesLeft <= 0)
                    StartNewRule();
                break;

            case RulePhase.ActiveRule:
                if (phaseHalfMovesLeft <= 0)
                    EndCurrentRuleAndStartCooldown();
                break;

            case RulePhase.Cooldown:
                if (phaseHalfMovesLeft <= 0)
                    StartNewRule();
                break;
        }

        UpdateTurnsText(); // ✅ всегда обновляем UI
    }

    private void StartNewRule()
    {
        currentRule = GetNextRule();

        rulePhase = RulePhase.ActiveRule;
        phaseHalfMovesLeft = activeRuleRounds * 2;

        waitingForRulePanelConfirm = true;

        Debug.Log($"NEW RULE: {GetRuleDescription()} ({phaseHalfMovesLeft} полуходов)");

        UpdateTurnsText();

        if (rulePanelTitle != null) rulePanelTitle.text = "NEW RULE";
        if (rulePanelText != null) rulePanelText.text = GetRuleDescription();

        if (rulePanelOkButton != null)
            rulePanelOkButton.gameObject.SetActive(true);

        if (rulePanelAnimator != null)
            rulePanelAnimator.ShowPersistent(true);
    }

    private void EndCurrentRuleAndStartCooldown()
    {
        currentRule = RuleType.None;
        rulePhase = RulePhase.Cooldown;
        phaseHalfMovesLeft = cooldownRounds * 2;

        Debug.Log("RULE ENDED → cooldown (pause)");

        UpdateTurnsText();
    }

    private RuleType GetNextRule()
    {
        // сначала выдаем правила по порядку
        if (!randomModeUnlocked)
        {
            RuleType rule = orderedRules[orderedRuleIndex];
            orderedRuleIndex++;

            if (orderedRuleIndex >= orderedRules.Length)
            {
                randomModeUnlocked = true; // после полного круга — включаем рандом
                Debug.Log("RANDOM RULE MODE ACTIVATED");
            }

            return rule;
        }

        // дальше — случайное правило (и после каждого правила будет 2 раунда пауза)
        int r = Random.Range(0, orderedRules.Length);
        return orderedRules[r];
    }

    private void UpdateTurnsText()
    {
        bool show = (rulePhase == RulePhase.ActiveRule);

        if (turnsBadge != null)
            turnsBadge.SetActive(show);

        if (turnsText != null)
        {
            turnsText.gameObject.SetActive(show);

            if (show)
            {
                int roundsLeft = Mathf.CeilToInt(phaseHalfMovesLeft / 2f);
                turnsText.text = $"Rounds left: {roundsLeft}";
            }
        }
    }

    private void OnRulePanelOkPressed()
    {
        waitingForRulePanelConfirm = false;

        if (rulePanelOkButton != null)
            rulePanelOkButton.gameObject.SetActive(false);

        if (rulePanelAnimator != null)
            rulePanelAnimator.HideNow();
    }

    private void ApplyUiFontSizes()
    {
        if (rulePanelTitle != null)
            rulePanelTitle.fontSize = rulePanelTitleFontSize;

        if (rulePanelText != null)
            rulePanelText.fontSize = rulePanelBodyFontSize;

        if (turnsText != null)
            turnsText.fontSize = turnsFontSize;
    }

    // ✅ разные тексты для разных правил
    private string GetRuleDescription()
    {
        switch (currentRule)
        {
            case RuleType.PawnForwardCapture:
                return "Пешка может атаковать фигуру\nпрямо перед собой.";

            case RuleType.AllPiecesMoveAsPawn:
                return "Все фигуры кроме короля\nходят как пешки.";

            case RuleType.KnightsSlide:
                return "Кони забыли как прыгать.\nХодят на 2 клетки по прямой.";

            case RuleType.BishopPhase:
                return "Слоны проходят сквозь фигуры.";

            default:
                return "";
        }
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
            gameOver = true;
            string winner = currentPlayerIsWhite ? "Чёрные" : "Белые";
            Debug.Log($"ШАХ И МАТ! Победили {winner}!");
        }
        else if (!kingInCheck && !hasLegalMoves)
        {
            gameOver = true;
            Debug.Log("ПАТ! Ничья!");
        }
        else if (kingInCheck)
        {
            Debug.Log($"ШАХ! {(currentPlayerIsWhite ? "Белый" : "Чёрный")} король под ударом!");
        }
        else
        {
            Debug.Log("Ход перешел к: " + (isWhiteTurn ? "Белым" : "Черным"));
        }
    }

    // =============================================
    // ШАХ: король под ударом?
    // =============================================
    private bool IsKingInCheck(bool whiteKing)
    {
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

        if (kingX == -1) return false;
        return IsSquareUnderAttack(kingX, kingY, whiteKing);
    }

    private bool IsSquareUnderAttack(int targetX, int targetY, bool defendingIsWhite)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                ChessPiece p = chessPieces[x, y];
                if (p == null || p.isWhite == defendingIsWhite) continue;
                if (IsValidMove(p, targetX, targetY)) return true;
            }
        }
        return false;
    }

    private bool DoesMoveCauseCheck(ChessPiece piece, int toX, int toY, bool isWhitePiece)
    {
        int fromX = piece.currentX;
        int fromY = piece.currentY;
        ChessPiece targetBackup = chessPieces[toX, toY];

        chessPieces[fromX, fromY] = null;
        chessPieces[toX, toY] = piece;
        piece.currentX = toX;
        piece.currentY = toY;

        bool inCheck = IsKingInCheck(isWhitePiece);

        piece.currentX = fromX;
        piece.currentY = fromY;
        chessPieces[fromX, fromY] = piece;
        chessPieces[toX, toY] = targetBackup;

        return inCheck;
    }

    private bool HasAnyLegalMove(bool forWhite)
    {
        for (int fx = 0; fx < TILE_COUNT_X; fx++)
        {
            for (int fy = 0; fy < TILE_COUNT_Y; fy++)
            {
                ChessPiece piece = chessPieces[fx, fy];
                if (piece == null || piece.isWhite != forWhite) continue;

                for (int tx = 0; tx < TILE_COUNT_X; tx++)
                {
                    for (int ty = 0; ty < TILE_COUNT_Y; ty++)
                    {
                        if (!IsValidMove(piece, tx, ty)) continue;
                        if (!DoesMoveCauseCheck(piece, tx, ty, forWhite))
                            return true;
                    }
                }
            }
        }
        return false;
    }

    // =============================================
    // ДИСПЕТЧЕР ПРАВИЛ
    // =============================================
    private bool IsValidMove(ChessPiece piece, int targetX, int targetY)
    {
        if (piece.currentX == targetX && piece.currentY == targetY) return false;

        if (chessPieces[targetX, targetY] != null && chessPieces[targetX, targetY].isWhite == piece.isWhite)
            return false;

        // ===== RULE 2 — все фигуры как пешки =====
        if (currentRule == RuleType.AllPiecesMoveAsPawn && piece.type != ChessPieceType.King)
            return IsPawnMoveValid(piece, targetX, targetY);

        // ===== RULE 3 — кони скользят =====
        if (currentRule == RuleType.KnightsSlide && piece.type == ChessPieceType.Knight)
            return IsKnightSlideMove(piece, targetX, targetY);

        switch (piece.type)
        {
            case ChessPieceType.Pawn: return IsPawnMoveValid(piece, targetX, targetY);
            case ChessPieceType.Rook: return IsRookMoveValid(piece, targetX, targetY);
            case ChessPieceType.Knight: return IsKnightMoveValid(piece, targetX, targetY);
            case ChessPieceType.Bishop: return IsBishopMoveValid(piece, targetX, targetY);
            case ChessPieceType.Queen: return IsRookMoveValid(piece, targetX, targetY) || IsBishopMoveValid(piece, targetX, targetY);
            case ChessPieceType.King: return IsKingMoveValid(piece, targetX, targetY);
        }
        return false;
    }

    // =============================================
    // ПЕШКА + PawnForwardCapture
    // =============================================
    private bool IsPawnMoveValid(ChessPiece piece, int targetX, int targetY)
    {
        int direction = piece.isWhite ? 1 : -1;

        // Вперёд на 1
        if (piece.currentX == targetX && targetY == piece.currentY + direction)
        {
            // RULE: можно бить вперёд если враг
            if (currentRule == RuleType.PawnForwardCapture)
            {
                if (chessPieces[targetX, targetY] != null && chessPieces[targetX, targetY].isWhite != piece.isWhite)
                    return true;
            }

            // обычное: вперед только если пусто
            return chessPieces[targetX, targetY] == null;
        }

        // Вперёд на 2 (старт)
        bool isStartRow = (piece.isWhite && piece.currentY == 1) || (!piece.isWhite && piece.currentY == 6);
        if (piece.currentX == targetX && isStartRow && targetY == piece.currentY + direction * 2)
            return chessPieces[targetX, targetY] == null &&
                   chessPieces[targetX, piece.currentY + direction] == null;

        // Диагональное взятие
        if (Mathf.Abs(targetX - piece.currentX) == 1 && targetY == piece.currentY + direction)
            if (chessPieces[targetX, targetY] != null && chessPieces[targetX, targetY].isWhite != piece.isWhite)
                return true;

        return false;
    }

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

    private bool IsKnightMoveValid(ChessPiece piece, int targetX, int targetY)
    {
        int dx = Mathf.Abs(targetX - piece.currentX);
        int dy = Mathf.Abs(targetY - piece.currentY);
        return (dx == 2 && dy == 1) || (dx == 1 && dy == 2);
    }

    // RULE 3 — кони больше не прыгают, а "скользят" на 2 клетки по прямой
    private bool IsKnightSlideMove(ChessPiece piece, int targetX, int targetY)
    {
        int dx = Mathf.Abs(targetX - piece.currentX);
        int dy = Mathf.Abs(targetY - piece.currentY);

        // только по прямой на 2 клетки
        if ((dx == 2 && dy == 0) || (dx == 0 && dy == 2))
        {
            int xDir = (targetX > piece.currentX) ? 1 : (targetX < piece.currentX ? -1 : 0);
            int yDir = (targetY > piece.currentY) ? 1 : (targetY < piece.currentY ? -1 : 0);

            int checkX = piece.currentX + xDir;
            int checkY = piece.currentY + yDir;

            // нельзя перепрыгивать фигуры (в середине)
            if (chessPieces[checkX, checkY] != null)
                return false;

            return true;
        }

        return false;
    }

    private bool IsBishopMoveValid(ChessPiece piece, int targetX, int targetY)
    {
        if (Mathf.Abs(targetX - piece.currentX) != Mathf.Abs(targetY - piece.currentY))
            return false;

        // RULE 4 — слон проходит сквозь фигуры
        if (currentRule == RuleType.BishopPhase)
            return true;

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

    private bool IsKingMoveValid(ChessPiece piece, int targetX, int targetY)
    {
        int dx = Mathf.Abs(targetX - piece.currentX);
        int dy = Mathf.Abs(targetY - piece.currentY);
        return dx <= 1 && dy <= 1;
    }

    // =============================================
    // СПАВН
    // =============================================
    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];

        // --- БЕЛЫЕ ---
        for (int i = 0; i < 8; i++) SpawnSinglePiece(0, i, 1);
        SpawnSinglePiece(1, 0, 0); SpawnSinglePiece(1, 7, 0);
        SpawnSinglePiece(2, 1, 0); SpawnSinglePiece(2, 6, 0);
        SpawnSinglePiece(3, 2, 0); SpawnSinglePiece(3, 5, 0);
        SpawnSinglePiece(4, 3, 0);
        SpawnSinglePiece(5, 4, 0);

        // --- ЧЕРНЫЕ ---
        for (int i = 0; i < 8; i++) SpawnSinglePiece(6, i, 6);
        SpawnSinglePiece(7, 0, 7); SpawnSinglePiece(7, 7, 7);
        SpawnSinglePiece(8, 1, 7); SpawnSinglePiece(8, 6, 7);
        SpawnSinglePiece(9, 2, 7); SpawnSinglePiece(9, 5, 7);
        SpawnSinglePiece(10, 3, 7);
        SpawnSinglePiece(11, 4, 7);
    }

    private void SpawnSinglePiece(int prefabIndex, int x, int y)
    {
        GameObject go = Instantiate(chessPiecePrefabs[prefabIndex], transform);
        ChessPiece cp = go.GetComponent<ChessPiece>();
        cp.isWhite = (prefabIndex < 6);
        cp.PositionPiece(x, y, boardOffset, tileSize);
        chessPieces[x, y] = cp;
    }

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