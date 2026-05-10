using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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
    private const int OnlineCardsCount = 12;
    private const int OnlineColumns = 6;
    private static float _lastOnlineIntroStartRealtime = -100f;

    private static readonly string[] OnlineOnlyHiddenSpriteNames =
    {
        "julia",
        "sato",
        "doutora",
        "balao",
        "balao2",
        "progresso",
        "barra",
        "reserva",
        "vaso",
        "fundo1",
        "fundo_principal",
        "inicial"
    };

    // -------------------------------------------------------
    //  SEÇÃO 1 — Campos originais (não alterados)
    // -------------------------------------------------------
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

    [Header("Editor Preview")]
    public bool editorPreview = false;
    public bool autoRefreshPreview = true;

    public EducationalInfo educationalInfo;

    public Sprite[] cardFace;
    public Sprite cardBack;
    public GameObject[] cards;
    public Text matchText;
    public Text launchModeText;

    public AudioClip successSound;
    public AudioClip errorSound;
    private AudioSource audioSource;
    public AudioClip backgroundMusic;
    private AudioSource musicSource;
    private AudioSource voiceSource;

    private bool _init = false;
    private int _matches = 0;
    private bool _previewRunning = false;
    private bool _onlineMode = false;
    private bool _onlineConfigured = false;
    private bool _onlineResultDisplayed = false;
    private string _lastScoreEventSignature = "";
    private string _lastOnlineAudioFeedbackSignature = "";
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
    private GameObject _onlineBackgroundRoot;
    private GameObject _onlineInfoPanel;
    private RectTransform _onlineLightLayer;
    private RectTransform _onlineMistLayer;
    private Text _scorePanelText;
    private Text _scorePopupText;
    private Text _onlineRoomText;
    private Text _onlinePasswordText;
    private Text _onlinePlayerText;
    private Text _onlineTurnText;
    private Text _onlineStatusText;
    private GameObject _onlineIntroOverlay;
    private Text _onlineIntroText;
    private AudioClip _introPartidaClip;
    private AudioClip _introUmClip;
    private AudioClip _introDoisClip;
    private AudioClip _introTresClip;
    private AudioClip _introVaiClip;
    private Coroutine _scorePopupRoutine;
    private Coroutine _onlineIntroRoutine;
    private bool _onlineIntroShown;
    private bool _onlineIntroRunning;
    private bool _lastOnlineCanPlay;

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
        ApplyOnlineBoardTuning();

        EnsureCardsAreReady();

        StopPersistentMenuMusic();
        ConfigureAudioSources();
        PlayBackgroundMusicOnce();

        _matches = 0;
        if (matchText != null)
            matchText.text = "Acertos: 0";

        UpdateLaunchModeText();

        if (_onlineMode)
            ConfigureOnlineMode(true);

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

    private void ApplyOnlineBoardTuning()
    {
        if (!_onlineMode)
            return;

        generatedCardsCount = OnlineCardsCount;
        columns = OnlineColumns;
        _totalPairs = generatedCardsCount / 2;

        cellSize = new Vector2(114f, 120f);
        spacing = new Vector2(16f, 18f);
        cardsScale = new Vector3(1.38f, 1.38f, 1f);
        previewSeconds = 1.45f;
    }

    // -------------------------------------------------------
    //  NOVO: Inicia o timer
    // -------------------------------------------------------
    private void StartTimer()
    {
        _timerRunning = true;
        UpdateTimerDisplay();
    }

    private void StopPersistentMenuMusic()
    {
        MusicPlayer[] persistentMusicPlayers = FindObjectsOfType<MusicPlayer>();
        foreach (MusicPlayer persistentMusic in persistentMusicPlayers)
        {
            if (persistentMusic == null)
                continue;

            AudioSource[] persistentSources = persistentMusic.GetComponents<AudioSource>();
            foreach (AudioSource persistentSource in persistentSources)
            {
                if (persistentSource != null)
                    persistentSource.Stop();
            }
        }
    }

    private void ConfigureAudioSources()
    {
        AudioSource[] existingSources = GetComponents<AudioSource>();

        audioSource = existingSources.Length > 0 ? existingSources[0] : gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.volume = 0.42f;

        voiceSource = existingSources.Length > 1 ? existingSources[1] : gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.volume = 0.68f;
        voiceSource.pitch = 1.22f;

        musicSource = existingSources.Length > 2 ? existingSources[2] : gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0.045f;

        for (int i = 3; i < existingSources.Length; i++)
        {
            if (existingSources[i] == null)
                continue;

            existingSources[i].Stop();
            existingSources[i].enabled = false;
            existingSources[i].playOnAwake = false;
        }
    }

    private void PlayBackgroundMusicOnce()
    {
        if (backgroundMusic == null || musicSource == null)
            return;

        AudioSource[] allSources = FindObjectsOfType<AudioSource>();
        foreach (AudioSource source in allSources)
        {
            if (source == null || source == musicSource)
                continue;

            if (source.clip == backgroundMusic && source.isPlaying)
                source.Stop();
        }

        musicSource.clip = backgroundMusic;
        if (!musicSource.isPlaying)
            musicSource.Play();
    }

    // -------------------------------------------------------
    //  Update — original + timer novo
    // -------------------------------------------------------
    private void Update()
    {
        if (!_onlineMode && !_init && !_waitingForDialogue)
            InitializeCards();

        if (_onlineMode)
            AnimateOnlineBackground();

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

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_onlineMode && RelayMatchController.Instance.SessionRunning)
                RelayMatchController.Instance.LeaveMatchAndReturnToMenu("Partida encerrada.");
            else
                SceneManager.LoadScene(SceneIds.MainMenu);
        }
    }

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
    public void OnCardClicked(int index)
    {
        if (_onlineMode)
        {
            if (Card.DO_NOT)
                return;

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

        // evita clicar em carta já removida
        if (cards[index].transform.localScale == Vector3.zero) return;

        // se já está aberta, ignora para evitar toggle indevido
        if (card.state == 1) return;

        card.flipCard();
        CheckCards();
    }

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
                audioSource.PlayOneShot(successSound, 0.45f);

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
                audioSource.PlayOneShot(errorSound, 0.45f);

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

        if (cards != null && cards.Length == generatedCardsCount && cardsContainer != null && cardsContainer.childCount == generatedCardsCount)
            return;

        if (cardPrefab == null || cardsContainer == null)
        {
            Debug.LogWarning("Auto geração ativa, mas cardPrefab/cardsContainer não foram configurados no GameManager.");
            return;
        }

        if (generatedCardsCount < 2)
            generatedCardsCount = 2;

        if (generatedCardsCount % 2 != 0)
            generatedCardsCount += 1;

        // Unity pode manter valores antigos serializados na cena. Forca um minimo util.
        if (spacing.x < 9.9225f || spacing.y < 9.9225f)
            spacing = new Vector2(9.9225f, 9.9225f);

        if (cellSize.x < 99.225f || cellSize.y < 104.7375f)
            cellSize = new Vector2(99.225f, 104.7375f);

        for (int i = cardsContainer.childCount - 1; i >= 0; i--)
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

        RectTransform containerRect = cardsContainer as RectTransform;
        if (containerRect != null)
        {
            int rows   = Mathf.CeilToInt((float)generatedCardsCount / Mathf.Max(1, columns));
            float width  = (columns * cellSize.x) + ((columns - 1) * spacing.x);
            float height = (rows * cellSize.y) + ((rows - 1) * spacing.y);
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            containerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   height);
        }

        cardsContainer.localScale = cardsScale;
        cards = new GameObject[generatedCardsCount];

        for (int i = 0; i < generatedCardsCount; i++)
        {
            GameObject cardObj = Instantiate(cardPrefab, cardsContainer);
            cardObj.name = "Card_" + i;
            cards[i]     = cardObj;
            cardObj.transform.localScale = Vector3.one;

            RectTransform cardRect = cardObj.GetComponent<RectTransform>();
            if (cardRect != null)
            {
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
                cardRect.localScale = Vector3.one;
            }

            Card card = cardObj.GetComponent<Card>();
            if (card == null)
                card = cardObj.AddComponent<Card>();

            MemoryCardButton memoryCardButton = cardObj.GetComponent<MemoryCardButton>();
            if (memoryCardButton == null)
                memoryCardButton = cardObj.AddComponent<MemoryCardButton>();

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
        ApplyOnlineBoardTuning();
        HideCampaignPresentationForOnlineMode();

        EnsureCardsAreReady();
        EnsureOnlineBackground();
        EnsureOnlineInfoPanel();
        EnsureOnlineIntroOverlay();
        EnsureResultOverlay();
        EnsureOnlineHud();

        if (_onlineConfigured && !resetVisuals)
        {
            UpdateLaunchModeText();
            return;
        }

        _init = true;
        _previewRunning = false;
        _matches = 0;
        _onlineResultDisplayed = false;
        _onlineIntroShown = false;
        _onlineIntroRunning = false;
        _lastOnlineCanPlay = false;
        _lastScoreEventSignature = "";
        _lastOnlineAudioFeedbackSignature = "";
        HideResultOverlay();
        ClearScorePopup();

        if (matchText != null)
            matchText.text = "Aguardando dados da partida...";

        if (_scorePanelText != null)
            _scorePanelText.text = "Pontos: 0\nOponente: 0";

        UpdateOnlineInfoPanel("Conectando...", -1, -1, true, false, false);
        if (launchModeText != null)
            launchModeText.gameObject.SetActive(false);

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
            card.ResetOnlinePresentation(cardBack);
        }

        _onlineConfigured = true;
        UpdateLaunchModeText();
    }

    private void HideCampaignPresentationForOnlineMode()
    {
        HideCampaignImagesForOnlineMode();
        HideCampaignSpritesForOnlineMode();

        if (educationalPanel != null)
            educationalPanel.SetActive(false);

        if (mudOverlay != null)
        {
            Color color = mudOverlay.color;
            color.a = 0f;
            mudOverlay.color = color;
        }
    }

    private void HideCampaignImagesForOnlineMode()
    {
        Image[] images = FindObjectsOfType<Image>(true);
        foreach (Image image in images)
        {
            if (image == null || image.sprite == null)
                continue;

            if (ShouldHideInOnlineMode(image.sprite.name))
                image.gameObject.SetActive(false);
        }
    }

    private void HideCampaignSpritesForOnlineMode()
    {
        SpriteRenderer[] spriteRenderers = FindObjectsOfType<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
                continue;

            if (ShouldHideInOnlineMode(spriteRenderer.sprite.name) || ShouldHideInOnlineMode(spriteRenderer.gameObject.name))
                spriteRenderer.gameObject.SetActive(false);
        }
    }

    private bool ShouldHideInOnlineMode(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        string normalized = value.ToLowerInvariant();
        foreach (string hiddenName in OnlineOnlyHiddenSpriteNames)
        {
            if (normalized.Contains(hiddenName))
                return true;
        }

        return false;
    }

    public void ApplyOnlineSnapshot(
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
        ApplyOnlineBoardTuning();
        EnsureOnlineBackground();
        EnsureOnlineInfoPanel();
        EnsureOnlineIntroOverlay();
        EnsureCardsAreReady();
        _init = true;
        _previewRunning = previewRunning;
        _matches = scorePlayerOne + scorePlayerTwo;

        if (cards == null || cardValues == null || cardStates == null)
            return;

        int total = Mathf.Min(cards.Length, Mathf.Min(cardValues.Length, cardStates.Length));
        for (int i = 0; i < total; i++)
        {
            if (cards[i] == null)
                continue;

            Card card = cards[i].GetComponent<Card>();
            if (card == null)
                continue;

            int cardValue = Mathf.Max(1, cardValues[i]);
            card.initialized = true;
            card.cardValue = cardValue;
            card.matched = cardStates[i] == 2;
            card.ApplyOnlineVisual(getCardFace(cardValue), cardBack, cardStates[i]);
        }

        bool localCanPlay = !waitingForOpponent && !previewRunning && !gameOver && localPlayerSlot >= 0 && currentTurnSlot == localPlayerSlot;
        _lastOnlineCanPlay = localCanPlay;
        Card.DO_NOT = _onlineIntroRunning || !localCanPlay;

        if (matchText != null)
        {
            if (localPlayerSlot == 0)
                matchText.text = "Pontos: " + scorePlayerOne + "  |  Oponente: " + scorePlayerTwo;
            else if (localPlayerSlot == 1)
                matchText.text = "Pontos: " + scorePlayerTwo + "  |  Oponente: " + scorePlayerOne;
            else
                matchText.text = "Jogador 1: " + scorePlayerOne + " pts  |  Jogador 2: " + scorePlayerTwo + " pts";
        }

        UpdateScorePanel(scorePlayerOne, scorePlayerTwo, localPlayerSlot);
        UpdateOnlineInfoPanel(statusMessage, localPlayerSlot, currentTurnSlot, waitingForOpponent, previewRunning, gameOver);
        TryStartOnlineIntro(statusMessage, waitingForOpponent, previewRunning, gameOver);
        HandleOnlineAudioFeedback(statusMessage, scorePlayerOne, scorePlayerTwo, currentTurnSlot, previewRunning, gameOver);
        HandleOnlineScorePopup(scorePlayerOne, scorePlayerTwo, localPlayerSlot, lastScoreEventSlot, lastPointsEarned, lastComboValue);

        if (launchModeText == null)
            launchModeText = CreateLaunchModeText();

        if (launchModeText != null)
        {
            string localPlayerText = localPlayerSlot >= 0 ? "Jogador " + (localPlayerSlot + 1) : "Conectando...";
            string turnText = waitingForOpponent ? "Aguardando o segundo jogador" :
                gameOver ? "Partida encerrada" :
                previewRunning ? "Memorize as cartas" :
                currentTurnSlot == localPlayerSlot ? "Sua vez" : "Vez do oponente";

            launchModeText.text = "Sala: " + GameLaunchConfig.RoomCode + "  |  " + localPlayerText + "\n" + turnText + "\n" + statusMessage;
            launchModeText.gameObject.SetActive(false);
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

        Card.DO_NOT = true;
        _previewRunning = true;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            cards[i].SetActive(true);
            cards[i].transform.localScale = Vector3.one;
            Card card = cards[i].GetComponent<Card>();
            if (card != null)
            {
                card.initialized = false;
                card.matched = false;
                card.state = 0;
            }
        }

        for (int id = 0; id < 2; id++)
        {
            for (int i = 1; i <= (cards.Length / 2); i++)
            {
                bool test = false;
                int choice = 0;
                while (!test)
                {
                    choice = Random.Range(0, cards.Length);
                    Card choiceCard = cards[choice].GetComponent<Card>();
                    test = choiceCard != null && !choiceCard.initialized;
                }

                Card selected = cards[choice].GetComponent<Card>();
                selected.cardValue = i;
                selected.initialized = true;
            }
        }

        foreach (GameObject c in cards)
        {
            if (c == null) continue;
            Card card = c.GetComponent<Card>();
            if (card != null)
            {
                card.setupGraphics();
                card.PrepareForAppear();
            }
        }

        _init = true;
        StartCoroutine(HidePreviewCardsAfterDelay());
        StartCoroutine(PlayAppearSequence());
    }

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
    }

    private void CheckCards()
    {
        List<int> c = new List<int>();
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null || !cards[i].activeSelf) continue;
            Card card = cards[i].GetComponent<Card>();
            if (card != null && !card.matched && card.state == 1)
                c.Add(i);
        }

        if (c.Count == 2)
            CardComparison(c);
    }

   

    private IEnumerator HideInfoAfterDelay()
    {
        yield return new WaitForSeconds(1f);
        if (educationalInfo != null)
            educationalInfo.HideInfo();
    }

    private IEnumerator HideMatchedCardsAfterDelay(List<int> c)
    {
        yield return new WaitForSeconds(matchedDisappearDelaySeconds);

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

        Card.DO_NOT = false;
    }

    private IEnumerator HidePreviewCardsAfterDelay()
    {
        float totalDelay = previewSeconds + Mathf.Max(0f, (cards.Length - 1) * appearStaggerSeconds);
        yield return new WaitForSeconds(totalDelay);

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;

            Card card = cards[i].GetComponent<Card>();
            if (card == null) continue;

            card.ShowBack();
        }

        _previewRunning = false;
        Card.DO_NOT = false;
    }

    private IEnumerator PlayAppearSequence()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;

            Card card = cards[i].GetComponent<Card>();
            if (card != null)
                StartCoroutine(card.PlayAppear(i * appearStaggerSeconds));
        }

        yield return null;
    }

    private IEnumerator LoadNextSceneAfterDelay(string currentScene, float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadNextScene(currentScene);
    }

    private void LoadNextScene(string currentScene)
    {
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
    }

    private void UpdateLaunchModeText()
    {
        if (launchModeText == null)
            launchModeText = CreateLaunchModeText();

        if (launchModeText == null)
            return;

        if (_onlineMode)
        {
            launchModeText.gameObject.SetActive(false);
            return;
        }

        switch (GameLaunchConfig.CurrentMode)
        {
            case GameLaunchMode.CreateMatch:
                launchModeText.text = "Modo: Criar Partida  |  Codigo: " + GameLaunchConfig.RoomCode;
                break;
            case GameLaunchMode.JoinMatch:
                launchModeText.text = "Modo: Encontrar Partida  |  Codigo: " + GameLaunchConfig.RoomCode;
                break;
            default:
                launchModeText.text = "Modo: Historia  |  Capitulo: " + GameLaunchConfig.StoryChapter;
                break;
        }
    }

    private Text CreateLaunchModeText()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
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
        text.supportRichText = false;
        return text;
    }

    private void EnsureOnlineBackground()
    {
        if (_onlineBackgroundRoot != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        _onlineBackgroundRoot = new GameObject("OnlineForestBackground", typeof(RectTransform), typeof(Image));
        _onlineBackgroundRoot.transform.SetParent(canvas.transform, false);

        RectTransform rect = _onlineBackgroundRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = _onlineBackgroundRoot.GetComponent<Image>();
        image.sprite = CreateOnlineForestSprite();
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;

        _onlineMistLayer = CreateOnlineBackgroundLayer(
            _onlineBackgroundRoot.transform,
            "OnlineForestMist",
            CreateSoftBandSprite(new Color(0.72f, 0.90f, 0.70f, 0.10f), new Color(1f, 1f, 1f, 0f)),
            new Color(1f, 1f, 1f, 0.42f),
            new Vector2(2200f, 980f));

        _onlineLightLayer = CreateOnlineBackgroundLayer(
            _onlineBackgroundRoot.transform,
            "OnlineForestLight",
            CreateSoftBandSprite(new Color(0.90f, 0.82f, 0.42f, 0.18f), new Color(1f, 1f, 1f, 0f)),
            new Color(1f, 1f, 1f, 0.34f),
            new Vector2(2300f, 980f));

        if (cardsContainer != null && cardsContainer.parent == canvas.transform)
            _onlineBackgroundRoot.transform.SetSiblingIndex(Mathf.Max(0, cardsContainer.GetSiblingIndex()));
        else
            _onlineBackgroundRoot.transform.SetAsFirstSibling();
    }

    private RectTransform CreateOnlineBackgroundLayer(Transform parent, string objectName, Sprite sprite, Color color, Vector2 size)
    {
        GameObject layer = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        layer.transform.SetParent(parent, false);

        RectTransform rect = layer.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        Image image = layer.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private Sprite CreateSoftBandSprite(Color bandColor, Color clearColor)
    {
        const int width = 256;
        const int height = 128;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "OnlineSoftBandTexture";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float diagonal = Mathf.Repeat((x * 0.8f + y * 1.35f) / 96f, 1f);
                float band = Mathf.Clamp01(1f - Mathf.Abs(diagonal - 0.5f) * 5.5f);
                band *= band;
                Color color = Color.Lerp(clearColor, bandColor, band);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void AnimateOnlineBackground()
    {
        if (_onlineMistLayer != null)
        {
            float x = Mathf.Repeat(Time.time * 18f, 420f) - 210f;
            float y = Mathf.Sin(Time.time * 0.35f) * 12f;
            _onlineMistLayer.anchoredPosition = new Vector2(x, y);
        }

        if (_onlineLightLayer != null)
        {
            float x = 210f - Mathf.Repeat(Time.time * 28f, 420f);
            float y = Mathf.Sin(Time.time * 0.48f + 1.2f) * 16f;
            _onlineLightLayer.anchoredPosition = new Vector2(x, y);
        }
    }

    private Sprite CreateOnlineForestSprite()
    {
        const int width = 512;
        const int height = 288;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "OnlineForestBackgroundTexture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color top = new Color(0.18f, 0.34f, 0.25f, 1f);
        Color middle = new Color(0.30f, 0.58f, 0.34f, 1f);
        Color bottom = new Color(0.12f, 0.38f, 0.35f, 1f);
        Color river = new Color(0.16f, 0.46f, 0.48f, 1f);
        Color light = new Color(0.67f, 0.78f, 0.48f, 1f);
        Color shadow = new Color(0.08f, 0.22f, 0.16f, 1f);

        for (int y = 0; y < height; y++)
        {
            float vertical = (float)y / (height - 1);
            Color baseColor = vertical < 0.58f
                ? Color.Lerp(bottom, middle, vertical / 0.58f)
                : Color.Lerp(middle, top, (vertical - 0.58f) / 0.42f);

            for (int x = 0; x < width; x++)
            {
                float horizontal = (float)x / (width - 1);
                float riverMask = Mathf.Clamp01(1f - Mathf.Abs(vertical - (0.22f + Mathf.Sin(horizontal * Mathf.PI * 2f) * 0.035f)) * 7f);
                Color color = Color.Lerp(baseColor, river, riverMask * 0.38f);

                float vignette = Mathf.Clamp01(Mathf.Abs(horizontal - 0.5f) * 1.5f + Mathf.Abs(vertical - 0.48f) * 0.45f);
                color = Color.Lerp(color, shadow, vignette * 0.22f);

                if (y > height * 0.58f && ((x / 18) % 3 == 0 || (x / 27) % 5 == 0))
                    color = Color.Lerp(color, shadow, 0.18f);

                if (y > height * 0.72f && Mathf.Abs(Mathf.Sin((x + y) * 0.055f)) > 0.92f)
                    color = Color.Lerp(color, light, 0.16f);

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private void EnsureOnlineInfoPanel()
    {
        if (_onlineInfoPanel != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _onlineInfoPanel = new GameObject("OnlineRoomInfoPanel", typeof(RectTransform), typeof(Image));
        _onlineInfoPanel.transform.SetParent(canvas.transform, false);
        _onlineInfoPanel.transform.SetAsLastSibling();

        RectTransform rect = _onlineInfoPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(330f, 124f);

        Image panelImage = _onlineInfoPanel.GetComponent<Image>();
        panelImage.color = new Color(0.95f, 0.90f, 0.78f, 0.96f);
        panelImage.raycastTarget = false;

        Outline outline = _onlineInfoPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.66f, 0.28f, 0.15f, 0.85f);
        outline.effectDistance = new Vector2(4f, -4f);

        Color titleColor = new Color(0.48f, 0.18f, 0.13f);
        Color bodyColor = new Color(0.39f, 0.20f, 0.13f);

        _onlineRoomText = CreateOverlayText(_onlineInfoPanel.transform, "RoomLabel", font, 17, FontStyle.Bold, new Vector2(-4f, 38f), new Vector2(278f, 24f), TextAnchor.MiddleCenter, titleColor);
        _onlinePasswordText = CreateOverlayText(_onlineInfoPanel.transform, "PasswordLabel", font, 22, FontStyle.Bold, new Vector2(-4f, 10f), new Vector2(278f, 28f), TextAnchor.MiddleCenter, titleColor);
        _onlinePlayerText = CreateOverlayText(_onlineInfoPanel.transform, "PlayerLabel", font, 18, FontStyle.Bold, new Vector2(-4f, -19f), new Vector2(278f, 24f), TextAnchor.MiddleCenter, bodyColor);
        _onlineTurnText = CreateOverlayText(_onlineInfoPanel.transform, "TurnLabel", font, 18, FontStyle.Bold, new Vector2(-4f, -47f), new Vector2(278f, 24f), TextAnchor.MiddleCenter, bodyColor);
        _onlineStatusText = CreateOverlayText(_onlineInfoPanel.transform, "StatusLabel", font, 13, FontStyle.Normal, new Vector2(-4f, -61f), new Vector2(278f, 16f), TextAnchor.MiddleCenter, new Color(0.45f, 0.28f, 0.18f));
        _onlineStatusText.gameObject.SetActive(false);
    }

    private void UpdateOnlineInfoPanel(string statusMessage, int localPlayerSlot, int currentTurnSlot, bool waitingForOpponent, bool previewRunning, bool gameOver)
    {
        EnsureOnlineInfoPanel();
        if (_onlineInfoPanel == null)
            return;

        string roomCode = string.IsNullOrEmpty(GameLaunchConfig.RoomCode) ? "----" : GameLaunchConfig.RoomCode;
        string playerText = localPlayerSlot >= 0 ? "Jogador " + (localPlayerSlot + 1) : "Conectando";
        string turnText = waitingForOpponent ? "Aguardando oponente" :
            gameOver ? "Partida encerrada" :
            previewRunning ? "Memorize as cartas" :
            currentTurnSlot == localPlayerSlot ? "Sua vez" : "Vez do oponente";

        if (_onlineRoomText != null)
            _onlineRoomText.text = "Sala online";

        if (_onlinePasswordText != null)
            _onlinePasswordText.text = "Senha: " + roomCode;

        if (_onlinePlayerText != null)
            _onlinePlayerText.text = playerText;

        if (_onlineTurnText != null)
            _onlineTurnText.text = "Vez: " + turnText;

        if (_onlineStatusText != null)
            _onlineStatusText.text = string.IsNullOrEmpty(statusMessage) ? "Multiplayer ativo" : statusMessage;
    }

    private void EnsureOnlineIntroOverlay()
    {
        if (_onlineIntroOverlay != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _onlineIntroOverlay = new GameObject("OnlineIntroOverlay", typeof(RectTransform), typeof(Image));
        _onlineIntroOverlay.transform.SetParent(canvas.transform, false);
        _onlineIntroOverlay.transform.SetAsLastSibling();

        RectTransform rect = _onlineIntroOverlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = _onlineIntroOverlay.GetComponent<Image>();
        image.color = new Color(0.06f, 0.14f, 0.10f, 0.42f);
        image.raycastTarget = false;

        _onlineIntroText = CreateOverlayText(
            _onlineIntroOverlay.transform,
            "OnlineIntroText",
            font,
            54,
            FontStyle.Bold,
            Vector2.zero,
            new Vector2(720f, 150f),
            TextAnchor.MiddleCenter,
            new Color(0.98f, 0.90f, 0.66f));

        Outline outline = _onlineIntroText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.39f, 0.16f, 0.10f, 0.95f);
        outline.effectDistance = new Vector2(4f, -4f);

        _onlineIntroOverlay.SetActive(false);
    }

    private void TryStartOnlineIntro(string statusMessage, bool waitingForOpponent, bool previewRunning, bool gameOver)
    {
        if (_onlineIntroShown || _onlineIntroRunning || waitingForOpponent || !previewRunning || gameOver)
            return;

        if (string.IsNullOrEmpty(statusMessage))
            return;

        string normalized = statusMessage.ToLowerInvariant();
        if (!normalized.Contains("partida iniciada"))
            return;

        if (Time.realtimeSinceStartup - _lastOnlineIntroStartRealtime < 6f)
            return;

        _lastOnlineIntroStartRealtime = Time.realtimeSinceStartup;
        _onlineIntroShown = true;
        _onlineIntroRoutine = StartCoroutine(PlayOnlineIntroCountdown());
    }

    private IEnumerator PlayOnlineIntroCountdown()
    {
        EnsureOnlineIntroOverlay();
        if (_onlineIntroOverlay == null || _onlineIntroText == null)
            yield break;

        _onlineIntroRunning = true;
        Card.DO_NOT = true;
        _onlineIntroOverlay.SetActive(true);

        yield return ShowOnlineIntroStep("Partida iniciada", 0.34f, 54, GetIntroPartidaClip());
        yield return ShowOnlineIntroStep("1", 0.16f, 86, GetIntroUmClip());
        yield return ShowOnlineIntroStep("2", 0.16f, 86, GetIntroDoisClip());
        yield return ShowOnlineIntroStep("3", 0.16f, 86, GetIntroTresClip());
        yield return ShowOnlineIntroStep("Vai!", 0.18f, 78, GetIntroVaiClip());

        _onlineIntroOverlay.SetActive(false);
        _onlineIntroRunning = false;
        Card.DO_NOT = !_lastOnlineCanPlay;
        _onlineIntroRoutine = null;
    }

    private AudioClip GetIntroPartidaClip()
    {
        if (_introPartidaClip == null)
            _introPartidaClip = Resources.Load<AudioClip>("Audio/intro_partida_iniciada");

        return _introPartidaClip;
    }

    private AudioClip GetIntroUmClip()
    {
        if (_introUmClip == null)
            _introUmClip = Resources.Load<AudioClip>("Audio/intro_um");

        return _introUmClip;
    }

    private AudioClip GetIntroDoisClip()
    {
        if (_introDoisClip == null)
            _introDoisClip = Resources.Load<AudioClip>("Audio/intro_dois");

        return _introDoisClip;
    }

    private AudioClip GetIntroTresClip()
    {
        if (_introTresClip == null)
            _introTresClip = Resources.Load<AudioClip>("Audio/intro_tres");

        return _introTresClip;
    }

    private AudioClip GetIntroVaiClip()
    {
        if (_introVaiClip == null)
            _introVaiClip = Resources.Load<AudioClip>("Audio/intro_vai");

        return _introVaiClip;
    }

    private IEnumerator ShowOnlineIntroStep(string message, float seconds, int fontSize, AudioClip voiceClip)
    {
        _onlineIntroText.text = message;
        _onlineIntroText.fontSize = fontSize;
        _onlineIntroText.color = new Color(0.98f, 0.90f, 0.66f, 1f);
        _onlineIntroText.rectTransform.localScale = Vector3.one * 0.88f;

        if (voiceSource != null && voiceClip != null)
        {
            voiceSource.Stop();
            voiceSource.clip = voiceClip;
            voiceSource.Play();
            float playbackLength = voiceSource.pitch > 0f ? voiceClip.length / voiceSource.pitch : voiceClip.length;
            seconds = Mathf.Max(seconds, playbackLength + 0.01f);
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            float scale = Mathf.Lerp(0.88f, 1.08f, Mathf.Sin(t * Mathf.PI));
            _onlineIntroText.rectTransform.localScale = Vector3.one * scale;

            Color color = _onlineIntroText.color;
            color.a = Mathf.Lerp(1f, 0.15f, Mathf.Max(0f, (t - 0.72f) / 0.28f));
            _onlineIntroText.color = color;
            yield return null;
        }
    }

    private void EnsureOnlineHud()
    {
        if (_onlineHudRoot != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _onlineHudRoot = new GameObject("OnlineHudRoot", typeof(RectTransform), typeof(Image));
        _onlineHudRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = _onlineHudRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-30f, -24f);
        rootRect.sizeDelta = new Vector2(310f, 128f);

        Image rootImage = _onlineHudRoot.GetComponent<Image>();
        rootImage.color = new Color(0.95f, 0.90f, 0.78f, 0.96f);

        Outline outline = _onlineHudRoot.AddComponent<Outline>();
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

        _scorePopupText.gameObject.SetActive(false);
    }

    private void EnsureResultOverlay()
    {
        if (_resultOverlay != null)
            return;

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        _resultOverlay = new GameObject("ResultOverlay", typeof(RectTransform), typeof(Image));
        _resultOverlay.transform.SetParent(canvas.transform, false);

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
        _resultBackButton.onClick.AddListener(HandleResultBackButtonPressed);
        _resultRematchButton.onClick.AddListener(HandleResultRematchButtonPressed);

        _resultOverlay.SetActive(false);
    }

    private Text CreateOverlayText(Transform parent, string objectName, Font font, int fontSize, FontStyle fontStyle, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

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
        return text;
    }

    private Button CreateOverlayButton(Transform parent, string objectName, Font font, string label, Vector2 anchoredPosition, out Text labelText)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

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
        outline.effectDistance = new Vector2(3f, -3f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
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

        labelText.text = label;
        return button;
    }

    private void HideResultOverlay()
    {
        if (_resultOverlay != null)
            _resultOverlay.SetActive(false);
    }

    private void ShowOnlineResultOverlay(int scorePlayerOne, int scorePlayerTwo, int localPlayerSlot, int winnerSlot, string statusMessage)
    {
        EnsureResultOverlay();
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
        _resultRematchButton.interactable = true;

        _resultOverlay.SetActive(true);
        _onlineResultDisplayed = true;
    }

    private void UpdateScorePanel(int scorePlayerOne, int scorePlayerTwo, int localPlayerSlot)
    {
        EnsureOnlineHud();
        if (_scorePanelText == null)
            return;

        if (localPlayerSlot == 0)
            _scorePanelText.text = "Seus pontos: " + scorePlayerOne + "\nOponente: " + scorePlayerTwo;
        else if (localPlayerSlot == 1)
            _scorePanelText.text = "Seus pontos: " + scorePlayerTwo + "\nOponente: " + scorePlayerOne;
        else
            _scorePanelText.text = "Jogador 1: " + scorePlayerOne + "\nJogador 2: " + scorePlayerTwo;
    }

    private void HandleOnlineScorePopup(int scorePlayerOne, int scorePlayerTwo, int localPlayerSlot, int lastScoreEventSlot, int lastPointsEarned, int lastComboValue)
    {
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
    }

    private void HandleOnlineAudioFeedback(string statusMessage, int scorePlayerOne, int scorePlayerTwo, int currentTurnSlot, bool previewRunning, bool gameOver)
    {
        if (audioSource == null || string.IsNullOrEmpty(statusMessage) || previewRunning || gameOver)
            return;

        string normalized = statusMessage.ToLowerInvariant();
        bool isSuccess = normalized.Contains("par encontrado");
        bool isError = normalized.Contains("nao formou par") || normalized.Contains("não formou par");

        if (!isSuccess && !isError)
            return;

        string signature = scorePlayerOne + ":" + scorePlayerTwo + ":" + currentTurnSlot + ":" + statusMessage;
        if (_lastOnlineAudioFeedbackSignature == signature)
            return;

        _lastOnlineAudioFeedbackSignature = signature;

        AudioClip clip = isSuccess ? successSound : errorSound;
        if (clip != null)
            audioSource.PlayOneShot(clip, 0.45f);
    }

    private void ShowScorePopup(string message, Color color)
    {
        EnsureOnlineHud();
        if (_scorePopupText == null)
            return;

        if (_scorePopupRoutine != null)
            StopCoroutine(_scorePopupRoutine);

        _scorePopupRoutine = StartCoroutine(PlayScorePopup(message, color));
    }

    private IEnumerator PlayScorePopup(string message, Color color)
    {
        _scorePopupText.text = message;
        _scorePopupText.color = color;
        _scorePopupText.gameObject.SetActive(true);

        RectTransform popupRect = _scorePopupText.rectTransform;
        Vector2 startPosition = new Vector2(0f, -34f);
        Vector2 endPosition = new Vector2(0f, -56f);
        popupRect.anchoredPosition = startPosition;

        float duration = 1.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            popupRect.anchoredPosition = Vector2.Lerp(startPosition, endPosition, t);
            Color frameColor = color;
            frameColor.a = 1f - t;
            _scorePopupText.color = frameColor;
            yield return null;
        }

        ClearScorePopup();
        _scorePopupRoutine = null;
    }

    private void ClearScorePopup()
    {
        if (_scorePopupRoutine != null)
        {
            StopCoroutine(_scorePopupRoutine);
            _scorePopupRoutine = null;
        }

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
            _resultBackButton.interactable = false;
            _resultRematchButton.interactable = false;
            _resultHintText.text = "Saindo da sala...";
            RelayMatchController.Instance.LeaveMatchAndReturnToMenu("Partida encerrada.");
            return;
        }
        SceneManager.LoadScene(SceneIds.MainMenu);
    }

    private void HandleResultRematchButtonPressed()
    {
        if (_onlineMode && RelayMatchController.Instance.SessionRunning)
        {
            _resultBackButton.interactable = false;
            _resultRematchButton.interactable = false;
            _resultHintText.text = "Solicitando nova partida...";
            RelayMatchController.Instance.RequestRematch();
            return;
        }
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
