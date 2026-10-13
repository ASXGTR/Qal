using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Explicit aliases to eliminate namespace ambiguity
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using Color = UnityEngine.Color;

public class DealershipModeController : MonoBehaviour
{
    // ============================================================================================================
    //  DEALERSHIP SHOWROOM CONTROLLER — STAGE 1/2 GATING, ENGINE START/STOP & MANUAL OVERRIDE
    // ============================================================================================================

    [Header("Panel Containers")]
    public GameObject dealershipLoginPanel;       // Active by default on scene load
    public GameObject dealershipControlsPanel;    // Unmasked only upon successful login

    [Header("Gatekeeper Login Controls")]
    public TMP_InputField dealerIdInput;
    public TMP_InputField dealerPasswordInput;
    public Button submitLoginButton;
    public Button cancelLoginButton;
    public TextMeshProUGUI loginErrorText;

    [Header("Live Status Displays (Populated Post-Login)")]
    public TextMeshProUGUI immobilizerStatusText;
    public TextMeshProUGUI ignitionStatusText;
    public TextMeshProUGUI demoKeyInfoText;

    [Header("Stage 1 Ignition Simulator Button")]
    public Button stage1IgnitionButton;
    public TextMeshProUGUI stage1IgnitionButtonText;

    [Header("Start / Stop Engine Button")]
    public Button startEngineButton;
    public Image startEngineButtonImage;
    public TextMeshProUGUI startEngineButtonText;
    public Color startButtonDefaultBgColor = Color.white;
    public Color startButtonRunningBgColor = new Color(0f, 1f, 0f);      // #00FF00 Green
    public Color startButtonDefaultTextColor = new Color(0f, 0.65f, 0.2f); // Green text on white button

    [Header("Manual Password Override Modal")]
    public Button openManualOverrideButton;
    public GameObject manualOverrideModalPanel;
    public TMP_InputField manualOverrideInput;
    public Button submitManualOverrideButton;
    public Button closeManualOverrideButton;
    public TextMeshProUGUI manualOverrideErrorText;

    [Header("Showroom Management Buttons")]
    public Button toggleImmobilizerButton;
    public TextMeshProUGUI toggleImmobilizerButtonText;
    public Button resetDemoButton;

    [Header("Status Indicator Visuals (Circle & Button Highlights)")]
    public Image statusCircleImage;
    public Color textDefaultColor = Color.white;
    public Color textActiveColor = new Color(0f, 1f, 0f);               // #00FF00 Green
    public Color circleDefaultColor = new Color(1f, 0f, 0f);            // Red
    public Color circleActiveColor = new Color(0f, 1f, 0f);             // #00FF00 Green

    [Header("Toggle Button Text Colors")]
    public Color disableStateTextColor = new Color(1f, 0.27f, 0.27f);   // Red
    public Color enableStateTextColor = new Color(0f, 1f, 0.46f);       // Green

    [Header("Single-Line Feedback Notice (Auto-Hiding)")]
    public TextMeshProUGUI feedbackNoticeText;
    [SerializeField] private float feedbackDisplayDuration = 3.5f;
    private Coroutine feedbackRoutine;

    [Header("Scene Routing")]
    [SerializeField] private string modeSelectionScene = "ModeSelect";

    private const string SHOWROOM_DEMO_KEY = "DEALER-DEFAULT";
    private string activeDealerId = "";
    private bool isAcousticUnlocked = false;
    private bool isStage1Listening = false;
    private bool isEngineRunning = false;
    private Coroutine stage1ListeningRoutine;

    void Awake()
    {
        // Absolute fail-safe: Ensure no consumer password flag ever lingers on disk
        PurgeAccidentalConsumerFlags();

        if (dealershipLoginPanel != null) dealershipLoginPanel.SetActive(true);
        if (dealershipControlsPanel != null) dealershipControlsPanel.SetActive(false);
        if (manualOverrideModalPanel != null) manualOverrideModalPanel.SetActive(false);
        if (feedbackNoticeText != null) feedbackNoticeText.gameObject.SetActive(false);
        if (loginErrorText != null) loginErrorText.text = "";

        if (startEngineButtonImage == null && startEngineButton != null)
            startEngineButtonImage = startEngineButton.GetComponent<Image>();

        SetIndicatorVisuals(false);
    }

    void Start()
    {
        if (submitLoginButton != null)
        {
            submitLoginButton.onClick.RemoveAllListeners();
            submitLoginButton.onClick.AddListener(OnSubmitLoginPressed);
        }

        if (cancelLoginButton != null)
        {
            cancelLoginButton.onClick.RemoveAllListeners();
            cancelLoginButton.onClick.AddListener(OnCancelLoginPressed);
        }

        if (stage1IgnitionButton != null)
        {
            stage1IgnitionButton.onClick.RemoveAllListeners();
            stage1IgnitionButton.onClick.AddListener(OnStage1IgnitionPressed);
        }

        if (startEngineButton != null)
        {
            startEngineButton.onClick.RemoveAllListeners();
            startEngineButton.onClick.AddListener(OnStartEnginePressed);
        }

        if (openManualOverrideButton != null)
        {
            openManualOverrideButton.onClick.RemoveAllListeners();
            openManualOverrideButton.onClick.AddListener(OpenManualOverrideModal);
        }

        if (submitManualOverrideButton != null)
        {
            submitManualOverrideButton.onClick.RemoveAllListeners();
            submitManualOverrideButton.onClick.AddListener(OnSubmitManualOverridePressed);
        }

        if (closeManualOverrideButton != null)
        {
            closeManualOverrideButton.onClick.RemoveAllListeners();
            closeManualOverrideButton.onClick.AddListener(CloseManualOverrideModal);
        }

        if (toggleImmobilizerButton != null)
        {
            toggleImmobilizerButton.onClick.RemoveAllListeners();
            toggleImmobilizerButton.onClick.AddListener(OnToggleImmobilizerPressed);
        }

        if (resetDemoButton != null)
        {
            resetDemoButton.onClick.RemoveAllListeners();
            resetDemoButton.onClick.AddListener(OnResetDemoPressed);
        }

        UltrasonicDecoder decoder = Object.FindAnyObjectByType<UltrasonicDecoder>();
        if (decoder != null)
        {
            decoder.OnOwnerAudioPasswordDecoded += OnAcousticTokenReceived;
        }

        isAcousticUnlocked = false;
        isStage1Listening = false;
        isEngineRunning = false;
    }

    void OnDestroy()
    {
        UltrasonicDecoder decoder = Object.FindAnyObjectByType<UltrasonicDecoder>();
        if (decoder != null)
        {
            decoder.OnOwnerAudioPasswordDecoded -= OnAcousticTokenReceived;
        }
    }

    private void SetIndicatorVisuals(bool active)
    {
        if (stage1IgnitionButtonText != null)
            stage1IgnitionButtonText.color = active ? textActiveColor : textDefaultColor;

        if (startEngineButtonText != null)
        {
            if (isEngineRunning)
            {
                startEngineButtonText.text = "STOP ENGINE";
                startEngineButtonText.color = Color.black;
            }
            else
            {
                startEngineButtonText.text = "START ENGINE";
                startEngineButtonText.color = startButtonDefaultTextColor;
            }
        }

        if (startEngineButtonImage != null)
        {
            startEngineButtonImage.color = isEngineRunning ? startButtonRunningBgColor : startButtonDefaultBgColor;
        }

        if (statusCircleImage != null)
            statusCircleImage.color = (isEngineRunning || active) ? circleActiveColor : circleDefaultColor;
    }

    // ============================================================================================================
    // GATEKEEPER LOGIN & VALIDATION ENGINE
    // ============================================================================================================
    public void OnSubmitLoginPressed()
    {
        string enteredId = dealerIdInput != null ? dealerIdInput.text.Trim().ToUpperInvariant() : "";
        string enteredPass = dealerPasswordInput != null ? dealerPasswordInput.text.Trim() : "";

        if (string.IsNullOrEmpty(enteredId))
        {
            ShowLoginError("DEALER STATION ID CANNOT BE EMPTY.");
            return;
        }

        if (string.IsNullOrEmpty(enteredPass))
        {
            ShowLoginError("DEALERSHIP PASSWORD CANNOT BE EMPTY.");
            return;
        }

        bool isAuthorized = false;

        switch (enteredId)
        {
            case "DEALER-LIV01":
                isAuthorized = (enteredPass == "DEALER-PASS-2026");
                break;

            case "DEALER-DEFAULT":
                isAuthorized = (enteredPass == "DEALER-DEFAULT");
                break;

            default:
                if (FakeECU.Instance != null && !string.IsNullOrEmpty(FakeECU.Instance.ServiceOverrideCode))
                {
                    isAuthorized = (enteredPass == FakeECU.Instance.ServiceOverrideCode);
                }
                break;
        }

        if (isAuthorized)
        {
            activeDealerId = enteredId;

            if (FakeECU.Instance != null)
            {
                FakeECU.Instance.SetDealershipImmobilizerMode();

                // Provision key into ECU RAM for the session
                FakeECU.Instance.RegisterNewMasterPassword(SHOWROOM_DEMO_KEY);

                // IMMEDIATELY erase persistent consumer flags written to disk by RegisterNewMasterPassword
                PurgeAccidentalConsumerFlags();

                FakeECU.Instance.LogECUEvent("DealershipGatekeeperAccess", true, 0, "DealerCredential", activeDealerId, "ShowroomSessionAuthorized");
            }

            if (loginErrorText != null) loginErrorText.text = "";

            if (dealershipLoginPanel != null) dealershipLoginPanel.SetActive(false);
            if (dealershipControlsPanel != null) dealershipControlsPanel.SetActive(true);

            RefreshUI();
            ShowFeedback($"DEALERSHIP ACCESS GRANTED. LOGGED UNDER {activeDealerId}.");
        }
        else
        {
            if (FakeECU.Instance != null)
            {
                FakeECU.Instance.LogECUEvent("DealershipGatekeeperAccess", false, 1, "DealerCredential", string.IsNullOrEmpty(enteredId) ? "UNKNOWN" : enteredId, "InvalidCredentials");
            }

            ShowLoginError("ACCESS DENIED: INVALID DEALER ID OR PASSWORD.");
        }
    }

    public void OnCancelLoginPressed()
    {
        SceneManager.LoadScene(modeSelectionScene);
    }

    private void ShowLoginError(string msg)
    {
        if (loginErrorText != null)
        {
            loginErrorText.text = msg.ToUpperInvariant();
        }
    }

    // ============================================================================================================
    // MANUAL PASSWORD OVERRIDE MODAL
    // ============================================================================================================
    public void OpenManualOverrideModal()
    {
        if (manualOverrideModalPanel != null)
        {
            manualOverrideModalPanel.SetActive(true);
            manualOverrideModalPanel.transform.SetAsLastSibling();
        }

        if (manualOverrideInput != null)
        {
            manualOverrideInput.text = "";
            manualOverrideInput.Select();
            manualOverrideInput.ActivateInputField();
        }

        if (manualOverrideErrorText != null)
            manualOverrideErrorText.text = "";
    }

    public void CloseManualOverrideModal()
    {
        if (manualOverrideModalPanel != null)
            manualOverrideModalPanel.SetActive(false);

        if (manualOverrideInput != null)
            manualOverrideInput.text = "";

        if (manualOverrideErrorText != null)
            manualOverrideErrorText.text = "";
    }

    public void OnSubmitManualOverridePressed()
    {
        string entered = manualOverrideInput != null ? manualOverrideInput.text.Trim() : "";

        if (string.IsNullOrEmpty(entered))
        {
            if (manualOverrideErrorText != null)
                manualOverrideErrorText.text = "PASSWORD CANNOT BE EMPTY.";
            return;
        }

        bool match = false;

        if (activeDealerId == "DEALER-LIV01" && entered == "DEALER-PASS-2026") match = true;
        else if (activeDealerId == "DEALER-DEFAULT" && entered == "DEALER-DEFAULT") match = true;
        else if (entered == "DEALER-DEFAULT") match = true;
        else if (FakeECU.Instance != null && entered == FakeECU.Instance.ServiceOverrideCode) match = true;

        if (match)
        {
            isAcousticUnlocked = true;
            isStage1Listening = false;

            if (FakeECU.Instance != null)
            {
                FakeECU.Instance.LogECUEvent("DealershipManualOverride", true, 0, "ManualDealerPIN", string.IsNullOrEmpty(activeDealerId) ? "DEALER_SHOWROOM" : activeDealerId, "ImmobiliserSatisfied");
            }

            CloseManualOverrideModal();
            ShowFeedback("MANUAL OVERRIDE ACCEPTED. PROPULSION CIRCUITS READY.");
            RefreshUI();
        }
        else
        {
            if (FakeECU.Instance != null)
            {
                FakeECU.Instance.LogECUEvent("DealershipManualOverride", false, 1, "ManualDealerPIN", string.IsNullOrEmpty(activeDealerId) ? "DEALER_SHOWROOM" : activeDealerId, "InvalidPassword");
            }

            if (manualOverrideErrorText != null)
                manualOverrideErrorText.text = "ACCESS DENIED: INVALID DEALERSHIP PASSWORD.";
        }
    }

    // ============================================================================================================
    // SHOWROOM UI DISPLAY & STATUS REFRESH
    // ============================================================================================================
    public void RefreshUI()
    {
        if (FakeECU.Instance == null) return;

        bool isBypassed = FakeECU.Instance.CurrentImmobilizerMode == FakeECU.ImmobilizerMode.Disabled;
        bool isAuthorized = isBypassed || isAcousticUnlocked || (FakeECU.Instance.CurrentState == FakeECU.ECUState.Stage2Authorized);

        if (immobilizerStatusText != null)
        {
            immobilizerStatusText.text = isBypassed
                ? "IMMOBILISER STATUS: <color=#FFA500>DISABLED (SHOWROOM BYPASS)</color>"
                : "IMMOBILISER STATUS: <color=#00FF77>ACTIVE (PROTECTED)</color>";
        }

        if (ignitionStatusText != null)
        {
            if (isEngineRunning)
            {
                ignitionStatusText.text = "PROPULSION CIRCUITS: <color=#00FF77>ENGINE RUNNING (CONTACTORS CLOSED)</color>";
            }
            else if (isAuthorized)
            {
                ignitionStatusText.text = "PROPULSION CIRCUITS: <color=#00FF77>READY FOR START (PRESS START ENGINE)</color>";
            }
            else if (isStage1Listening)
            {
                ignitionStatusText.text = "PROPULSION CIRCUITS: <color=#FFA500>STAGE 1 ACTIVE (LISTENING FOR TOKEN)</color>";
            }
            else
            {
                ignitionStatusText.text = "PROPULSION CIRCUITS: <color=#FF4444>INHIBITED (IMMOBILISER ACTIVE)</color>";
            }
        }

        if (demoKeyInfoText != null)
        {
            demoKeyInfoText.text = $"DEFAULT SHOWROOM KEY: <b>{SHOWROOM_DEMO_KEY}</b>";
        }

        if (toggleImmobilizerButtonText != null)
        {
            if (isBypassed)
            {
                toggleImmobilizerButtonText.text = "ENABLE SYSTEM";
                toggleImmobilizerButtonText.color = enableStateTextColor;
            }
            else
            {
                toggleImmobilizerButtonText.text = "DISABLE SYSTEM";
                toggleImmobilizerButtonText.color = disableStateTextColor;
            }
        }

        bool shouldBeActive = isEngineRunning || isAuthorized || isStage1Listening;
        SetIndicatorVisuals(shouldBeActive);

        Canvas.ForceUpdateCanvases();
    }

    // ============================================================================================================
    // ACOUSTIC TOKEN RECEIVER
    // ============================================================================================================
    private void OnAcousticTokenReceived(string decodedPIN)
    {
        if (string.Equals(decodedPIN.Trim(), SHOWROOM_DEMO_KEY, System.StringComparison.OrdinalIgnoreCase))
        {
            isAcousticUnlocked = true;
            isStage1Listening = false;

            if (stage1ListeningRoutine != null)
            {
                StopCoroutine(stage1ListeningRoutine);
                stage1ListeningRoutine = null;
            }

            if (FakeECU.Instance != null)
            {
                FakeECU.Instance.LogECUEvent("ShowroomAcousticUnlock", true, 0, "Acoustic_CPFSK", string.IsNullOrEmpty(activeDealerId) ? "DEALER_SHOWROOM" : activeDealerId, "SalesDemoTablet");
            }

            SetIndicatorVisuals(true);
            ShowFeedback("ACOUSTIC TOKEN ACCEPTED. PROPULSION AUTHORISED. PRESS START ENGINE.");
            RefreshUI();
        }
    }

    // ============================================================================================================
    // BUTTON 1: STAGE 1 IGNITION SIMULATOR BUTTON
    // ============================================================================================================
    public void OnStage1IgnitionPressed()
    {
        if (isEngineRunning)
        {
            ShowFeedback("ENGINE IS ALREADY RUNNING.");
            return;
        }

        // Arm FakeECU in RAM with SHOWROOM_DEMO_KEY, but keep storage clean
        if (FakeECU.Instance != null)
        {
            FakeECU.Instance.RegisterNewMasterPassword(SHOWROOM_DEMO_KEY);
            PurgeAccidentalConsumerFlags();
        }

        UltrasonicDecoder decoder = Object.FindAnyObjectByType<UltrasonicDecoder>();
        if (decoder != null)
        {
            decoder.OnIgnitionStage1Pressed();
        }

        StartStage1ListeningWindow(30.0f);
        ShowFeedback("IGNITION STAGE 1 ACTIVE. CABIN MICROPHONE ENERGIZED.");
        RefreshUI();
    }

    // ============================================================================================================
    // BUTTON 2: START / STOP ENGINE
    // ============================================================================================================
    public void OnStartEnginePressed()
    {
        bool isBypassed = FakeECU.Instance != null && FakeECU.Instance.CurrentImmobilizerMode == FakeECU.ImmobilizerMode.Disabled;
        bool isAuthorized = isBypassed || isAcousticUnlocked || (FakeECU.Instance != null && FakeECU.Instance.CurrentState == FakeECU.ECUState.Stage2Authorized);

        if (isEngineRunning)
        {
            isEngineRunning = false;
            isAcousticUnlocked = false;
            isStage1Listening = false;

            if (stage1ListeningRoutine != null)
            {
                StopCoroutine(stage1ListeningRoutine);
                stage1ListeningRoutine = null;
            }

            if (FakeECU.Instance != null)
            {
                FakeECU.Instance.SetDealershipImmobilizerMode();
                FakeECU.Instance.ResetToLocked();
                FakeECU.Instance.LogECUEvent("EngineShutdown", true, 0, "StartEngineButton", "DEALER_SHOWROOM", "ImmobiliserReEngaged");
            }

            SetIndicatorVisuals(false);
            ShowFeedback("ENGINE SHUTDOWN. IMMOBILISER RE-ENGAGED. AUTHENTICATION REQUIRED TO RESTART.");
            RefreshUI();
            return;
        }

        if (isAuthorized)
        {
            isEngineRunning = true;
            isStage1Listening = false;

            if (stage1ListeningRoutine != null)
            {
                StopCoroutine(stage1ListeningRoutine);
                stage1ListeningRoutine = null;
            }

            if (FakeECU.Instance != null)
                FakeECU.Instance.StartIgnition();

            SetIndicatorVisuals(true);
            ShowFeedback("ENGINE STARTED. HIGH-VOLTAGE CONTACTORS CLOSED. PROPULSION ENGAGED.");
            RefreshUI();
            return;
        }

        if (isStage1Listening)
        {
            if (FakeECU.Instance != null)
            {
                FakeECU.Instance.LogECUEvent("IgnitionStartBlocked", false, 1, "StartEngineButton", "DEALER_SHOWROOM", "AcousticTokenMissing");
            }

            ShowFeedback("START INHIBITED: ACOUSTIC TOKEN REQUIRED. IMMOBILISER ACTIVE.");
            RefreshUI();
            return;
        }

        OnStage1IgnitionPressed();
    }

    private void StartStage1ListeningWindow(float duration)
    {
        if (stage1ListeningRoutine != null)
            StopCoroutine(stage1ListeningRoutine);

        stage1ListeningRoutine = StartCoroutine(Stage1ListeningRoutine(duration));
    }

    private IEnumerator Stage1ListeningRoutine(float duration)
    {
        isStage1Listening = true;
        SetIndicatorVisuals(true);

        yield return new WaitForSeconds(duration);

        if (!isAcousticUnlocked && !isEngineRunning)
        {
            isStage1Listening = false;
            SetIndicatorVisuals(false);
            ShowFeedback("STAGE 1 LISTENING WINDOW ELAPSED. IMMOBILISER LOCKED.");
        }

        stage1ListeningRoutine = null;
        RefreshUI();
    }

    // ============================================================================================================
    // SHOWROOM MANAGEMENT ACTIONS
    // ============================================================================================================
    public void OnToggleImmobilizerPressed()
    {
        if (FakeECU.Instance == null) return;

        if (FakeECU.Instance.CurrentImmobilizerMode == FakeECU.ImmobilizerMode.Disabled)
        {
            FakeECU.Instance.EnableImmobilizerSystem();
            isAcousticUnlocked = false;
            isStage1Listening = false;
            isEngineRunning = false;
            SetIndicatorVisuals(false);
            ShowFeedback("IMMOBILISER ACTIVE. VEHICLE PROTECTED.");
        }
        else
        {
            FakeECU.Instance.DisableImmobilizerSystem();
            SetIndicatorVisuals(true);
            ShowFeedback("IMMOBILISER DISABLED. VEHICLE IN SHOWROOM BYPASS.");
        }

        RefreshUI();
    }

    public void OnResetDemoPressed()
    {
        // 1. Purge all storage keys
        PlayerPrefs.DeleteKey("ECU_DEALERSHIP_PASSWORD");
        PlayerPrefs.DeleteKey("DEALER_APK_AUTH_STATE");
        PurgeAccidentalConsumerFlags();

        isAcousticUnlocked = false;
        isStage1Listening = false;
        isEngineRunning = false;

        if (stage1ListeningRoutine != null)
        {
            StopCoroutine(stage1ListeningRoutine);
            stage1ListeningRoutine = null;
        }

        SetIndicatorVisuals(false);

        // 2. Revert ECU state back to clean Factory Mode (0x00)
        if (FakeECU.Instance != null)
        {
            FakeECU.Instance.SetFactoryImmobilizerMode();
            FakeECU.Instance.ResetToLocked();
            FakeECU.Instance.LogECUEvent(
                "ShowroomReset",
                true,
                0,
                "System",
                string.IsNullOrEmpty(activeDealerId) ? "DEALER_SHOWROOM" : activeDealerId,
                "ReturnToModeSelect"
            );
        }

        ShowFeedback("RESETTING DEMO. RETURNING TO MODE SELECTION...");
        StartCoroutine(ReturnToModeSelectRoutine(1.2f));
    }

    private void PurgeAccidentalConsumerFlags()
    {
        PlayerPrefs.DeleteKey("ConsumerPasswordSet");
        PlayerPrefs.DeleteKey("ECU_USER_SALT");
        PlayerPrefs.DeleteKey("ECU_ENCRYPTED_SECRET");
        PlayerPrefs.DeleteKey("ECU_USER_PASSWORD");
        PlayerPrefs.Save();
    }

    private IEnumerator ReturnToModeSelectRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(modeSelectionScene);
    }

    private void ShowFeedback(string msg)
    {
        if (feedbackNoticeText == null) return;

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        feedbackNoticeText.text = msg.ToUpperInvariant();
        feedbackNoticeText.gameObject.SetActive(true);
        feedbackRoutine = StartCoroutine(HideFeedbackAfterSeconds(feedbackDisplayDuration));
    }

    private IEnumerator HideFeedbackAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (feedbackNoticeText != null)
            feedbackNoticeText.gameObject.SetActive(false);
        feedbackRoutine = null;
    }
}