using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
<<<<<<< HEAD
using TMPro;

// ============================================================
//  GameManager.cs  —  Memory River (adaptado do original)
//  O que mudou vs original:
//  - Integração com PhaseConfig (3 fases com dados diferentes)
//  - Timer por fase (1:30 nas fases 1-2, 0:30 na fase 3)
//  - Feedback educativo ao acertar par (mensagem da carta)
//  - Efeito de lama ao errar
//  - Integração com ProgressionManager (estrelas e progresso)
//  - Integração com NarrativeManager (diálogos do Sato)
//  - Integração com SabotageController (Doutora Resíduo)
//  - Tudo que existia antes (multiplayer, animações) foi mantido
// ============================================================

public class GameManager : MonoBehaviour
{
    // -------------------------------------------------------
    //  SEÇÃO 1 — Campos originais (não alterados)
    // -------------------------------------------------------
=======

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class GameManager : MonoBehaviour
{
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
    [Header("Cards Generation")]
    public bool autoGenerateCards = true;
    public GameObject cardPrefab;
    public Transform cardsContainer;
    public int generatedCardsCount = 16;
    public int columns = 4;
    public Vector2 spacing = new Vector2(9.9225f, 9.9225f);
    public Vector2 cellSize = new Vector2(99.225f, 104.7375f);
    public Vector3 cardsScale = Vector3.one;
    public float previewSeconds = 2f;
    public float appearStaggerSeconds = 0.04f;
    public float matchedDisappearDelaySeconds = 0.45f;
    public float matchedDisappearDurationSeconds = 0.22f;

<<<<<<< HEAD
=======
    [Header("Editor Preview")]
    public bool editorPreview = false;
    public bool autoRefreshPreview = true;

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
    public EducationalInfo educationalInfo;

    public Sprite[] cardFace;
    public Sprite cardBack;
    public GameObject[] cards;
    public Text matchText;
    public Text launchModeText;

<<<<<<< HEAD
    public AudioClip successSound;
    public AudioClip errorSound;
    private AudioSource audioSource;
    public AudioClip backgroundMusic;
    private AudioSource musicSource;

=======
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
    private bool _init = false;
    private int _matches = 0;
    private bool _previewRunning = false;
    private bool _onlineMode = false;
    private bool _onlineConfigured = false;
    private bool _onlineResultDisplayed = false;
    private string _lastScoreEventSignature = "";
    private GameObject _resultOverlay;
    private Text _resultTitleText;
    private Text _resultSubtitleText;
    private Text _resultPointsText;
    private Text _resultHintText;
    private Button _resultBackButton;
    private Button _resultRematchButton;
    private Text _resultBackButtonText;
    private Text _resultRematchButtonText;
    private GameObject _onlineHudRoot;
    private Text _scorePanelText;
    private Text _scorePopupText;
    private Coroutine _scorePopupRoutine;

<<<<<<< HEAD
    // -------------------------------------------------------
    //  SEÇÃO 2 — Campos novos (adicionados para o GDD)
    // -------------------------------------------------------

    [Header("--- NOVO: Configuração de Fases ---")]
    [Tooltip("Arraste aqui o arquivo PhaseConfig_Main criado no projeto")]
    public PhaseConfig phaseConfig;

    [Header("--- NOVO: Timer ---")]
    [Tooltip("Texto na tela que mostra o tempo restante (ex: 1:30)")]
    public TextMeshProUGUI timerText;

    [Tooltip("Imagem de fundo que muda de cor conforme o estado do rio")]
    public Image riverBackground;

    [Header("--- NOVO: Feedback Educativo ---")]
    [Tooltip("Painel que aparece com a mensagem educativa ao acertar um par")]
    public GameObject educationalPanel;

    [Tooltip("Texto da mensagem educativa")]
    public TextMeshProUGUI educationalMessageText;

    [Tooltip("Texto do nome da carta acertada")]
    public TextMeshProUGUI cardNameText;

    [Header("--- NOVO: Efeito de Lama ---")]
    [Tooltip("Imagem semitransparente de lama que cobre o tabuleiro ao errar")]
    public Image mudOverlay;

    // -------------------------------------------------------
    //  Estado interno novo
    // -------------------------------------------------------
    private float _timeRemaining;
    private bool _timerRunning = false;
    private int _currentPhaseIndex = 0;
    private PhaseData _currentPhase;
    private int _totalPairs = 0;
    private bool _waitingForDialogue = false;

    // -------------------------------------------------------
    //  Start — igual ao original + inicialização das fases
    // -------------------------------------------------------
    private void Start()
    {
        _onlineMode = GameLaunchConfig.IsOnlineMode && RelayMatchController.Instance.SessionRunning;
        Card.DO_NOT = _onlineMode;

        // --- NOVO: carrega a fase atual do ProgressionManager ---
        if (ProgressionManager.Instance != null)
            _currentPhaseIndex = ProgressionManager.Instance.CurrentPhaseIndex;

        // --- NOVO: aplica configuração da fase atual ---
        ApplyPhaseConfig();

=======
    public AudioClip successSound;
    public AudioClip errorSound;
    private AudioSource audioSource;
    public AudioClip backgroundMusic;
    private AudioSource musicSource;

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (!editorPreview) return;
        if (Application.isPlaying) return;

        if (autoRefreshPreview)
        {
            GenerateEditorPreview();
        }
#endif
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Editor Preview")]
    public void GenerateEditorPreview()
    {
        if (cardsContainer == null || cardPrefab == null)
            return;

        ClearEditorPreview();
        EnsureCardsEditor();
    }

    private void ClearEditorPreview()
    {
        if (cardsContainer == null) return;

        for (int i = cardsContainer.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(cardsContainer.GetChild(i).gameObject);
        }
    }

    private void EnsureCardsEditor()
    {
        cards = new GameObject[generatedCardsCount];

        RectTransform containerRect = cardsContainer as RectTransform;

        int rows = Mathf.CeilToInt((float)generatedCardsCount / Mathf.Max(1, columns));
        float width = (columns * cellSize.x) + ((columns - 1) * spacing.x);
        float height = (rows * cellSize.y) + ((rows - 1) * spacing.y);

        if (containerRect != null)
        {
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        for (int i = 0; i < generatedCardsCount; i++)
        {
            GameObject cardObj = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab);
            cardObj.transform.SetParent(cardsContainer);
            cardObj.name = "Card_" + i;

            cards[i] = cardObj;

            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            if (cardRect != null)
            {
                int row = i / Mathf.Max(1, columns);
                int column = i % Mathf.Max(1, columns);

                cardRect.anchoredPosition = new Vector2(
                    column * (cellSize.x + spacing.x),
                    -row * (cellSize.y + spacing.y)
                );
            }
        }
    }
#endif

    private void Start()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == "Fase1")
        {
            generatedCardsCount = 8;
            columns = 2;
        }
        else if (sceneName == "Fase2")
        {
            generatedCardsCount = 12;
            columns = 3;
        }
        else if (sceneName == "Fase3")
        {
            generatedCardsCount = 16;
            columns = 4;
        }

     _onlineMode = GameLaunchConfig.IsOnlineMode && RelayMatchController.Instance.SessionRunning;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        EnsureCardsAreReady();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = backgroundMusic;
        musicSource.loop = true;
        musicSource.volume = 0.2f;
<<<<<<< HEAD
=======

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        if (backgroundMusic != null)
            musicSource.Play();

        _matches = 0;
<<<<<<< HEAD
=======

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        if (matchText != null)
            matchText.text = "Acertos: 0";

        UpdateLaunchModeText();

        if (_onlineMode)
            ConfigureOnlineMode(true);
<<<<<<< HEAD

        // --- NOVO: esconde painel educativo e lama no início ---
        if (educationalPanel != null)
            educationalPanel.SetActive(false);

        if (mudOverlay != null)
        {
            Color c = mudOverlay.color;
            c.a = 0f;
            mudOverlay.color = c;
        }

        // --- NOVO: mostra diálogo de início da fase ---
        if (!_onlineMode && NarrativeManager.Instance != null)
        {
            _waitingForDialogue = true;
            Card.DO_NOT = true;
            NarrativeManager.Instance.ShowPhaseStart(_currentPhaseIndex, OnPhaseDialogueComplete);
        }
        else
        {
            StartTimer();
        }
    }

    // -------------------------------------------------------
    //  NOVO: Chamado quando o diálogo de início da fase termina
    // -------------------------------------------------------
    private void OnPhaseDialogueComplete()
    {
        _waitingForDialogue = false;
        Card.DO_NOT = false;
        StartTimer();

        // Ativa sabotagem se for fase 3
        if (_currentPhase != null && _currentPhase.enableSabotage && SabotageController.Instance != null)
            SabotageController.Instance.ActivateSabotage(_currentPhase.maxSabotageCards);
    }

    // -------------------------------------------------------
    //  NOVO: Aplica os dados da fase atual (tabuleiro, timer, cor)
    // -------------------------------------------------------
    private void ApplyPhaseConfig()
    {
        if (phaseConfig == null)
        {
            Debug.LogWarning("[GameManager] PhaseConfig não configurado! Usando valores padrão.");
            _timeRemaining = 90f;
            return;
        }

        _currentPhase = phaseConfig.GetPhase(_currentPhaseIndex);
        if (_currentPhase == null) return;

        // Aplica dimensões do tabuleiro
        columns              = _currentPhase.columns;
        generatedCardsCount  = _currentPhase.columns * _currentPhase.rows;

        // Garante número par de cartas
        if (generatedCardsCount % 2 != 0)
            generatedCardsCount++;

        _totalPairs    = generatedCardsCount / 2;
        _timeRemaining = _currentPhase.timeLimit;

        // Aplica cor do rio no fundo
        if (riverBackground != null)
            riverBackground.color = _currentPhase.riverColor;

        // Carrega sprites das cartas da fase, se houver
        if (_currentPhase.availableCards != null && _currentPhase.availableCards.Length > 0)
        {
            cardFace = new Sprite[_currentPhase.availableCards.Length];
            for (int i = 0; i < _currentPhase.availableCards.Length; i++)
                cardFace[i] = _currentPhase.availableCards[i].cardSprite;
        }

        Debug.Log($"[GameManager] Fase {_currentPhaseIndex} carregada: {_currentPhase.phaseName} | {generatedCardsCount} cartas | {_timeRemaining}s");
    }

    // -------------------------------------------------------
    //  NOVO: Inicia o timer
    // -------------------------------------------------------
    private void StartTimer()
    {
        _timerRunning = true;
        UpdateTimerDisplay();
    }

    // -------------------------------------------------------
    //  Update — original + timer novo
    // -------------------------------------------------------
    private void Update()
    {
        if (!_onlineMode && !_init && !_waitingForDialogue)
            InitializeCards();

        // --- NOVO: atualiza o timer ---
        if (_timerRunning && !_onlineMode && _init && !_previewRunning)
        {
            _timeRemaining -= Time.deltaTime;

            if (_timeRemaining <= 0f)
            {
                _timeRemaining = 0f;
                _timerRunning  = false;
                UpdateTimerDisplay();
                OnTimerExpired();
            }
            else
            {
                UpdateTimerDisplay();
            }
        }

=======
    }

    private void Update()
    {
        if (!_onlineMode && !_init)
            InitializeCards();

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_onlineMode && RelayMatchController.Instance.SessionRunning)
                RelayMatchController.Instance.LeaveMatchAndReturnToMenu("Partida encerrada.");
            else
                SceneManager.LoadScene(SceneIds.MainMenu);
        }
    }

<<<<<<< HEAD
    // -------------------------------------------------------
    //  NOVO: Atualiza o texto do timer na tela (formato 1:30)
    // -------------------------------------------------------
    private void UpdateTimerDisplay()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(_timeRemaining / 60f);
        int seconds = Mathf.FloorToInt(_timeRemaining % 60f);
        timerText.text = $"{minutes}:{seconds:00}";

        // Deixa o timer vermelho nos últimos 10 segundos
        timerText.color = _timeRemaining <= 10f ? Color.red : Color.white;
    }

    // -------------------------------------------------------
    //  NOVO: Timer zerou — passa a vez ou encerra fase
    // -------------------------------------------------------
    private void OnTimerExpired()
    {
        Card.DO_NOT = true;
        Debug.Log("[GameManager] Tempo esgotado!");

        // Registra o progresso com os pares encontrados até agora
        if (ProgressionManager.Instance != null)
            ProgressionManager.Instance.CompletePhase(_currentPhaseIndex, _matches, _totalPairs, 0f);

        // Desativa sabotagem se estava ativa
        if (SabotageController.Instance != null)
            SabotageController.Instance.DeactivateSabotage();

        // Volta para o menu de história
        StartCoroutine(LoadNextSceneAfterDelay(SceneManager.GetActiveScene().name, 1.5f));
    }

    // -------------------------------------------------------
    //  OnCardClicked — original sem alterações
    // -------------------------------------------------------
=======
    
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
    public void OnCardClicked(int index)
    {
        if (_onlineMode)
        {
<<<<<<< HEAD
            if (Card.DO_NOT) return;
=======
            if (Card.DO_NOT)
                return;

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
            RelayMatchController.Instance.RequestFlip(index);
            return;
        }

        if (!_init) return;
        if (_previewRunning) return;
        if (Card.DO_NOT) return;
        if (cards == null || index < 0 || index >= cards.Length) return;
        if (cards[index] == null || !cards[index].activeSelf) return;

        Card card = cards[index].GetComponent<Card>();
        if (card == null) return;
        if (card.IsAnimating) return;
        if (card.matched) return;
<<<<<<< HEAD
        if (cards[index].transform.localScale == Vector3.zero) return;
=======

        // evita clicar em carta já removida
        if (cards[index].transform.localScale == Vector3.zero) return;

        // se já está aberta, ignora para evitar toggle indevido
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        if (card.state == 1) return;

        card.flipCard();
        CheckCards();
    }

<<<<<<< HEAD
    // -------------------------------------------------------
    //  CardComparison — original + feedback educativo e lama
    // -------------------------------------------------------
    private void CardComparison(List<int> c)
    {
        Card.DO_NOT = true;
        bool isMatch = cards[c[0]].GetComponent<Card>().cardValue == cards[c[1]].GetComponent<Card>().cardValue;

        if (isMatch)
        {
            _matches++;
            if (matchText != null)
                matchText.text = "Acertos: " + _matches;

            if (audioSource != null && successSound != null)
                audioSource.PlayOneShot(successSound);

            if (educationalInfo != null)
                educationalInfo.ShowSuccessMessage();

            // --- NOVO: mostra mensagem educativa da carta ---
            ShowEducationalMessage(cards[c[0]].GetComponent<Card>().cardValue);

            // --- NOVO: Sato elogia o jogador ---
            if (NarrativeManager.Instance != null)
                NarrativeManager.Instance.ShowSatoPraise();

            // --- NOVO: avisa o SabotageController ---
            if (SabotageController.Instance != null)
                SabotageController.Instance.OnPairFound(_matches);

            StartCoroutine(HideMatchedCardsAfterDelay(c));
        }
        else
        {
            if (audioSource != null && errorSound != null)
                audioSource.PlayOneShot(errorSound);

            if (educationalInfo != null)
                educationalInfo.ShowErrorMessage();

            // --- NOVO: efeito de lama ao errar ---
            StartCoroutine(ShowMudEffect());

            StartCoroutine(HideInfoAfterDelay());
        }

        if (isMatch)
        {
            for (int i = 0; i < c.Count; i++)
            {
                Card card = cards[c[i]].GetComponent<Card>();
                if (card == null) continue;
                card.state   = 1;
                card.matched = true;
            }
        }
        else
        {
            for (int i = 0; i < c.Count; i++)
            {
                Card card = cards[c[i]].GetComponent<Card>();
                if (card == null) continue;
                card.state = 0;
                card.falseCheck();
            }
        }

        if (_matches >= _totalPairs)
        {
            _timerRunning = false;

            // --- NOVO: registra conclusão da fase ---
            if (ProgressionManager.Instance != null)
                ProgressionManager.Instance.CompletePhase(_currentPhaseIndex, _matches, _totalPairs, _timeRemaining);

            // --- NOVO: desativa sabotagem ---
            if (SabotageController.Instance != null)
                SabotageController.Instance.DeactivateSabotage();

            string currentScene = SceneManager.GetActiveScene().name;
            if (isMatch)
                StartCoroutine(LoadNextSceneAfterDelay(currentScene, matchedDisappearDelaySeconds + matchedDisappearDurationSeconds));
            else
                LoadNextScene(currentScene);
        }
    }

    // -------------------------------------------------------
    //  NOVO: Mostra mensagem educativa da carta acertada
    // -------------------------------------------------------
    private void ShowEducationalMessage(int cardValue)
    {
        if (educationalPanel == null || _currentPhase == null) return;
        if (_currentPhase.availableCards == null) return;

        int index = cardValue - 1;
        if (index < 0 || index >= _currentPhase.availableCards.Length) return;

        CardData data = _currentPhase.availableCards[index];

        if (cardNameText != null)
            cardNameText.text = data.cardName;

        if (educationalMessageText != null)
            educationalMessageText.text = data.educationalMessage;

        educationalPanel.SetActive(true);
        StartCoroutine(HideEducationalPanelAfterDelay(3f));
    }

    private IEnumerator HideEducationalPanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (educationalPanel != null)
            educationalPanel.SetActive(false);
    }

    // -------------------------------------------------------
    //  NOVO: Efeito de lama ao errar
    // -------------------------------------------------------
    private IEnumerator ShowMudEffect()
    {
        if (mudOverlay == null || _currentPhase == null) yield break;

        float targetAlpha = _currentPhase.mudIntensity;
        float elapsed     = 0f;
        float fadeIn      = 0.3f;
        float hold        = 0.8f;
        float fadeOut     = 0.5f;

        // Aparece
        while (elapsed < fadeIn)
        {
            elapsed += Time.deltaTime;
            Color c = mudOverlay.color;
            c.a = Mathf.Lerp(0f, targetAlpha, elapsed / fadeIn);
            mudOverlay.color = c;
            yield return null;
        }

        yield return new WaitForSeconds(hold);

        // Desaparece
        elapsed = 0f;
        while (elapsed < fadeOut)
        {
            elapsed += Time.deltaTime;
            Color c = mudOverlay.color;
            c.a = Mathf.Lerp(targetAlpha, 0f, elapsed / fadeOut);
            mudOverlay.color = c;
            yield return null;
        }

        Color final = mudOverlay.color;
        final.a = 0f;
        mudOverlay.color = final;
    }

    // -------------------------------------------------------
    //  Todos os métodos originais abaixo — sem alterações
    // -------------------------------------------------------

    private void EnsureCardsAreReady()
    {
        if (!autoGenerateCards) return;
=======
    private void EnsureCardsAreReady()
    {
        if (!autoGenerateCards)
            return;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb

        if (cards != null && cards.Length == generatedCardsCount && cardsContainer != null && cardsContainer.childCount == generatedCardsCount)
            return;

        if (cardPrefab == null || cardsContainer == null)
        {
            Debug.LogWarning("Auto geração ativa, mas cardPrefab/cardsContainer não foram configurados no GameManager.");
            return;
        }

<<<<<<< HEAD
        if (generatedCardsCount < 2) generatedCardsCount = 2;
        if (generatedCardsCount % 2 != 0) generatedCardsCount += 1;

=======
        if (generatedCardsCount < 2)
            generatedCardsCount = 2;

        if (generatedCardsCount % 2 != 0)
            generatedCardsCount += 1;

        // Unity pode manter valores antigos serializados na cena. Forca um minimo util.
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        if (spacing.x < 9.9225f || spacing.y < 9.9225f)
            spacing = new Vector2(9.9225f, 9.9225f);

        if (cellSize.x < 99.225f || cellSize.y < 104.7375f)
            cellSize = new Vector2(99.225f, 104.7375f);

        for (int i = cardsContainer.childCount - 1; i >= 0; i--)
<<<<<<< HEAD
            Destroy(cardsContainer.GetChild(i).gameObject);
=======
            {
            #if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(cardsContainer.GetChild(i).gameObject);
                else
                    Destroy(cardsContainer.GetChild(i).gameObject);
            #else
                Destroy(cardsContainer.GetChild(i).gameObject);
            #endif
            }
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb

        RectTransform containerRect = cardsContainer as RectTransform;
        if (containerRect != null)
        {
<<<<<<< HEAD
            int rows   = Mathf.CeilToInt((float)generatedCardsCount / Mathf.Max(1, columns));
            float width  = (columns * cellSize.x) + ((columns - 1) * spacing.x);
            float height = (rows * cellSize.y) + ((rows - 1) * spacing.y);
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   height);
        }

        cardsContainer.localScale = cardsScale;
=======
            int rows = Mathf.CeilToInt((float)generatedCardsCount / Mathf.Max(1, columns));
            float width = (columns * cellSize.x) + ((columns - 1) * spacing.x);
            float height = (rows * cellSize.y) + ((rows - 1) * spacing.y);

            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        cardsContainer.localScale = cardsScale;

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        cards = new GameObject[generatedCardsCount];

        for (int i = 0; i < generatedCardsCount; i++)
        {
            GameObject cardObj = Instantiate(cardPrefab, cardsContainer);
            cardObj.name = "Card_" + i;
<<<<<<< HEAD
            cards[i]     = cardObj;
=======
            cards[i] = cardObj;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
            cardObj.transform.localScale = Vector3.one;

            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            if (cardRect != null)
            {
<<<<<<< HEAD
                int row    = i / Mathf.Max(1, columns);
                int column = i % Mathf.Max(1, columns);
                cardRect.anchorMin = new Vector2(0f, 1f);
                cardRect.anchorMax = new Vector2(0f, 1f);
                cardRect.pivot     = new Vector2(0f, 1f);
                cardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellSize.x);
                cardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   cellSize.y);
                cardRect.anchoredPosition = new Vector2(
                    column * (cellSize.x + spacing.x),
                    -row   * (cellSize.y + spacing.y)
                );
=======
                int row = i / Mathf.Max(1, columns);
                int column = i % Mathf.Max(1, columns);

                cardRect.anchorMin = new Vector2(0f, 1f);
                cardRect.anchorMax = new Vector2(0f, 1f);
                cardRect.pivot = new Vector2(0f, 1f);
                cardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellSize.x);
                cardRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellSize.y);
               float totalWidth = (columns * cellSize.x) + ((columns - 1) * spacing.x);
               float totalHeight = (Mathf.CeilToInt((float)generatedCardsCount / columns) * cellSize.y);

               cardRect.anchoredPosition = new Vector2(
        column * (cellSize.x + spacing.x),
        -row * (cellSize.y + spacing.y)
    );
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
                cardRect.localScale = Vector3.one;
            }

            Card card = cardObj.GetComponent<Card>();
<<<<<<< HEAD
            if (card == null) card = cardObj.AddComponent<Card>();

            MemoryCardButton memoryCardButton = cardObj.GetComponent<MemoryCardButton>();
            if (memoryCardButton == null) memoryCardButton = cardObj.AddComponent<MemoryCardButton>();
=======
            if (card == null)
                card = cardObj.AddComponent<Card>();

            MemoryCardButton memoryCardButton = cardObj.GetComponent<MemoryCardButton>();
            if (memoryCardButton == null)
                memoryCardButton = cardObj.AddComponent<MemoryCardButton>();
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb

            memoryCardButton.SetCardIndex(i);
            memoryCardButton.SetGameManager(this);

            Button button = cardObj.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(memoryCardButton.OnClickFlip);
            }
        }
    }

    public void ConfigureOnlineMode(bool resetVisuals = false)
    {
        _onlineMode = true;
        EnsureCardsAreReady();
<<<<<<< HEAD
=======

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        EnsureResultOverlay();
        EnsureOnlineHud();

        if (_onlineConfigured && !resetVisuals)
        {
            UpdateLaunchModeText();
            return;
        }

<<<<<<< HEAD
        _init                  = true;
        _previewRunning        = false;
        _matches               = 0;
=======
        _init = true;
        _previewRunning = false;
        _matches = 0;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        _onlineResultDisplayed = false;
        _lastScoreEventSignature = "";
        HideResultOverlay();
        ClearScorePopup();

        if (matchText != null)
            matchText.text = "Aguardando dados da partida...";

        if (_scorePanelText != null)
            _scorePanelText.text = "Pontos: 0\nOponente: 0";

<<<<<<< HEAD
        if (cards == null) return;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            cards[i].SetActive(true);
            Card card = cards[i].GetComponent<Card>();
            if (card == null) continue;
            card.initialized = true;
            card.matched     = false;
            card.state       = 0;
=======
        if (cards == null)
            return;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;

            cards[i].SetActive(true);
            Card card = cards[i].GetComponent<Card>();
            if (card == null)
                continue;

            card.initialized = true;
            card.matched = false;
            card.state = 0;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
            card.ResetOnlinePresentation(cardBack);
        }

        _onlineConfigured = true;
        UpdateLaunchModeText();
    }

    public void ApplyOnlineSnapshot(
<<<<<<< HEAD
        int[] cardValues, int[] cardStates,
        int scorePlayerOne, int scorePlayerTwo,
        int currentTurnSlot, int localPlayerSlot,
        bool previewRunning, bool waitingForOpponent,
        bool gameOver, int winnerSlot,
        int lastScoreEventSlot, int lastPointsEarned,
        int lastComboValue, string statusMessage)
    {
        _onlineMode     = true;
        EnsureCardsAreReady();
        _init           = true;
        _previewRunning = previewRunning;
        _matches        = scorePlayerOne + scorePlayerTwo;

        if (cards == null || cardValues == null || cardStates == null) return;
=======
        int[] cardValues,
        int[] cardStates,
        int scorePlayerOne,
        int scorePlayerTwo,
        int currentTurnSlot,
        int localPlayerSlot,
        bool previewRunning,
        bool waitingForOpponent,
        bool gameOver,
        int winnerSlot,
        int lastScoreEventSlot,
        int lastPointsEarned,
        int lastComboValue,
        string statusMessage)
    {
        _onlineMode = true;
        EnsureCardsAreReady();
        _init = true;
        _previewRunning = previewRunning;
        _matches = scorePlayerOne + scorePlayerTwo;

        if (cards == null || cardValues == null || cardStates == null)
            return;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb

        int total = Mathf.Min(cards.Length, Mathf.Min(cardValues.Length, cardStates.Length));
        for (int i = 0; i < total; i++)
        {
<<<<<<< HEAD
            if (cards[i] == null) continue;
            Card card = cards[i].GetComponent<Card>();
            if (card == null) continue;
            int cardValue    = Mathf.Max(1, cardValues[i]);
            card.initialized = true;
            card.cardValue   = cardValue;
            card.matched     = cardStates[i] == 2;
=======
            if (cards[i] == null)
                continue;

            Card card = cards[i].GetComponent<Card>();
            if (card == null)
                continue;

            int cardValue = Mathf.Max(1, cardValues[i]);
            card.initialized = true;
            card.cardValue = cardValue;
            card.matched = cardStates[i] == 2;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
            card.ApplyOnlineVisual(getCardFace(cardValue), cardBack, cardStates[i]);
        }

        bool localCanPlay = !waitingForOpponent && !previewRunning && !gameOver && localPlayerSlot >= 0 && currentTurnSlot == localPlayerSlot;
        Card.DO_NOT = !localCanPlay;

        if (matchText != null)
        {
<<<<<<< HEAD
            if (localPlayerSlot == 0)      matchText.text = "Pontos: " + scorePlayerOne + "  |  Oponente: " + scorePlayerTwo;
            else if (localPlayerSlot == 1) matchText.text = "Pontos: " + scorePlayerTwo + "  |  Oponente: " + scorePlayerOne;
            else                           matchText.text = "Jogador 1: " + scorePlayerOne + " pts  |  Jogador 2: " + scorePlayerTwo + " pts";
=======
            if (localPlayerSlot == 0)
                matchText.text = "Pontos: " + scorePlayerOne + "  |  Oponente: " + scorePlayerTwo;
            else if (localPlayerSlot == 1)
                matchText.text = "Pontos: " + scorePlayerTwo + "  |  Oponente: " + scorePlayerOne;
            else
                matchText.text = "Jogador 1: " + scorePlayerOne + " pts  |  Jogador 2: " + scorePlayerTwo + " pts";
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        }

        UpdateScorePanel(scorePlayerOne, scorePlayerTwo, localPlayerSlot);
        HandleOnlineScorePopup(scorePlayerOne, scorePlayerTwo, localPlayerSlot, lastScoreEventSlot, lastPointsEarned, lastComboValue);

<<<<<<< HEAD
        if (launchModeText == null) launchModeText = CreateLaunchModeText();
=======
        if (launchModeText == null)
            launchModeText = CreateLaunchModeText();

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        if (launchModeText != null)
        {
            string localPlayerText = localPlayerSlot >= 0 ? "Jogador " + (localPlayerSlot + 1) : "Conectando...";
            string turnText = waitingForOpponent ? "Aguardando o segundo jogador" :
<<<<<<< HEAD
                              gameOver           ? "Partida encerrada" :
                              previewRunning     ? "Memorize as cartas" :
                              currentTurnSlot == localPlayerSlot ? "Sua vez" : "Vez do oponente";
=======
                gameOver ? "Partida encerrada" :
                previewRunning ? "Memorize as cartas" :
                currentTurnSlot == localPlayerSlot ? "Sua vez" : "Vez do oponente";

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
            launchModeText.text = "Sala: " + GameLaunchConfig.RoomCode + "  |  " + localPlayerText + "\n" + turnText + "\n" + statusMessage;
        }

        if (gameOver)
        {
            Card.DO_NOT = true;
            ShowOnlineResultOverlay(scorePlayerOne, scorePlayerTwo, localPlayerSlot, winnerSlot, statusMessage);
        }
        else
        {
            _onlineResultDisplayed = false;
            HideResultOverlay();
        }
    }

    private void InitializeCards()
    {
        if (cards == null) return;
<<<<<<< HEAD
        Card.DO_NOT     = true;
=======

        Card.DO_NOT = true;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        _previewRunning = true;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            cards[i].SetActive(true);
            cards[i].transform.localScale = Vector3.one;
            Card card = cards[i].GetComponent<Card>();
<<<<<<< HEAD
            if (card != null) { card.initialized = false; card.matched = false; card.state = 0; }
=======
            if (card != null)
            {
                card.initialized = false;
                card.matched = false;
                card.state = 0;
            }
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        }

        for (int id = 0; id < 2; id++)
        {
            for (int i = 1; i <= (cards.Length / 2); i++)
            {
<<<<<<< HEAD
                bool test = false; int choice = 0;
=======
                bool test = false;
                int choice = 0;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
                while (!test)
                {
                    choice = Random.Range(0, cards.Length);
                    Card choiceCard = cards[choice].GetComponent<Card>();
                    test = choiceCard != null && !choiceCard.initialized;
                }
<<<<<<< HEAD
                Card selected = cards[choice].GetComponent<Card>();
                selected.cardValue   = i;
=======

                Card selected = cards[choice].GetComponent<Card>();
                selected.cardValue = i;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
                selected.initialized = true;
            }
        }

        foreach (GameObject c in cards)
        {
            if (c == null) continue;
            Card card = c.GetComponent<Card>();
<<<<<<< HEAD
            if (card != null) { card.setupGraphics(); card.PrepareForAppear(); }
=======
            if (card != null)
            {
                card.setupGraphics();
                card.PrepareForAppear();
            }
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        }

        _init = true;
        StartCoroutine(HidePreviewCardsAfterDelay());
        StartCoroutine(PlayAppearSequence());
    }

<<<<<<< HEAD
    public Sprite getCardBack() => cardBack;

    public Sprite getCardFace(int i)
    {
        if (cardFace == null || cardFace.Length == 0) return cardBack;
        return cardFace[Mathf.Clamp(i - 1, 0, cardFace.Length - 1)];
=======
    public Sprite getCardBack()
    {
        return cardBack;
    }

    public Sprite getCardFace(int i)
    {
        if (cardFace == null || cardFace.Length == 0)
            return cardBack;

        int index = Mathf.Clamp(i - 1, 0, cardFace.Length - 1);
        return cardFace[index];
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
    }

    private void CheckCards()
    {
        List<int> c = new List<int>();
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null || !cards[i].activeSelf) continue;
            Card card = cards[i].GetComponent<Card>();
<<<<<<< HEAD
            if (card != null && !card.matched && card.state == 1) c.Add(i);
        }
        if (c.Count == 2) CardComparison(c);
=======
            if (card != null && !card.matched && card.state == 1)
                c.Add(i);
        }

        if (c.Count == 2)
            CardComparison(c);
    }

    private void CardComparison(List<int> c)
    {
        Card.DO_NOT = true;
        bool isMatch = cards[c[0]].GetComponent<Card>().cardValue == cards[c[1]].GetComponent<Card>().cardValue;

        if (isMatch)
        {
            _matches++;
            if (matchText != null)
                matchText.text = "Acertos: " + _matches;

            if (audioSource != null && successSound != null)
                audioSource.PlayOneShot(successSound);

            if (educationalInfo != null)
                educationalInfo.ShowSuccessMessage();

            StartCoroutine(HideMatchedCardsAfterDelay(c));
        }
        else
        {
            if (audioSource != null && errorSound != null)
                audioSource.PlayOneShot(errorSound);

            if (educationalInfo != null)
                educationalInfo.ShowErrorMessage();

            StartCoroutine(HideInfoAfterDelay());
        }

        if (isMatch)
        {
            for (int i = 0; i < c.Count; i++)
            {
                Card card = cards[c[i]].GetComponent<Card>();
                if (card == null) continue;
                card.state = 1;
                card.matched = true;
            }
        }
        else
        {
            for (int i = 0; i < c.Count; i++)
            {
                Card card = cards[c[i]].GetComponent<Card>();
                if (card == null) continue;
                card.state = 0;
                card.falseCheck();
            }
        }

        if (_matches >= cards.Length / 2)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (isMatch)
            {
                StartCoroutine(LoadNextSceneAfterDelay(currentScene, matchedDisappearDelaySeconds + matchedDisappearDurationSeconds));
            }
            else
            {
                LoadNextScene(currentScene);
            }
        }
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
    }

    private IEnumerator HideInfoAfterDelay()
    {
        yield return new WaitForSeconds(1f);
<<<<<<< HEAD
        if (educationalInfo != null) educationalInfo.HideInfo();
=======
        if (educationalInfo != null)
            educationalInfo.HideInfo();
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
    }

    private IEnumerator HideMatchedCardsAfterDelay(List<int> c)
    {
        yield return new WaitForSeconds(matchedDisappearDelaySeconds);

<<<<<<< HEAD
        Card first  = cards[c[0]] != null ? cards[c[0]].GetComponent<Card>() : null;
        Card second = cards[c[1]] != null ? cards[c[1]].GetComponent<Card>() : null;

        if (first  != null) StartCoroutine(first.PlayDisappear());
        if (second != null) StartCoroutine(second.PlayDisappear());

        yield return new WaitForSeconds(matchedDisappearDurationSeconds);
=======
        Card first = null;
        Card second = null;

        if (cards[c[0]] != null)
            first = cards[c[0]].GetComponent<Card>();

        if (cards[c[1]] != null)
            second = cards[c[1]].GetComponent<Card>();

        if (first != null)
            StartCoroutine(first.PlayDisappear());

        if (second != null)
            StartCoroutine(second.PlayDisappear());

        yield return new WaitForSeconds(matchedDisappearDurationSeconds);

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        Card.DO_NOT = false;
    }

    private IEnumerator HidePreviewCardsAfterDelay()
    {
        float totalDelay = previewSeconds + Mathf.Max(0f, (cards.Length - 1) * appearStaggerSeconds);
        yield return new WaitForSeconds(totalDelay);
<<<<<<< HEAD
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            Card card = cards[i].GetComponent<Card>();
            if (card != null) card.ShowBack();
        }
        _previewRunning = false;
        Card.DO_NOT     = false;
=======

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;

            Card card = cards[i].GetComponent<Card>();
            if (card == null) continue;

            card.ShowBack();
        }

        _previewRunning = false;
        Card.DO_NOT = false;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
    }

    private IEnumerator PlayAppearSequence()
    {
        for (int i = 0; i < cards.Length; i++)
        {
<<<<<<< HEAD
            if (cards[i] == null) continue;
            Card card = cards[i].GetComponent<Card>();
            if (card != null) StartCoroutine(card.PlayAppear(i * appearStaggerSeconds));
        }
=======
            if (cards[i] == null)
                continue;

            Card card = cards[i].GetComponent<Card>();
            if (card != null)
                StartCoroutine(card.PlayAppear(i * appearStaggerSeconds));
        }

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        yield return null;
    }

    private IEnumerator LoadNextSceneAfterDelay(string currentScene, float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadNextScene(currentScene);
    }

    private void LoadNextScene(string currentScene)
    {
<<<<<<< HEAD
        if (currentScene == SceneIds.Gameplay)
        {
            if (GameLaunchConfig.CurrentMode == GameLaunchMode.Story)
            {
                // Avança para a próxima fase automaticamente
                if (ProgressionManager.Instance != null)
                {
                    int next = _currentPhaseIndex + 1;
                    if (next < 3)
                        ProgressionManager.Instance.SetCurrentPhase(next);
                }
                SceneManager.LoadScene(SceneIds.StoryMenu);
            }
            else
            {
                SceneManager.LoadScene(SceneIds.MainMenu);
            }
            return;
        }
        Debug.Log("Fim do jogo. Nenhuma próxima fase configurada.");
=======
        if (currentScene == SceneIds.Level1)
        {
            SceneManager.LoadScene(SceneIds.Level2);
        }
        else if (currentScene == SceneIds.Level2)
        {
            SceneManager.LoadScene(SceneIds.Level3);
        }
        else if (currentScene == SceneIds.Level3)
        {
            SceneManager.LoadScene(SceneIds.MainMenu);
        }
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
    }

    private void UpdateLaunchModeText()
    {
<<<<<<< HEAD
        if (launchModeText == null) launchModeText = CreateLaunchModeText();
        if (launchModeText == null) return;
=======
        if (launchModeText == null)
            launchModeText = CreateLaunchModeText();

        if (launchModeText == null)
            return;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb

        switch (GameLaunchConfig.CurrentMode)
        {
            case GameLaunchMode.CreateMatch:
<<<<<<< HEAD
                launchModeText.text = "Modo: Criar Partida  |  Codigo: " + GameLaunchConfig.RoomCode; break;
            case GameLaunchMode.JoinMatch:
                launchModeText.text = "Modo: Encontrar Partida  |  Codigo: " + GameLaunchConfig.RoomCode; break;
            default:
                launchModeText.text = "Modo: Historia  |  Capitulo: " + GameLaunchConfig.StoryChapter; break;
=======
                launchModeText.text = "Modo: Criar Partida  |  Codigo: " + GameLaunchConfig.RoomCode;
                break;
            case GameLaunchMode.JoinMatch:
                launchModeText.text = "Modo: Encontrar Partida  |  Codigo: " + GameLaunchConfig.RoomCode;
                break;
            default:
                launchModeText.text = "Modo: Historia  |  Capitulo: " + GameLaunchConfig.StoryChapter;
                break;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        }
    }

    private Text CreateLaunchModeText()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
<<<<<<< HEAD
        if (canvas == null) return null;

        Transform existing = canvas.transform.Find("LaunchModeText");
        if (existing != null) return existing.GetComponent<Text>();

        GameObject textObject = new GameObject("LaunchModeText", typeof(RectTransform));
        textObject.transform.SetParent(canvas.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0f, 1f);
        rect.anchorMax        = new Vector2(0f, 1f);
        rect.pivot            = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(30f, -20f);
        rect.sizeDelta        = new Vector2(800f, 60f);

        Text text = textObject.AddComponent<Text>();
        text.font            = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize        = 22;
        text.alignment       = TextAnchor.UpperLeft;
        text.color           = new Color(0.98f, 0.96f, 0.85f);
=======
        if (canvas == null)
            return null;

        Transform existing = canvas.transform.Find("LaunchModeText");
        if (existing != null)
            return existing.GetComponent<Text>();

        GameObject textObject = new GameObject("LaunchModeText", typeof(RectTransform));
        textObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(30f, -20f);
        rect.sizeDelta = new Vector2(800f, 60f);

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.alignment = TextAnchor.UpperLeft;
        text.color = new Color(0.98f, 0.96f, 0.85f);
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        text.supportRichText = false;
        return text;
    }

    private void EnsureOnlineHud()
    {
<<<<<<< HEAD
        if (_onlineHudRoot != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
=======
        if (_onlineHudRoot != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _onlineHudRoot = new GameObject("OnlineHudRoot", typeof(RectTransform), typeof(Image));
        _onlineHudRoot.transform.SetParent(canvas.transform, false);
<<<<<<< HEAD
        RectTransform rootRect = _onlineHudRoot.GetComponent<RectTransform>();
        rootRect.anchorMin        = new Vector2(1f, 1f);
        rootRect.anchorMax        = new Vector2(1f, 1f);
        rootRect.pivot            = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-30f, -24f);
        rootRect.sizeDelta        = new Vector2(310f, 128f);
=======

        RectTransform rootRect = _onlineHudRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-30f, -24f);
        rootRect.sizeDelta = new Vector2(310f, 128f);
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb

        Image rootImage = _onlineHudRoot.GetComponent<Image>();
        rootImage.color = new Color(0.95f, 0.90f, 0.78f, 0.96f);

        Outline outline = _onlineHudRoot.AddComponent<Outline>();
<<<<<<< HEAD
        outline.effectColor    = new Color(0.66f, 0.28f, 0.15f, 0.85f);
        outline.effectDistance = new Vector2(4f, -4f);

        _scorePanelText = CreateOverlayText(_onlineHudRoot.transform, "ScorePanelText", font, 24, FontStyle.Bold,
            new Vector2(0f, 18f), new Vector2(260f, 70f), TextAnchor.MiddleCenter, new Color(0.48f, 0.18f, 0.13f));

        _scorePopupText = CreateOverlayText(_onlineHudRoot.transform, "ScorePopupText", font, 22, FontStyle.Bold,
            new Vector2(0f, -34f), new Vector2(260f, 34f), TextAnchor.MiddleCenter, new Color(0.56f, 0.71f, 0.42f));
=======
        outline.effectColor = new Color(0.66f, 0.28f, 0.15f, 0.85f);
        outline.effectDistance = new Vector2(4f, -4f);

        _scorePanelText = CreateOverlayText(
            _onlineHudRoot.transform,
            "ScorePanelText",
            font,
            24,
            FontStyle.Bold,
            new Vector2(0f, 18f),
            new Vector2(260f, 70f),
            TextAnchor.MiddleCenter,
            new Color(0.48f, 0.18f, 0.13f));

        _scorePopupText = CreateOverlayText(
            _onlineHudRoot.transform,
            "ScorePopupText",
            font,
            22,
            FontStyle.Bold,
            new Vector2(0f, -34f),
            new Vector2(260f, 34f),
            TextAnchor.MiddleCenter,
            new Color(0.56f, 0.71f, 0.42f));
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb

        _scorePopupText.gameObject.SetActive(false);
    }

    private void EnsureResultOverlay()
    {
<<<<<<< HEAD
        if (_resultOverlay != null) return;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
=======
        if (_resultOverlay != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _resultOverlay = new GameObject("ResultOverlay", typeof(RectTransform), typeof(Image));
        _resultOverlay.transform.SetParent(canvas.transform, false);
<<<<<<< HEAD
        RectTransform overlayRect  = _resultOverlay.GetComponent<RectTransform>();
        overlayRect.anchorMin      = Vector2.zero;
        overlayRect.anchorMax      = Vector2.one;
        overlayRect.offsetMin      = Vector2.zero;
        overlayRect.offsetMax      = Vector2.zero;
        _resultOverlay.GetComponent<Image>().color = new Color(0.18f, 0.12f, 0.08f, 0.82f);

        GameObject card = new GameObject("ResultCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(_resultOverlay.transform, false);
        RectTransform cardRect    = card.GetComponent<RectTransform>();
        cardRect.anchorMin        = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax        = new Vector2(0.5f, 0.5f);
        cardRect.pivot            = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta        = new Vector2(620f, 360f);
        cardRect.anchoredPosition = Vector2.zero;
        card.GetComponent<Image>().color = new Color(0.95f, 0.90f, 0.78f, 0.98f);

        Outline cardOutline       = card.AddComponent<Outline>();
        cardOutline.effectColor   = new Color(0.66f, 0.28f, 0.15f, 0.9f);
        cardOutline.effectDistance = new Vector2(5f, -5f);

        _resultTitleText    = CreateOverlayText(card.transform, "ResultTitle",    font, 44, FontStyle.Bold,   new Vector2(0f,  108f), new Vector2(540f,  60f), TextAnchor.MiddleCenter, new Color(0.48f, 0.18f, 0.13f));
        _resultSubtitleText = CreateOverlayText(card.transform, "ResultSubtitle", font, 24, FontStyle.Bold,   new Vector2(0f,   44f), new Vector2(540f,  40f), TextAnchor.MiddleCenter, new Color(0.44f, 0.35f, 0.23f));
        _resultPointsText   = CreateOverlayText(card.transform, "ResultPoints",   font, 28, FontStyle.Bold,   new Vector2(0f,  -32f), new Vector2(540f,  96f), TextAnchor.MiddleCenter, new Color(0.48f, 0.18f, 0.13f));
        _resultHintText     = CreateOverlayText(card.transform, "ResultHint",     font, 18, FontStyle.Italic, new Vector2(0f, -110f), new Vector2(540f,  40f), TextAnchor.MiddleCenter, new Color(0.44f, 0.35f, 0.23f));

        _resultBackButton    = CreateOverlayButton(card.transform, "BackButton",    font, "Voltar ao menu",    new Vector2(-125f, -165f), out _resultBackButtonText);
        _resultRematchButton = CreateOverlayButton(card.transform, "RematchButton", font, "Jogar novamente",   new Vector2( 125f, -165f), out _resultRematchButtonText);
=======

        RectTransform overlayRect = _resultOverlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = _resultOverlay.GetComponent<Image>();
        overlayImage.color = new Color(0.18f, 0.12f, 0.08f, 0.82f);

        GameObject card = new GameObject("ResultCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(_resultOverlay.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(620f, 360f);
        cardRect.anchoredPosition = Vector2.zero;

        Image cardImage = card.GetComponent<Image>();
        cardImage.color = new Color(0.95f, 0.90f, 0.78f, 0.98f);

        Outline cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(0.66f, 0.28f, 0.15f, 0.9f);
        cardOutline.effectDistance = new Vector2(5f, -5f);

        _resultTitleText = CreateOverlayText(card.transform, "ResultTitle", font, 44, FontStyle.Bold, new Vector2(0f, 108f), new Vector2(540f, 60f), TextAnchor.MiddleCenter, new Color(0.48f, 0.18f, 0.13f));
        _resultSubtitleText = CreateOverlayText(card.transform, "ResultSubtitle", font, 24, FontStyle.Bold, new Vector2(0f, 44f), new Vector2(540f, 40f), TextAnchor.MiddleCenter, new Color(0.44f, 0.35f, 0.23f));
        _resultPointsText = CreateOverlayText(card.transform, "ResultPoints", font, 28, FontStyle.Bold, new Vector2(0f, -32f), new Vector2(540f, 96f), TextAnchor.MiddleCenter, new Color(0.48f, 0.18f, 0.13f));
        _resultHintText = CreateOverlayText(card.transform, "ResultHint", font, 18, FontStyle.Italic, new Vector2(0f, -110f), new Vector2(540f, 40f), TextAnchor.MiddleCenter, new Color(0.44f, 0.35f, 0.23f));

        _resultBackButton = CreateOverlayButton(card.transform, "BackButton", font, "Voltar ao menu", new Vector2(-125f, -165f), out _resultBackButtonText);
        _resultRematchButton = CreateOverlayButton(card.transform, "RematchButton", font, "Jogar novamente", new Vector2(125f, -165f), out _resultRematchButtonText);
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        _resultBackButton.onClick.AddListener(HandleResultBackButtonPressed);
        _resultRematchButton.onClick.AddListener(HandleResultRematchButtonPressed);

        _resultOverlay.SetActive(false);
    }

    private Text CreateOverlayText(Transform parent, string objectName, Font font, int fontSize, FontStyle fontStyle, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
<<<<<<< HEAD
        RectTransform rect    = textObject.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta        = size;
        Text text             = textObject.GetComponent<Text>();
        text.font             = font;
        text.fontSize         = fontSize;
        text.fontStyle        = fontStyle;
        text.alignment        = alignment;
        text.color            = color;
        text.supportRichText  = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow   = VerticalWrapMode.Overflow;
=======

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        return text;
    }

    private Button CreateOverlayButton(Transform parent, string objectName, Font font, string label, Vector2 anchoredPosition, out Text labelText)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
<<<<<<< HEAD
        RectTransform rect    = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta        = new Vector2(210f, 54f);

        Image image  = buttonObject.GetComponent<Image>();
        image.color  = new Color(0.88f, 0.80f, 0.57f, 1f);
        Outline outline       = buttonObject.AddComponent<Outline>();
        outline.effectColor   = new Color(0.66f, 0.28f, 0.15f, 0.85f);
=======

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(210f, 54f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.88f, 0.80f, 0.57f, 1f);

        Outline outline = buttonObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.66f, 0.28f, 0.15f, 0.85f);
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        outline.effectDistance = new Vector2(3f, -3f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
<<<<<<< HEAD
        ColorBlock colors    = button.colors;
        colors.normalColor      = new Color(0.88f, 0.80f, 0.57f, 1f);
        colors.highlightedColor = new Color(0.96f, 0.90f, 0.75f, 1f);
        colors.pressedColor     = new Color(0.79f, 0.70f, 0.48f, 1f);
        colors.disabledColor    = new Color(0.77f, 0.73f, 0.62f, 1f);
        colors.colorMultiplier  = 1f;
        colors.fadeDuration     = 0.1f;
        button.colors           = colors;

        labelText = CreateOverlayText(buttonObject.transform, objectName + "Text", font, 22, FontStyle.Bold,
            Vector2.zero, new Vector2(190f, 30f), TextAnchor.MiddleCenter, new Color(0.48f, 0.18f, 0.13f));
=======
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.88f, 0.80f, 0.57f, 1f);
        colors.highlightedColor = new Color(0.96f, 0.90f, 0.75f, 1f);
        colors.pressedColor = new Color(0.79f, 0.70f, 0.48f, 1f);
        colors.disabledColor = new Color(0.77f, 0.73f, 0.62f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;

        labelText = CreateOverlayText(
            buttonObject.transform,
            objectName + "Text",
            font,
            22,
            FontStyle.Bold,
            Vector2.zero,
            new Vector2(190f, 30f),
            TextAnchor.MiddleCenter,
            new Color(0.48f, 0.18f, 0.13f));

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        labelText.text = label;
        return button;
    }

<<<<<<< HEAD
    private void HideResultOverlay() { if (_resultOverlay != null) _resultOverlay.SetActive(false); }
=======
    private void HideResultOverlay()
    {
        if (_resultOverlay != null)
            _resultOverlay.SetActive(false);
    }
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb

    private void ShowOnlineResultOverlay(int scorePlayerOne, int scorePlayerTwo, int localPlayerSlot, int winnerSlot, string statusMessage)
    {
        EnsureResultOverlay();
<<<<<<< HEAD
        if (_resultOverlay == null || (_onlineResultDisplayed && _resultOverlay.activeSelf)) return;

        int localScore    = localPlayerSlot == 1 ? scorePlayerTwo : scorePlayerOne;
        int opponentScore = localPlayerSlot == 1 ? scorePlayerOne : scorePlayerTwo;

        string title = winnerSlot == -1 ? "Empate" :
                       localPlayerSlot < 0 ? "Partida encerrada" :
                       localPlayerSlot == winnerSlot ? "Voce ganhou!" : "Voce perdeu";

        _resultTitleText.text    = title;
        _resultSubtitleText.text = statusMessage;
        _resultPointsText.text   = "Seus pontos: " + localScore + "\nOponente: " + opponentScore;
        _resultHintText.text     = "Escolha o proximo passo.";
        _resultBackButtonText.text   = "Voltar ao menu";
        _resultRematchButtonText.text = "Jogar novamente";
        _resultBackButton.interactable    = true;
=======
        if (_resultOverlay == null)
            return;

        if (_onlineResultDisplayed && _resultOverlay.activeSelf)
            return;

        int localScore = localPlayerSlot == 1 ? scorePlayerTwo : scorePlayerOne;
        int opponentScore = localPlayerSlot == 1 ? scorePlayerOne : scorePlayerTwo;

        string title;
        if (winnerSlot == -1)
            title = "Empate";
        else if (localPlayerSlot < 0)
            title = "Partida encerrada";
        else if (localPlayerSlot >= 0 && localPlayerSlot == winnerSlot)
            title = "Voce ganhou!";
        else
            title = "Voce perdeu";

        _resultTitleText.text = title;
        _resultSubtitleText.text = statusMessage;
        _resultPointsText.text = "Seus pontos: " + localScore + "\nOponente: " + opponentScore;
        _resultHintText.text = "Escolha o proximo passo.";
        _resultBackButtonText.text = "Voltar ao menu";
        _resultRematchButtonText.text = "Jogar novamente";
        _resultBackButton.interactable = true;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        _resultRematchButton.interactable = true;

        _resultOverlay.SetActive(true);
        _onlineResultDisplayed = true;
    }

    private void UpdateScorePanel(int scorePlayerOne, int scorePlayerTwo, int localPlayerSlot)
    {
        EnsureOnlineHud();
<<<<<<< HEAD
        if (_scorePanelText == null) return;
        if      (localPlayerSlot == 0) _scorePanelText.text = "Seus pontos: " + scorePlayerOne + "\nOponente: " + scorePlayerTwo;
        else if (localPlayerSlot == 1) _scorePanelText.text = "Seus pontos: " + scorePlayerTwo + "\nOponente: " + scorePlayerOne;
        else                           _scorePanelText.text = "Jogador 1: " + scorePlayerOne + "\nJogador 2: " + scorePlayerTwo;
=======
        if (_scorePanelText == null)
            return;

        if (localPlayerSlot == 0)
            _scorePanelText.text = "Seus pontos: " + scorePlayerOne + "\nOponente: " + scorePlayerTwo;
        else if (localPlayerSlot == 1)
            _scorePanelText.text = "Seus pontos: " + scorePlayerTwo + "\nOponente: " + scorePlayerOne;
        else
            _scorePanelText.text = "Jogador 1: " + scorePlayerOne + "\nJogador 2: " + scorePlayerTwo;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
    }

    private void HandleOnlineScorePopup(int scorePlayerOne, int scorePlayerTwo, int localPlayerSlot, int lastScoreEventSlot, int lastPointsEarned, int lastComboValue)
    {
<<<<<<< HEAD
        if (lastScoreEventSlot < 0 || lastPointsEarned <= 0) return;
        string signature = scorePlayerOne + ":" + scorePlayerTwo + ":" + lastScoreEventSlot + ":" + lastPointsEarned + ":" + lastComboValue;
        if (_lastScoreEventSignature == signature) return;
        _lastScoreEventSignature = signature;
        string message = localPlayerSlot == lastScoreEventSlot
            ? "+" + lastPointsEarned + " ponto(s)  |  Combo x" + Mathf.Max(1, lastComboValue)
            : "Oponente +" + lastPointsEarned + "  |  Combo x" + Mathf.Max(1, lastComboValue);
        ShowScorePopup(message, localPlayerSlot == lastScoreEventSlot ? new Color(0.56f, 0.71f, 0.42f) : new Color(0.75f, 0.38f, 0.30f));
=======
        if (lastScoreEventSlot < 0 || lastPointsEarned <= 0)
            return;

        string signature = scorePlayerOne + ":" + scorePlayerTwo + ":" + lastScoreEventSlot + ":" + lastPointsEarned + ":" + lastComboValue;
        if (_lastScoreEventSignature == signature)
            return;

        _lastScoreEventSignature = signature;

        string message = localPlayerSlot == lastScoreEventSlot
            ? "+" + lastPointsEarned + " ponto(s)  |  Combo x" + Mathf.Max(1, lastComboValue)
            : "Oponente +" + lastPointsEarned + "  |  Combo x" + Mathf.Max(1, lastComboValue);

        ShowScorePopup(message, localPlayerSlot == lastScoreEventSlot
            ? new Color(0.56f, 0.71f, 0.42f)
            : new Color(0.75f, 0.38f, 0.30f));
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
    }

    private void ShowScorePopup(string message, Color color)
    {
        EnsureOnlineHud();
<<<<<<< HEAD
        if (_scorePopupText == null) return;
        if (_scorePopupRoutine != null) StopCoroutine(_scorePopupRoutine);
=======
        if (_scorePopupText == null)
            return;

        if (_scorePopupRoutine != null)
            StopCoroutine(_scorePopupRoutine);

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        _scorePopupRoutine = StartCoroutine(PlayScorePopup(message, color));
    }

    private IEnumerator PlayScorePopup(string message, Color color)
    {
<<<<<<< HEAD
        _scorePopupText.text  = message;
        _scorePopupText.color = color;
        _scorePopupText.gameObject.SetActive(true);

        RectTransform popupRect  = _scorePopupText.rectTransform;
        Vector2 startPosition    = new Vector2(0f, -34f);
        Vector2 endPosition      = new Vector2(0f, -56f);
        popupRect.anchoredPosition = startPosition;

        float duration = 1.3f, elapsed = 0f;
=======
        _scorePopupText.text = message;
        _scorePopupText.color = color;
        _scorePopupText.gameObject.SetActive(true);

        RectTransform popupRect = _scorePopupText.rectTransform;
        Vector2 startPosition = new Vector2(0f, -34f);
        Vector2 endPosition = new Vector2(0f, -56f);
        popupRect.anchoredPosition = startPosition;

        float duration = 1.3f;
        float elapsed = 0f;

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            popupRect.anchoredPosition = Vector2.Lerp(startPosition, endPosition, t);
<<<<<<< HEAD
            Color frameColor = color; frameColor.a = 1f - t;
=======
            Color frameColor = color;
            frameColor.a = 1f - t;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
            _scorePopupText.color = frameColor;
            yield return null;
        }

        ClearScorePopup();
        _scorePopupRoutine = null;
    }

    private void ClearScorePopup()
    {
<<<<<<< HEAD
        if (_scorePopupRoutine != null) { StopCoroutine(_scorePopupRoutine); _scorePopupRoutine = null; }
=======
        if (_scorePopupRoutine != null)
        {
            StopCoroutine(_scorePopupRoutine);
            _scorePopupRoutine = null;
        }

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        if (_scorePopupText != null)
        {
            _scorePopupText.gameObject.SetActive(false);
            _scorePopupText.rectTransform.anchoredPosition = new Vector2(0f, -34f);
        }
    }

    private void HandleResultBackButtonPressed()
    {
        if (_onlineMode && RelayMatchController.Instance.SessionRunning)
        {
<<<<<<< HEAD
            _resultBackButton.interactable    = false;
=======
            _resultBackButton.interactable = false;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
            _resultRematchButton.interactable = false;
            _resultHintText.text = "Saindo da sala...";
            RelayMatchController.Instance.LeaveMatchAndReturnToMenu("Partida encerrada.");
            return;
        }
<<<<<<< HEAD
=======

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        SceneManager.LoadScene(SceneIds.MainMenu);
    }

    private void HandleResultRematchButtonPressed()
    {
        if (_onlineMode && RelayMatchController.Instance.SessionRunning)
        {
<<<<<<< HEAD
            _resultBackButton.interactable    = false;
=======
            _resultBackButton.interactable = false;
>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
            _resultRematchButton.interactable = false;
            _resultHintText.text = "Solicitando nova partida...";
            RelayMatchController.Instance.RequestRematch();
            return;
        }
<<<<<<< HEAD
=======

>>>>>>> c42ab4f55a7d5622566b6b5a989db3c4fcd647eb
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
