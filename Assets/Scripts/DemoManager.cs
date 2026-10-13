using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DemoManager : MonoBehaviour
{
    // ============================================================================================================
    //  DEMO MANAGER — CENTRAL VEHICLE HMI, SECURITY WORKFLOW & ENGINE IGNITION CONTROLLER
    //  Conforms to ISO 14229-1 (UDS Service 0x27), ISO/SAE 21434 (RBAC), and UNECE WP.29 R155 Audit Logging
    // ============================================================================================================

    public static DemoManager Instance;

    [Header("ECU Source")]
    public FakeECU ECUSource;

    // ------------------------------------------------------------------------------------------------------------
    //  MANUAL OVERRIDE MODAL (OWNER & GUEST FALLBACK)
    // ------------------------------------------------------------------------------------------------------------
    [Header("Manual Override Modal (Fallback)")]
    public GameObject manualOverridePanel;
    public Button openManualOverrideButton;
    public Button closeManualOverrideButton;
    public TMP_InputField manualOverrideInput;
    public Button manualOverrideSubmitButton;
    public TextMeshProUGUI manualOverrideStatusText;

    private int consecutiveEmptyEntries = 0;

    // ------------------------------------------------------------------------------------------------------------
    //  TECHNICIAN GATEWAY (UNMASKED AFTER 3 EMPTY ENTRIES)
    // ------------------------------------------------------------------------------------------------------------
    [Header("Technician Gateway Panel")]
    public GameObject technicianGatewayPanel;
    public Button btnAuthorisedTechnician;
    public Button btnAuthorisedForensic;
    public Button gatewayExitButton;

    [Header("Technician Access Prompt Group")]
    public GameObject techPromptRoot;
    public TMP_InputField techInputField;
    public Button techEnterButton;
    public Button techCancelButton;
    public TextMeshProUGUI techStatusText;

    [Header("Forensic Access Prompt Group")]
    public GameObject forensicPromptRoot;
    public TMP_InputField forensicInputField;
    public Button forensicEnterButton;
    public Button forensicCancelButton;
    public TextMeshProUGUI forensicStatusText;

    // ------------------------------------------------------------------------------------------------------------
    //  TARGET INSTITUTIONAL TERMINALS (ATP & AFP)
    // ------------------------------------------------------------------------------------------------------------
    [Header("Target Institutional Terminals")]
    public GameObject authorisedTechnicianPanel;  // ATP (Workshop Maintenance Bay)
    public GameObject authorisedForensicPanel;    // AFP (Statutory Forensic & Recovery Hub)

    // ------------------------------------------------------------------------------------------------------------
    //  CONSUMER MODE ENGINE START / STOP IGNITION CONTROLS
    // ------------------------------------------------------------------------------------------------------------
    [Header("Stage 1 Ignition Simulator Button")]
    public Button stage1IgnitionButton;
    public TextMeshProUGUI stage1IgnitionButtonText;

    [Header("Start / Stop Engine Button (Multi-Push Gating)")]
    public Button startEngineButton;
    public Image startEngineButtonImage;                                // Button background image
    public TextMeshProUGUI startEngineButtonText;
    public Color startButtonDefaultBgColor = Color.white;
    public Color startButtonRunningBgColor = new Color(0f, 1f, 0f);      // #00FF00 Green
    public Color startButtonDefaultTextColor = new Color(0f, 0.65f, 0.2f); // Green text on white button

    // ------------------------------------------------------------------------------------------------------------
    //  CENTRAL POPUPS & IGNITION STATUS INDICATORS
    // ------------------------------------------------------------------------------------------------------------
    [Header("Centralized Feedback Popups")]
    public GameObject successPopUpPanel;
    public TextMeshProUGUI successPopUpText;
    public GameObject errorPopUpPanel;
    public TextMeshProUGUI errorPopUpText;

    [Header("Ignition Status Visuals")]
    public TextMeshProUGUI ignitionButtonText;                          // Secondary text / legacy indicator
    public Image micStatusCircle;                                       // Status circle (Red: unpowered, Green: active)

    public Color textDefaultColor = Color.white;
    public Color textActiveColor = new Color(0f, 1f, 0f);               // #00FF00
    public Color micOffColor = Color.red;
    public Color micOnColor = Color.green;

    // Internal State Tracking
    private bool isEngineRunning = false;
    private bool isStage1Listening = false;
    private Coroutine stage1ListeningRoutine;

    // ============================================================================================================
    //  LIFECYCLE
    // ============================================================================================================
    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (startEngineButtonImage == null && startEngineButton != null)
            startEngineButtonImage = startEngineButton.GetComponent<Image>();
    }

    private void Start()
    {
        if (ECUSource == null)
            ECUSource = FakeECU.Instance;

        // Manual Override Listeners
        if (openManualOverrideButton != null)
        {
            openManualOverrideButton.onClick.RemoveAllListeners();
            openManualOverrideButton.onClick.AddListener(OpenManualOverridePanel);
        }

        if (closeManualOverrideButton != null)
        {
            closeManualOverrideButton.onClick.RemoveAllListeners();
            closeManualOverrideButton.onClick.AddListener(CloseManualOverridePanel);
        }

        if (manualOverrideSubmitButton != null)
        {
            manualOverrideSubmitButton.onClick.RemoveAllListeners();
            manualOverrideSubmitButton.onClick.AddListener(OnManualOverrideSubmit);
        }

        // Technician Gateway Main Navigation Buttons
        if (btnAuthorisedTechnician != null)
        {
            btnAuthorisedTechnician.onClick.RemoveAllListeners();
            btnAuthorisedTechnician.onClick.AddListener(OnAuthorisedTechnicianPressed);
        }

        if (btnAuthorisedForensic != null)
        {
            btnAuthorisedForensic.onClick.RemoveAllListeners();
            btnAuthorisedForensic.onClick.AddListener(OnAuthorisedForensicPressed);
        }

        if (gatewayExitButton != null)
        {
            gatewayExitButton.onClick.RemoveAllListeners();
            gatewayExitButton.onClick.AddListener(CloseTechnicianGateway);
        }

        // Dedicated Technician Prompt Buttons
        if (techEnterButton != null)
        {
            techEnterButton.onClick.RemoveAllListeners();
            techEnterButton.onClick.AddListener(OnTechPromptEnter);
        }

        if (techCancelButton != null)
        {
            techCancelButton.onClick.RemoveAllListeners();
            techCancelButton.onClick.AddListener(OnTechPromptCancel);
        }

        // Dedicated Forensic Prompt Buttons
        if (forensicEnterButton != null)
        {
            forensicEnterButton.onClick.RemoveAllListeners();
            forensicEnterButton.onClick.AddListener(OnForensicPromptEnter);
        }

        if (forensicCancelButton != null)
        {
            forensicCancelButton.onClick.RemoveAllListeners();
            forensicCancelButton.onClick.AddListener(OnForensicPromptCancel);
        }

        // Consumer Mode Engine Controls
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

        HideAllDemoPanels();
        ResetIgnitionUI();
    }

    // ============================================================================================================
    //  STAGE 1 IGNITION SIMULATOR BUTTON (CONSUMER MODE)
    // ============================================================================================================
    public void OnStage1IgnitionPressed()
    {
        if (isEngineRunning)
        {
            ShowSuccessPopup("ENGINE IS ALREADY RUNNING.");
            return;
        }

        if (FakeECU.Instance != null && FakeECU.Instance.lockoutActive)
        {
            ShowErrorPopup($"IMMOBILISER IS LOCKED ({FakeECU.Instance.RemainingCooldownSeconds}S REMAINING).");
            return;
        }

        // Energize interior cabin microphone via UltrasonicDecoder
        UltrasonicDecoder decoder = Object.FindAnyObjectByType<UltrasonicDecoder>();
        if (decoder != null)
        {
            decoder.OnIgnitionStage1Pressed();
        }
        else if (FakeECU.Instance != null)
        {
            FakeECU.Instance.EnterStage1Listening();
        }

        StartStage1ListeningWindow(30.0f);
        ShowSuccessPopup("IGNITION STAGE 1 ACTIVE. CABIN MICROPHONE ENERGIZED.");
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
        SetIgnitionActive(true);

        yield return new WaitForSeconds(duration);

        // If listening window times out without token and engine is not running, revert to locked
        bool isAuthorized = FakeECU.Instance != null && FakeECU.Instance.CurrentState == FakeECU.ECUState.Stage2Authorized;
        if (!isAuthorized && !isEngineRunning)
        {
            isStage1Listening = false;
            ResetIgnitionUI();
            ShowErrorPopup("STAGE 1 LISTENING WINDOW ELAPSED. IMMOBILISER LOCKED.");
        }

        stage1ListeningRoutine = null;
    }

    // ============================================================================================================
    //  START / STOP ENGINE BUTTON (CONSUMER MODE MULTI-PUSH GATING)
    // ============================================================================================================
    public void OnStartEnginePressed()
    {
        bool isBypassed = FakeECU.Instance != null && FakeECU.Instance.CurrentImmobilizerMode == FakeECU.ImmobilizerMode.Disabled;
        bool isAuthorized = isBypassed || (FakeECU.Instance != null && FakeECU.Instance.CurrentState == FakeECU.ECUState.Stage2Authorized);

        // --------------------------------------------------------------------------------------------------------
        // PUSH 3: ENGINE IS ALREADY RUNNING -> SHUT DOWN ENGINE
        // --------------------------------------------------------------------------------------------------------
        if (isEngineRunning)
        {
            isEngineRunning = false;
            isStage1Listening = false;

            if (stage1ListeningRoutine != null)
            {
                StopCoroutine(stage1ListeningRoutine);
                stage1ListeningRoutine = null;
            }

            // CONSUMER MODE PERSISTENCE RULE:
            // If the owner previously disabled the system in Owner Mode, keep it disabled! Do not force re-enable.
            if (isBypassed)
            {
                if (FakeECU.Instance != null)
                {
                    FakeECU.Instance.ResetToLocked(); // Clears volatile authorization, but preserves Disabled mode
                    FakeECU.Instance.LogECUEvent("EngineShutdown", true, 0, "StartEngineButton", "MasterOwner", "ImmobiliserRemainsDisabled");
                }

                ResetIgnitionUI();
                ShowSuccessPopup("ENGINE SHUTDOWN. IMMOBILISER REMAINS DISABLED (OWNER BYPASS).");
            }
            else
            {
                // Re-engage immobiliser back to active locked state
                if (FakeECU.Instance != null)
                {
                    FakeECU.Instance.ResetToLocked();
                    FakeECU.Instance.LogECUEvent("EngineShutdown", true, 0, "StartEngineButton", "MasterOwner", "ImmobiliserReEngaged");
                }

                ResetIgnitionUI();
                ShowSuccessPopup("ENGINE SHUTDOWN. IMMOBILISER RE-ENGAGED. AUTHENTICATION REQUIRED TO RESTART.");
            }

            return;
        }

        // --------------------------------------------------------------------------------------------------------
        // PUSH 2 (AUTHORIZED): PROPULSION UNLOCKED -> STARTS ENGINE
        // --------------------------------------------------------------------------------------------------------
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

            SetIgnitionActive(true);
            ShowSuccessPopup("ENGINE STARTED. HIGH-VOLTAGE CONTACTORS CLOSED. PROPULSION ENGAGED.");
            return;
        }

        // --------------------------------------------------------------------------------------------------------
        // PUSH 2 (UNAUTHORIZED): STAGE 1 IS ACTIVE BUT NO TOKEN / PIN ENTERED
        // --------------------------------------------------------------------------------------------------------
        if (isStage1Listening)
        {
            if (FakeECU.Instance != null)
            {
                FakeECU.Instance.LogECUEvent("IgnitionStartBlocked", false, 1, "StartEngineButton", "None", "AcousticTokenMissing");
            }

            ShowErrorPopup("START INHIBITED: ACOUSTIC TOKEN REQUIRED. IMMOBILISER ACTIVE.");
            return;
        }

        // --------------------------------------------------------------------------------------------------------
        // PUSH 1 (FROM LOCKED STATE): ACTS LIKE PRESSING STAGE 1 BUTTON
        // --------------------------------------------------------------------------------------------------------
        OnStage1IgnitionPressed();
    }

    // ============================================================================================================
    //  MANUAL OVERRIDE (FALLBACK MODAL) & 3-EMPTY-TRIGGER
    // ============================================================================================================
    public void OpenManualOverridePanel()
    {
        if (manualOverridePanel != null)
        {
            manualOverridePanel.SetActive(true);
            manualOverridePanel.transform.SetAsLastSibling();
        }

        if (manualOverrideInput != null)
        {
            manualOverrideInput.text = "";
            manualOverrideInput.Select();
            manualOverrideInput.ActivateInputField();
        }

        if (manualOverrideStatusText != null)
            manualOverrideStatusText.text = "";
    }

    public void CloseManualOverridePanel()
    {
        if (manualOverridePanel != null)
            manualOverridePanel.SetActive(false);

        if (manualOverrideInput != null)
            manualOverrideInput.text = "";

        if (manualOverrideStatusText != null)
            manualOverrideStatusText.text = "";
    }

    public void OnManualOverrideSubmit()
    {
        if (manualOverrideInput == null || FakeECU.Instance == null)
            return;

        string entered = manualOverrideInput.text.Trim();

        // 1. CHECK FOR 3-EMPTY-ENTRY TECHNICIAN TRIGGER (STEGANOGRAPHIC UNMASKING)
        if (string.IsNullOrEmpty(entered))
        {
            consecutiveEmptyEntries++;
            if (consecutiveEmptyEntries >= 3)
            {
                consecutiveEmptyEntries = 0;
                CloseManualOverridePanel();
                OpenTechnicianGateway();
                ShowSuccessPopup("3 EMPTY SUBMISSIONS DETECTED - TECHNICIAN GATEWAY UNMASKED.");
                return;
            }

            ShowManualOverrideError(
                $"PIN CANNOT BE EMPTY (ATTEMPT {consecutiveEmptyEntries}/3).\n" +
                "3 INCORRECT ATTEMPTS WILL CAUSE A TEMPORARY LOCKOUT."
            );
            return;
        }

        consecutiveEmptyEntries = 0;

        if (QALEncoder.Instance != null)
            entered = QALEncoder.Instance.SanitizePasswordPublic(entered);

        // 2. EVALUATE VIA FAKEECU (MASTER OWNER OR DELEGATED GUEST PIN)
        if (FakeECU.Instance.TryManualPropulsionOverride(entered, out string message))
        {
            SetIgnitionActive(true);
            ShowSuccessPopup(message.ToUpperInvariant());
            CloseManualOverridePanel();
        }
        else
        {
            if (FakeECU.Instance.lockoutActive)
            {
                ShowManualOverrideError($"IMMOBILIZER LOCKED. COOLDOWN: {FakeECU.Instance.RemainingCooldownSeconds}S.");
            }
            else
            {
                ShowManualOverrideError(
                    $"{message.ToUpperInvariant()}\n" +
                    "3 INCORRECT ATTEMPTS WILL CAUSE A TEMPORARY LOCKOUT."
                );
            }
        }
    }

    private void ShowManualOverrideError(string msg)
    {
        if (manualOverrideStatusText != null)
        {
            manualOverrideStatusText.text = msg;
            StartCoroutine(ClearManualOverrideStatus(4.5f));
        }
        else
        {
            ShowErrorPopup(msg);
        }
    }

    private IEnumerator ClearManualOverrideStatus(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (manualOverrideStatusText != null)
            manualOverrideStatusText.text = "";
    }

    // ============================================================================================================
    //  TECHNICIAN GATEWAY FLOW & INLINE PROMPTS
    // ============================================================================================================
    public void OpenTechnicianGateway()
    {
        if (technicianGatewayPanel != null)
        {
            technicianGatewayPanel.SetActive(true);
            technicianGatewayPanel.transform.SetAsLastSibling();
        }

        ResetGatewayPrompts();
    }

    public void CloseTechnicianGateway()
    {
        ResetGatewayPrompts();

        if (technicianGatewayPanel != null)
            technicianGatewayPanel.SetActive(false);
    }

    private void ResetGatewayPrompts()
    {
        if (btnAuthorisedTechnician != null) btnAuthorisedTechnician.interactable = true;
        if (btnAuthorisedForensic != null) btnAuthorisedForensic.interactable = true;

        if (techPromptRoot != null) techPromptRoot.SetActive(false);
        if (forensicPromptRoot != null) forensicPromptRoot.SetActive(false);

        if (techInputField != null) techInputField.text = "";
        if (techStatusText != null) techStatusText.text = "";
        if (forensicInputField != null) forensicInputField.text = "";
        if (forensicStatusText != null) forensicStatusText.text = "";
    }

    private void SetGatewayMainButtonsInteractable(bool interactable)
    {
        if (btnAuthorisedTechnician != null) btnAuthorisedTechnician.interactable = interactable;
        if (btnAuthorisedForensic != null) btnAuthorisedForensic.interactable = interactable;
    }

    public void OnAuthorisedTechnicianPressed()
    {
        SetGatewayMainButtonsInteractable(false);

        if (techPromptRoot != null) techPromptRoot.SetActive(true);

        if (techInputField != null)
        {
            techInputField.text = "";
            techInputField.Select();
            techInputField.ActivateInputField();
        }

        if (techStatusText != null) techStatusText.text = "ENTER DEALERSHIP STATION CODE:";
    }

    public void OnTechPromptCancel() => ResetGatewayPrompts();

    public void OnTechPromptEnter()
    {
        if (techInputField == null) return;
        string entered = techInputField.text.Trim();

        if (string.IsNullOrEmpty(entered))
        {
            if (techStatusText != null) techStatusText.text = "<color=red>CODE CANNOT BE EMPTY.</color>";
            return;
        }

        bool isDealerValid = false;
        if (FakeECU.Instance != null)
        {
            // Strictly validates accredited Workshop Facility Station Code or dynamic Service Override Code.
            // Showroom staging passwords (DEALER-DEFAULT, DEALER-PASS-2026) are strictly rejected here.
            isDealerValid = FakeECU.Instance.TryServiceOverride(entered) ||
                            string.Equals(entered, "DEALER-LIV01", System.StringComparison.OrdinalIgnoreCase);

            if (!isDealerValid)
            {
                // Record failure counter and commit immutable audit log
                // (LogECUEvent automatically calls SavePersistentTelemetry to persist data)
                FakeECU.Instance.totalServiceModeFailures++;
                FakeECU.Instance.LogECUEvent(
                    eventType: "WorkshopStationAccess",
                    success: false,
                    attempts: 1,
                    method: "DiagnosticTerminal",
                    keySlot: string.IsNullOrEmpty(entered) ? "UNKNOWN" : entered,
                    subjectId: "UNAUTHORIZED_PROBE"
                );
            }
        }

        if (isDealerValid)
        {
            CloseTechnicianGateway();
            OpenAuthorisedTechnicianPanel();
            ShowSuccessPopup("AUTHORISED DEALERSHIP WORKSHOP MODE ACTIVATED.");
        }
        else
        {
            if (techStatusText != null)
                techStatusText.text = "<color=red>INVALID DEALERSHIP CODE.</color>";
        }
    }

    public void OnAuthorisedForensicPressed()
    {
        SetGatewayMainButtonsInteractable(false);

        if (forensicPromptRoot != null) forensicPromptRoot.SetActive(true);

        if (forensicInputField != null)
        {
            forensicInputField.text = "";
            forensicInputField.Select();
            forensicInputField.ActivateInputField();
        }

        if (forensicStatusText != null) forensicStatusText.text = "ENTER STATUTORY WARRANT / FORENSIC TOKEN:";
    }

    public void OnForensicPromptCancel() => ResetGatewayPrompts();

    public void OnForensicPromptEnter()
    {
        if (forensicInputField == null) return;
        string entered = forensicInputField.text.Trim();

        if (string.IsNullOrEmpty(entered))
        {
            if (forensicStatusText != null) forensicStatusText.text = "<color=red>TOKEN CANNOT BE EMPTY.</color>";
            return;
        }

        bool isForensicValid = false;
        if (FakeECU.Instance != null)
        {
            isForensicValid = string.Equals(entered, "POLICE-FORENSIC", System.StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(entered, "WARRANT-99", System.StringComparison.OrdinalIgnoreCase) ||
                              FakeECU.Instance.TryServiceOverride(entered);

            if (isForensicValid)
            {
                FakeECU.Instance.RecordForensicAccess(true, "PC_4821_CRN_88291A");
            }
            else
            {
                FakeECU.Instance.RecordForensicAccess(false, "UNAUTHORIZED_PROBE");
            }
        }

        if (isForensicValid)
        {
            CloseTechnicianGateway();
            OpenAuthorisedForensicPanel();
            ShowSuccessPopup("STATUTORY FORENSIC & FACTORY TERMINAL ACTIVATED.");
        }
        else
        {
            if (forensicStatusText != null)
                forensicStatusText.text = "<color=red>INVALID STATUTORY TOKEN / WARRANT ID.</color>";
        }
    }

    // ============================================================================================================
    //  SUB-PANEL TRANSITIONS (ATP & AFP)
    // ============================================================================================================
    public void OpenAuthorisedTechnicianPanel()
    {
        if (authorisedTechnicianPanel != null)
        {
            authorisedTechnicianPanel.SetActive(true);
            authorisedTechnicianPanel.transform.SetAsLastSibling();

            AuthorisedTechnicianPanelController atp = authorisedTechnicianPanel.GetComponentInChildren<AuthorisedTechnicianPanelController>(true);
            if (atp != null) atp.Refresh();
        }
    }

    public void CloseAuthorisedTechnicianPanel()
    {
        if (authorisedTechnicianPanel != null)
            authorisedTechnicianPanel.SetActive(false);
    }

    public void OpenAuthorisedForensicPanel()
    {
        if (authorisedForensicPanel != null)
        {
            authorisedForensicPanel.SetActive(true);
            authorisedForensicPanel.transform.SetAsLastSibling();

            AuthorisedForensicPanelController afp = authorisedForensicPanel.GetComponentInChildren<AuthorisedForensicPanelController>(true);
            if (afp != null) afp.RefreshHub();
        }
    }

    public void CloseAuthorisedForensicPanel()
    {
        if (authorisedForensicPanel != null)
            authorisedForensicPanel.SetActive(false);
    }

    public void HideAllDemoPanels()
    {
        if (manualOverridePanel != null) manualOverridePanel.SetActive(false);
        if (technicianGatewayPanel != null) technicianGatewayPanel.SetActive(false);
        if (authorisedTechnicianPanel != null) authorisedTechnicianPanel.SetActive(false);
        if (authorisedForensicPanel != null) authorisedForensicPanel.SetActive(false);
        if (successPopUpPanel != null) successPopUpPanel.SetActive(false);
        if (errorPopUpPanel != null) errorPopUpPanel.SetActive(false);
    }

    // ============================================================================================================
    //  POPUPS & IGNITION VISUAL STATES
    // ============================================================================================================
    public void ShowSuccessPopup(string message)
    {
        if (successPopUpPanel != null && successPopUpText != null)
        {
            successPopUpText.text = message;
            successPopUpPanel.SetActive(true);
            successPopUpPanel.transform.SetAsLastSibling();
            StartCoroutine(HidePopupRoutine(successPopUpPanel, 4f));
        }
        else
        {
            Debug.Log($"[DemoManager Success] {message}");
        }
    }

    public void ShowErrorPopup(string message)
    {
        if (errorPopUpPanel != null && errorPopUpText != null)
        {
            errorPopUpText.text = message;
            errorPopUpPanel.SetActive(true);
            errorPopUpPanel.transform.SetAsLastSibling();
            StartCoroutine(HidePopupRoutine(errorPopUpPanel, 4f));
        }
        else
        {
            Debug.LogWarning($"[DemoManager Error] {message}");
        }
    }

    private IEnumerator HidePopupRoutine(GameObject panel, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (panel != null) panel.SetActive(false);
    }

    // Visual synchronization for buttons and circle
    public void SetIgnitionActive(bool active)
    {
        // Stage 1 Button Text
        if (stage1IgnitionButtonText != null)
            stage1IgnitionButtonText.color = active ? textActiveColor : textDefaultColor;

        if (ignitionButtonText != null)
            ignitionButtonText.color = active ? textActiveColor : textDefaultColor;

        // Start / Stop Engine Button Visuals
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

        // Circle indicator
        if (micStatusCircle != null)
            micStatusCircle.color = active ? micOnColor : micOffColor;
    }

    public void ResetIgnitionUI()
    {
        isEngineRunning = false;
        isStage1Listening = false;

        if (stage1IgnitionButtonText != null)
            stage1IgnitionButtonText.color = textDefaultColor;

        if (ignitionButtonText != null)
            ignitionButtonText.color = textDefaultColor;

        if (startEngineButtonText != null)
        {
            startEngineButtonText.text = "START ENGINE";
            startEngineButtonText.color = startButtonDefaultTextColor;
        }

        if (startEngineButtonImage != null)
        {
            startEngineButtonImage.color = startButtonDefaultBgColor;
        }

        if (micStatusCircle != null)
            micStatusCircle.color = micOffColor;
    }
}