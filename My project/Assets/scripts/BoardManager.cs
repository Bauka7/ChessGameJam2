// BoardManager.cs
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;



// ✅ enum наружу
public enum RuleType
{
    None,
    PawnForwardCapture,     // Rule 1
    AllPiecesMoveAsPawn,    // Rule 2
    KnightsSlide,           // Rule 3
    BishopPhase,            // Rule 4
    NoWayBack,              // Rule 5 — нельзя ходить назад
    KingIsKnight            // Rule 6 — король ходит как конь
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

    // =============================================
    // ✅ HIGHLIGHT — подсветка возможных ходов
    // =============================================
    [Header("Highlight (подсветка ходов)")]
    [Tooltip("Префаб подсветки — Quad с MeshRenderer, Y ~ 0.05")]
    public GameObject highlightPrefab;

    [Tooltip("Материал для пустой клетки (зелёный, прозрачный)")]
    public Material highlightMoveMaterial;

    [Tooltip("Материал для клетки с вражеской фигурой (красный, прозрачный)")]
    public Material highlightCaptureMaterial;

    [Tooltip("Материал для выбранной фигуры (синий/жёлтый)")]
    public Material highlightSelectedMaterial;

    // Массив хайлайт-объектов: по одному на каждую клетку
    private GameObject[,] highlightTiles;
    // Отдельный объект подсветки выбранной фигуры
    private GameObject selectedHighlight;

    [Header("UI")]
    public TMP_Text turnsText;
    public TMP_Text rulePanelTitle;
    public TMP_Text rulePanelText;
    public Button rulePanelOkButton;
    public RulePanelAnimator rulePanelAnimator;
    public float rulePanelTitleFontSize = 40f;
    public float rulePanelBodyFontSize = 30f;
    public float turnsFontSize = 28f;

    [Header("UI - Check & Game Over")]
    public CheckWarningUI checkWarningUI;
    public TMP_Text checkText;

    public GameObject gameOverPanel;
    public TMP_Text gameOverTitleText;
    public TMP_Text gameOverBodyText;

    public Button restartButton;
    public Button menuButton;

    private int halfMoveCount = 0;
    private RuleType currentRule = RuleType.None;
    private bool waitingForRulePanelConfirm = false;

    private ChessPiece activePiece = null;
    private int hoverX = -1;
    private int hoverY = -1;

    private bool isWhiteTurn = true;
    private bool gameOver = false;

    private RuleType[] orderedRules = new RuleType[]
    {
        RuleType.PawnForwardCapture,
        RuleType.AllPiecesMoveAsPawn,
        RuleType.KnightsSlide,
        RuleType.BishopPhase,
        RuleType.NoWayBack,
        RuleType.KingIsKnight
    };

    private int orderedRuleIndex = 0;
    private bool randomModeUnlocked = false;

    private enum RulePhase
    {
        StartDelay,
        ActiveRule,
        Cooldown
    }

    // =============================================
    // 🔊 AUDIO — SFX
    // =============================================
    [Header("Audio - SFX")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip moveSlideClip;

    [Tooltip("Звук при выборе фигуры (опционально)")]
    [SerializeField] private AudioClip selectPieceClip;

    [Tooltip("Короткий звук при смене правила (~2 сек)")]
    [SerializeField] private AudioClip ruleChangeClip;

    [Tooltip("Звук при шахе (Shah)")]
    [SerializeField] private AudioClip checkClip;

    [Tooltip("Звук при мате (победа)")]
    [SerializeField] private AudioClip checkmateClip;

    // =============================================
    // 🎵 AUDIO — Фоновая музыка (BGM)
    // =============================================
    [Header("Audio - Фоновая музыка")]
    [Tooltip("AudioSource для фоновой музыки. Создай отдельный GameObject с AudioSource и перетащи сюда.")]
    [SerializeField] private AudioSource bgmSource;

    [Tooltip("Аудио клип фоновой музыки (будет играть по кругу)")]
    [SerializeField] private AudioClip bgmClip;

    [Tooltip("Громкость фоновой музыки (0 = тихо, 1 = полная громкость)")]
    [Range(0f, 1f)]
    [SerializeField] private float bgmVolume = 0.4f;

    [Header("Rule Timeline (Rounds)")]
    public int startDelayRounds = 3;
    public int activeRuleRounds = 3;
    public int cooldownRounds = 2;

    private RulePhase rulePhase = RulePhase.StartDelay;
    private int phaseHalfMovesLeft = 0;

    private void Start()
    {
        SpawnAllPieces();

        // ✅ Инициализируем массив подсветок (пустые слоты)
        highlightTiles = new GameObject[TILE_COUNT_X, TILE_COUNT_Y];

        currentRule = RuleType.None;

        rulePhase = RulePhase.StartDelay;
        phaseHalfMovesLeft = startDelayRounds * 2;

        // Rule panel
        if (rulePanelAnimator != null)
            rulePanelAnimator.gameObject.SetActive(false);

        if (rulePanelOkButton != null)
        {
            rulePanelOkButton.onClick.RemoveListener(OnRulePanelOkPressed);
            rulePanelOkButton.onClick.AddListener(OnRulePanelOkPressed);
            rulePanelOkButton.gameObject.SetActive(false);
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (checkWarningUI != null) checkWarningUI.HideImmediate();

        // Buttons
        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartScene);
        }

        if (menuButton != null)
        {
            menuButton.onClick.RemoveAllListeners();
            menuButton.onClick.AddListener(LoadMenu);
        }

        ApplyUiFontSizes();
        UpdateTurnsText();

        // 🎵 Запускаем фоновую музыку
        StartBGM();

        Debug.Log("[BoardManager] Start OK. Scene: " + SceneManager.GetActiveScene().name);
    }

    // =============================================
    // 🎵 Фоновая музыка
    // =============================================

    /// <summary>
    /// Запускает фоновую музыку. Вызывается один раз при старте игры.
    /// </summary>
    private void StartBGM()
    {
        if (bgmSource == null)
        {
            Debug.LogWarning("[BGM] bgmSource не назначен в Inspector! Создай GameObject с AudioSource и назначь его.");
            return;
        }

        if (bgmClip == null)
        {
            Debug.LogWarning("[BGM] bgmClip не назначен в Inspector! Перетащи аудио файл в поле BGM Clip.");
            return;
        }

        bgmSource.clip = bgmClip;
        bgmSource.loop = true;           // 🔁 Играет по кругу
        bgmSource.volume = bgmVolume;    // 🔊 Устанавливаем громкость
        bgmSource.playOnAwake = false;
        bgmSource.Play();

        Debug.Log("[BGM] Фоновая музыка запущена.");
    }

    /// <summary>
    /// Останавливает фоновую музыку (например, при победе или выходе в меню).
    /// </summary>
    private void StopBGM()
    {
        if (bgmSource != null && bgmSource.isPlaying)
            bgmSource.Stop();
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

        // ✅ ПРАВАЯ кнопка мыши — снять выделение
        if (Input.GetMouseButtonDown(1))
        {
            ClearHighlights();
            activePiece = null;
            return;
        }

        if (Input.GetMouseButtonDown(0) && hoverX != -1 && hoverY != -1)
        {
            // ✅ Проверка: не кликаем ли по UI?
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (activePiece == null)
                SelectPiece(hoverX, hoverY);
            else
                MovePiece(hoverX, hoverY);
        }
    }

    // =============================================
    // ✅ SELECT + HIGHLIGHT
    // =============================================
    private void SelectPiece(int x, int y)
    {
        if (chessPieces[x, y] == null) return;

        if (chessPieces[x, y].isWhite == isWhiteTurn)
        {
            activePiece = chessPieces[x, y];

            // ✅ Показываем возможные ходы
            ShowHighlights(activePiece);

            // ✅ Звук выбора фигуры (опционально)
            PlaySelectSfx();
        }
        else
        {
            Debug.Log("Сейчас не ваш ход!");
        }
    }

    // =============================================
    // ✅ HIGHLIGHT — основные методы
    // =============================================

    /// <summary>
    /// Показывает подсветку для всех допустимых ходов выбранной фигуры.
    /// Зелёный = пустая клетка, Красный = вражеская фигура, Синий/Жёлтый = сама фигура.
    /// </summary>
    private void ShowHighlights(ChessPiece piece)
    {
        // Сначала очищаем старую подсветку
        ClearHighlights();

        if (highlightPrefab == null)
        {
            Debug.LogWarning("[Highlight] highlightPrefab не назначен в Inspector!");
            return;
        }

        // ✅ Подсветка выбранной фигуры
        selectedHighlight = SpawnHighlight(piece.currentX, piece.currentY, highlightSelectedMaterial);

        // ✅ Перебираем все клетки доски
        for (int tx = 0; tx < TILE_COUNT_X; tx++)
        {
            for (int ty = 0; ty < TILE_COUNT_Y; ty++)
            {
                // Пропускаем клетку самой фигуры
                if (tx == piece.currentX && ty == piece.currentY) continue;

                // Проверяем: допустим ли ход по правилам?
                if (!IsValidMove(piece, tx, ty)) continue;

                // Проверяем: не приводит ли ход к шаху своего короля?
                if (DoesMoveCauseCheck(piece, tx, ty, piece.isWhite)) continue;

                // Определяем цвет подсветки
                bool isEnemy = (chessPieces[tx, ty] != null && chessPieces[tx, ty].isWhite != piece.isWhite);
                Material mat = isEnemy ? highlightCaptureMaterial : highlightMoveMaterial;

                highlightTiles[tx, ty] = SpawnHighlight(tx, ty, mat);
            }
        }
    }

    /// <summary>
    /// Создаёт один объект подсветки на заданной клетке.
    /// </summary>
    private GameObject SpawnHighlight(int x, int y, Material mat)
    {
        // Позиция: центр клетки, Y чуть выше доски (0.05f), чтобы не z-fight
        Vector3 pos = new Vector3(x * tileSize + 2.04f, 0.05f, y * tileSize + 2f) + boardOffset;

        GameObject go = Instantiate(highlightPrefab, pos, Quaternion.Euler(90f, 0f, 0f), transform);
        go.name = $"Highlight_{x}_{y}";

        // ✅ Устанавливаем материал
        if (mat != null)
        {
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.material = mat;
        }

        return go;
    }

    /// <summary>
    /// Удаляет все объекты подсветки с доски.
    /// </summary>
    private void ClearHighlights()
    {
        // Очищаем подсветку всех клеток
        for (int x = 0; x < TILE_COUNT_X; x++)
        {
            for (int y = 0; y < TILE_COUNT_Y; y++)
            {
                if (highlightTiles[x, y] != null)
                {
                    Destroy(highlightTiles[x, y]);
                    highlightTiles[x, y] = null;
                }
            }
        }

        // Очищаем подсветку выбранной фигуры
        if (selectedHighlight != null)
        {
            Destroy(selectedHighlight);
            selectedHighlight = null;
        }
    }

    // =============================================
    // MOVE PIECE
    // =============================================
    private void MovePiece(int x, int y)
    {
        if (activePiece == null) return;

        // ✅ Клик на ту же фигуру — снять выделение
        if (x == activePiece.currentX && y == activePiece.currentY)
        {
            ClearHighlights();
            activePiece = null;
            return;
        }

        // ✅ Клик на другую свою фигуру — переключить выделение
        if (chessPieces[x, y] != null && chessPieces[x, y].isWhite == activePiece.isWhite)
        {
            activePiece = chessPieces[x, y];
            ShowHighlights(activePiece);
            return;
        }

        // ✅ Проверка допустимости хода
        if (!IsValidMove(activePiece, x, y))
        {
            Debug.Log("Ход недопустим по правилам!");
            ClearHighlights();
            activePiece = null;
            return;
        }

        if (DoesMoveCauseCheck(activePiece, x, y, activePiece.isWhite))
        {
            Debug.Log("Нельзя! Ваш король окажется под шахом!");
            ClearHighlights();
            activePiece = null;
            return;
        }

        // ✅ Убираем подсветку перед ходом
        ClearHighlights();

        ChessPiece capturedPiece = chessPieces[x, y];

        chessPieces[activePiece.currentX, activePiece.currentY] = null;
        chessPieces[x, y] = activePiece;
        activePiece.hasMoved = true;
        activePiece.PositionPiece(x, y, boardOffset, tileSize);

        if (capturedPiece != null)
            Destroy(capturedPiece.gameObject);

        PlayMoveSlideSfx();

        activePiece = null;

        // Передаём ход
        isWhiteTurn = !isWhiteTurn;

        if (cameraController != null)
        {
            if (isWhiteTurn) cameraController.SwitchToWhite();
            else cameraController.SwitchToBlack();
        }

        halfMoveCount++;

        CheckGameState();

        if (!gameOver)
        {
            UpdateRuleTimeline();
        }
        else
        {
            ForceHideRuleUI();
        }
    }

    private void UpdateRuleTimeline()
    {
        if (gameOver) return;

        if (phaseHalfMovesLeft > 0)
            phaseHalfMovesLeft--;

        switch (rulePhase)
        {
            case RulePhase.StartDelay:
                if (phaseHalfMovesLeft <= 0) StartNewRule();
                break;
            case RulePhase.ActiveRule:
                if (phaseHalfMovesLeft <= 0) EndCurrentRuleAndStartCooldown();
                break;
            case RulePhase.Cooldown:
                if (phaseHalfMovesLeft <= 0) StartNewRule();
                break;
        }

        UpdateTurnsText();
    }

    private void StartNewRule()
    {
        if (gameOver) return;

        currentRule = GetNextRule();

        rulePhase = RulePhase.ActiveRule;
        phaseHalfMovesLeft = activeRuleRounds * 2;

        waitingForRulePanelConfirm = true;

        // ✅ Звук смены правила — один короткий звук
        PlayRuleChangeSfx();

        // ✅ При открытии панели правил — убираем подсветку
        ClearHighlights();
        activePiece = null;

        if (rulePanelTitle != null) rulePanelTitle.text = "NEW RULE";
        if (rulePanelText != null) rulePanelText.text = GetRuleDescription();

        if (rulePanelOkButton != null)
            rulePanelOkButton.gameObject.SetActive(true);

        if (rulePanelAnimator != null)
            rulePanelAnimator.ShowPersistent(true);

        UpdateTurnsText();
    }

    private void EndCurrentRuleAndStartCooldown()
    {
        if (gameOver) return;

        currentRule = RuleType.None;
        rulePhase = RulePhase.Cooldown;
        phaseHalfMovesLeft = cooldownRounds * 2;
        UpdateTurnsText();
    }

    private RuleType GetNextRule()
    {
        if (!randomModeUnlocked)
        {
            RuleType rule = orderedRules[orderedRuleIndex];
            orderedRuleIndex++;

            if (orderedRuleIndex >= orderedRules.Length)
                randomModeUnlocked = true;

            return rule;
        }

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

    private void ForceHideRuleUI()
    {
        waitingForRulePanelConfirm = false;

        // ✅ При Game Over убираем подсветку
        ClearHighlights();
        activePiece = null;

        if (rulePanelOkButton != null)
            rulePanelOkButton.gameObject.SetActive(false);

        if (rulePanelAnimator != null)
        {
            if (rulePanelAnimator.gameObject.activeInHierarchy)
                rulePanelAnimator.HideNow();
            else
                rulePanelAnimator.gameObject.SetActive(false);
        }
    }

    private void ApplyUiFontSizes()
    {
        if (rulePanelTitle != null) rulePanelTitle.fontSize = rulePanelTitleFontSize;
        if (rulePanelText != null) rulePanelText.fontSize = rulePanelBodyFontSize;
        if (turnsText != null) turnsText.fontSize = turnsFontSize;
    }

    private string GetRuleDescription()
    {
        switch (currentRule)
        {
            case RuleType.PawnForwardCapture: return "Пешка может атаковать фигуру\nпрямо перед собой.";
            case RuleType.AllPiecesMoveAsPawn: return "Все фигуры кроме короля\nходят как пешки.";
            case RuleType.KnightsSlide: return "Кони забыли как прыгать.\nХодят на 2 клетки по прямой.";
            case RuleType.BishopPhase: return "Слоны проходят сквозь фигуры.";
            case RuleType.NoWayBack: return "Движение только вперёд!\nНикто не может отступать назад.";
            case RuleType.KingIsKnight: return "Король возомнил себя конём.\nПрыгает буквой Г.";
            default: return "";
        }
    }

    // =============================================
    // UI: CHECK + GAME OVER
    // =============================================
    private void ShowCheckWarning(bool whiteKingInCheck)
    {
        if (gameOver) return;

        if (checkText != null)
        {
            checkText.text = whiteKingInCheck
                ? "ШАХ! Белый король под ударом"
                : "ШАХ! Чёрный король под ударом";
        }

        if (checkWarningUI != null)
            checkWarningUI.ShowCheck();
        else
            Debug.LogWarning("[CHECK UI] checkWarningUI is NOT assigned in Inspector!");

        // 🔊 Звук шаха
        PlayCheckSfx();
    }

    private void HideCheckWarning()
    {
        if (checkWarningUI != null)
            checkWarningUI.HideImmediate();
    }

    private void ShowGameOver(string title, string body)
    {
        ForceHideRuleUI();
        HideCheckWarning();

        // 🎵 Останавливаем фоновую музыку при конце игры
        StopBGM();

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        else Debug.LogWarning("[GameOver] gameOverPanel NOT assigned!");

        if (gameOverTitleText != null) gameOverTitleText.text = title;
        else Debug.LogWarning("[GameOver] gameOverTitleText NOT assigned!");

        if (gameOverBodyText != null) gameOverBodyText.text = body;
        else Debug.LogWarning("[GameOver] gameOverBodyText NOT assigned!");
    }

    private void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void LoadMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }

    // =============================================
    // ШАХ / МАТ / ПАТ
    // =============================================
    private void CheckGameState()
    {
        bool sideToMoveIsWhite = isWhiteTurn;

        bool kingInCheck = IsKingInCheck(sideToMoveIsWhite);
        bool hasLegalMoves = HasAnyLegalMove(sideToMoveIsWhite);

        if (kingInCheck && !hasLegalMoves)
        {
            gameOver = true;
            bool whiteWon = !sideToMoveIsWhite;

            // 🔊 Звук мата (победы) — останавливает BGM и играет победный звук
            PlayCheckmateSfx();

            ShowGameOver("CHECKMATE", whiteWon ? "Белые победили!" : "Чёрные победили!");
            return;
        }

        if (!kingInCheck && !hasLegalMoves)
        {
            gameOver = true;

            // 🔊 При пате тоже можно сыграть звук мата (ничья)
            PlayCheckmateSfx();

            ShowGameOver("STALEMATE", "ПАТ! Ничья");
            return;
        }

        if (kingInCheck)
            ShowCheckWarning(sideToMoveIsWhite);
        else
            HideCheckWarning();
    }

    // =============================================
    // CHECK LOGIC
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

                if (IsValidMove(p, targetX, targetY))
                    return true;
            }
        }
        return false;
    }

    private bool DoesMoveCauseCheck(ChessPiece piece, int toX, int toY, bool isWhitePiece)
    {
        int fromX = piece.currentX;
        int fromY = piece.currentY;

        ChessPiece capturedBackup = chessPieces[toX, toY];

        chessPieces[fromX, fromY] = null;
        chessPieces[toX, toY] = piece;

        bool inCheck = IsKingInCheck(isWhitePiece);

        chessPieces[fromX, fromY] = piece;
        chessPieces[toX, toY] = capturedBackup;

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
    // MOVE VALIDATION + RULES
    // =============================================
    private bool IsValidMove(ChessPiece piece, int targetX, int targetY)
    {
        if (piece.currentX == targetX && piece.currentY == targetY) return false;

        if (chessPieces[targetX, targetY] != null &&
            chessPieces[targetX, targetY].isWhite == piece.isWhite)
            return false;

        // RULE 5: нельзя ходить назад
        if (currentRule == RuleType.NoWayBack)
        {
            if (piece.isWhite && targetY < piece.currentY) return false;
            if (!piece.isWhite && targetY > piece.currentY) return false;
        }

        // RULE 6: король ходит как конь
        if (currentRule == RuleType.KingIsKnight && piece.type == ChessPieceType.King)
            return IsKnightMoveValid(piece, targetX, targetY);

        if (currentRule == RuleType.AllPiecesMoveAsPawn && piece.type != ChessPieceType.King)
            return IsPawnMoveValid(piece, targetX, targetY);

        if (currentRule == RuleType.KnightsSlide && piece.type == ChessPieceType.Knight)
            return IsKnightSlideMove(piece, targetX, targetY);

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

    private bool IsPawnMoveValid(ChessPiece piece, int targetX, int targetY)
    {
        int direction = piece.isWhite ? 1 : -1;

        if (piece.currentX == targetX && targetY == piece.currentY + direction)
        {
            if (currentRule == RuleType.PawnForwardCapture)
            {
                if (chessPieces[targetX, targetY] != null && chessPieces[targetX, targetY].isWhite != piece.isWhite)
                    return true;
            }
            return chessPieces[targetX, targetY] == null;
        }

        bool isStartRow = (piece.isWhite && piece.currentY == 1) || (!piece.isWhite && piece.currentY == 6);
        if (piece.currentX == targetX && isStartRow && targetY == piece.currentY + direction * 2)
            return chessPieces[targetX, targetY] == null &&
                   chessPieces[targetX, piece.currentY + direction] == null;

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

    private bool IsKnightSlideMove(ChessPiece piece, int targetX, int targetY)
    {
        int dx = Mathf.Abs(targetX - piece.currentX);
        int dy = Mathf.Abs(targetY - piece.currentY);

        if ((dx == 2 && dy == 0) || (dx == 0 && dy == 2))
        {
            int xDir = (targetX > piece.currentX) ? 1 : (targetX < piece.currentX ? -1 : 0);
            int yDir = (targetY > piece.currentY) ? 1 : (targetY < piece.currentY ? -1 : 0);

            int checkX = piece.currentX + xDir;
            int checkY = piece.currentY + yDir;

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
    // SPAWN
    // =============================================
    private void SpawnAllPieces()
    {
        chessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];

        for (int i = 0; i < 8; i++) SpawnSinglePiece(0, i, 1);
        SpawnSinglePiece(1, 0, 0); SpawnSinglePiece(1, 7, 0);
        SpawnSinglePiece(2, 1, 0); SpawnSinglePiece(2, 6, 0);
        SpawnSinglePiece(3, 2, 0); SpawnSinglePiece(3, 5, 0);
        SpawnSinglePiece(4, 3, 0);
        SpawnSinglePiece(5, 4, 0);

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

    // =============================================
    // 🔊 AUDIO — методы воспроизведения
    // =============================================

    private void PlayMoveSlideSfx()
    {
        if (sfxSource == null || moveSlideClip == null) return;
        sfxSource.PlayOneShot(moveSlideClip);
    }

    private void PlaySelectSfx()
    {
        if (sfxSource == null || selectPieceClip == null) return;
        sfxSource.PlayOneShot(selectPieceClip);
    }

    // ✅ Короткий звук при смене правила
    private void PlayRuleChangeSfx()
    {
        if (sfxSource == null || ruleChangeClip == null) return;
        sfxSource.Stop();
        sfxSource.PlayOneShot(ruleChangeClip);
    }

    /// <summary>
    /// 🔊 Звук шаха — играет когда король под ударом.
    /// </summary>
    private void PlayCheckSfx()
    {
        if (sfxSource == null || checkClip == null) return;
        sfxSource.PlayOneShot(checkClip);
    }

    /// <summary>
    /// 🔊 Звук мата/победы — останавливает BGM и играет победный звук.
    /// </summary>
    private void PlayCheckmateSfx()
    {
        // Останавливаем фоновую музыку
        StopBGM();

        if (sfxSource == null || checkmateClip == null) return;
        sfxSource.Stop();
        sfxSource.PlayOneShot(checkmateClip);
    }
}