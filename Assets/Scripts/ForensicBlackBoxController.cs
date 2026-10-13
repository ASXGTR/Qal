using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Text;
using System.Collections;

public class ForensicBlackboxController : MonoBehaviour
{
    // ============================================================================================================
    //  FORENSIC BLACKBOX CONTROLLER - AUDIT TRAIL, DEEP TAMPER COUNTERS & POPUP ACTIONS MENU
    // ============================================================================================================

    [Header("Actions Popup Menu")]
    public Button openActionsPopupButton;      // The single trigger button on the main blackbox view
    public GameObject actionsPopupPanel;       // The small popup modal holding the action buttons
    public Button closeActionsPopupButton;     // Cancel / close the popup menu

    [Header("Action Buttons (Inside the Popup Panel)")]
    public Button refreshLogButton;
    public Button exportEvidenceFileButton;
    public Button backToHubButton;

    [Header("Left Column: Telemetry & Deep Tamper Values (Uppercase Data Only)")]
    public TextMeshProUGUI gpsValueText;
    public TextMeshProUGUI zoneValueText;
    public TextMeshProUGUI hashIntegrityValueText;
    public TextMeshProUGUI hsmStatusValueText;
    public TextMeshProUGUI flashAttemptsValueText;
    public TextMeshProUGUI workshopAccessValueText;
    public TextMeshProUGUI forensicAccessValueText;
    public TextMeshProUGUI lockoutsValueText;
    public TextMeshProUGUI retentionPolicyValueText;

    [Header("Right Column: Scrollable Audit Trail")]
    public ScrollRect logScrollRect;
    public TextMeshProUGUI logContentText;
    public TextMeshProUGUI totalRecordsText;

    void Awake()
    {
        // Auto-correct prototype canvas typo in scene hierarchy if present
        AutoCorrectCanvasLabels();

        CloseActionsPopup();
        RefreshAll();
    }

    void Start()
    {
        // Wire Open / Close for the Popup Menu
        if (openActionsPopupButton != null)
        {
            openActionsPopupButton.onClick.RemoveAllListeners();
            openActionsPopupButton.onClick.AddListener(OpenActionsPopup);
        }

        if (closeActionsPopupButton != null)
        {
            closeActionsPopupButton.onClick.RemoveAllListeners();
            closeActionsPopupButton.onClick.AddListener(CloseActionsPopup);
        }

        // Wire Action Buttons (located inside the popup)
        if (refreshLogButton != null)
        {
            refreshLogButton.onClick.RemoveAllListeners();
            refreshLogButton.onClick.AddListener(OnRefreshPressed);
        }

        if (exportEvidenceFileButton != null)
        {
            exportEvidenceFileButton.onClick.RemoveAllListeners();
            exportEvidenceFileButton.onClick.AddListener(OnExportPressed);
        }

        if (backToHubButton != null)
        {
            backToHubButton.onClick.RemoveAllListeners();
            backToHubButton.onClick.AddListener(OnBackToHubPressed);
        }

        CloseActionsPopup();
        RefreshAll();
    }

    void OnEnable()
    {
        AutoCorrectCanvasLabels();
        CloseActionsPopup();
        RefreshAll();
        StartCoroutine(RefreshNextFrameRoutine());
    }

    private void AutoCorrectCanvasLabels()
    {
        // Scans all child labels on the blackbox canvas and fixes "ROOM" -> "ROOT"
        var allTextComponents = GetComponentsInChildren<TextMeshProUGUI>(true);
        if (allTextComponents != null)
        {
            foreach (var t in allTextComponents)
            {
                if (t != null && !string.IsNullOrEmpty(t.text) && t.text.Contains("HSM ROOM SECRET"))
                {
                    t.text = t.text.Replace("HSM ROOM SECRET", "HSM ROOT SECRET");
                }
            }
        }
    }

    private IEnumerator RefreshNextFrameRoutine()
    {
        yield return null;
        RefreshAll();
    }

    public void RefreshAll()
    {
        PopulateDeepTamperMetrics();
        PopulateLogDisplay();
        Canvas.ForceUpdateCanvases();
    }

    // ============================================================================================================
    //  POPUP ACTIONS MENU CONTROLS
    // ============================================================================================================
    public void OpenActionsPopup()
    {
        if (actionsPopupPanel != null)
        {
            actionsPopupPanel.SetActive(true);
            actionsPopupPanel.transform.SetAsLastSibling();
        }
    }

    public void CloseActionsPopup()
    {
        if (actionsPopupPanel != null)
            actionsPopupPanel.SetActive(false);
    }

    private void OnRefreshPressed()
    {
        RefreshAll();
        CloseActionsPopup();
    }

    private void OnExportPressed()
    {
        ExportLogToDesktopFile();
        CloseActionsPopup();
    }

    private void OnBackToHubPressed()
    {
        CloseActionsPopup();
        CloseSubPanel();
    }

    // ============================================================================================================
    //  LEFT COLUMN: DEEP TAMPER & TELEMETRY POPULATION (STRICT UPPERCASE)
    // ============================================================================================================
    private void PopulateDeepTamperMetrics()
    {
        var ecu = FakeECU.Instance;
        if (ecu == null) return;

        if (gpsValueText != null)
            gpsValueText.text = $"{ecu.lastUnlockLatitude:F4} N, {Mathf.Abs(ecu.lastUnlockLongitude):F4} W";

        if (zoneValueText != null)
            zoneValueText.text = "SIMULATED METROPOLITAN JURISDICTION";

        if (hashIntegrityValueText != null)
            hashIntegrityValueText.text = "<color=#00FF77>VERIFIED INTACT (ISO 21434 / SHA-256)</color>";

        if (hsmStatusValueText != null)
        {
            hsmStatusValueText.text = ecu.isHSMSecretIntact
                ? "<color=#00FF77>VERIFIED PROVISIONED (EVITA COMPLIANT)</color>"
                : "<color=#FF4444>TAMPERED / UNPROVISIONED</color>";
        }

        if (flashAttemptsValueText != null)
        {
            flashAttemptsValueText.text = ecu.totalECUFlashAttempts == 0
                ? "0000 (ORIGINAL OEM FIRMWARE)"
                : $"<color=#FFA500>{ecu.totalECUFlashAttempts:D4} (REPROGRAMMING DETECTED)</color>";
        }

        if (workshopAccessValueText != null)
            workshopAccessValueText.text = $"{ecu.totalServiceModeEntries:D4} ACCEPTED / {ecu.totalServiceModeFailures:D4} REJECTED";

        if (forensicAccessValueText != null)
            forensicAccessValueText.text = $"{ecu.totalForensicModeEntries:D4} AUTHORIZED / {ecu.totalForensicModeFailures:D4} REJECTED";

        if (lockoutsValueText != null)
            lockoutsValueText.text = $"{ecu.totalLockoutsTriggered:D4} COOLDOWN PENALTIES IMPOSED";

        if (retentionPolicyValueText != null)
            retentionPolicyValueText.text = "CIRCULAR RING BUFFER (100 EVENT CAP - GDPR)";
    }

    // ============================================================================================================
    //  RIGHT COLUMN: CHRONOLOGICAL AUDIT LOG STREAM (COMPACT MULTI-LINE FORMATTING)
    // ============================================================================================================
    public void PopulateLogDisplay()
    {
        var ecu = FakeECU.Instance;
        if (ecu == null) return;

        var logs = ecu.EventLogs;
        int rawCount = logs != null ? logs.Count : 0;
        int displayCount = Mathf.Min(rawCount, 100);

        if (totalRecordsText != null)
            totalRecordsText.text = $"TOTAL LOGGED RECORDS: {displayCount:D4} / 0100";

        if (logContentText != null)
        {
            if (rawCount == 0)
            {
                logContentText.text = "NO FORENSIC EVENTS LOGGED YET.";
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                // Iterate through the bounded records (newest first)
                int startIndex = rawCount - 1;
                int stopIndex = Mathf.Max(0, rawCount - 100);

                for (int i = startIndex; i >= stopIndex; i--)
                {
                    var e = logs[i];
                    string colorTag = e.Success ? "<color=#00FF77>" : "<color=#FF4444>";
                    string timeStr = !string.IsNullOrEmpty(e.Timestamp) ? e.Timestamp.ToUpperInvariant() : "UNKNOWN TIMESTAMP";
                    string eventType = !string.IsNullOrEmpty(e.EventType) ? e.EventType.ToUpperInvariant() : "UNKNOWN_EVENT";
                    string subject = !string.IsNullOrEmpty(e.SubjectIdentifier) ? e.SubjectIdentifier.ToUpperInvariant() : "NONE";
                    string slot = !string.IsNullOrEmpty(e.KeySlot) ? e.KeySlot.ToUpperInvariant() : "NONE";
                    string method = !string.IsNullOrEmpty(e.Method) ? e.Method.ToUpperInvariant() : "SYSTEM";
                    string statusStr = e.Success ? "SUCCESS (PASS)" : "FAILED (REJECTED)";

                    sb.AppendLine($"<b>[RECORD #{i + 1:D4}] {timeStr}</b>");
                    sb.AppendLine($"EVENT: {colorTag}<b>{eventType}</b></color>");
                    sb.AppendLine($"RESULT: {colorTag}<b>{statusStr}</b></color>");
                    sb.AppendLine($"SUBJECT: {subject}");
                    sb.AppendLine($"SLOT: {slot}");
                    sb.AppendLine($"METHOD: {method}");
                    sb.AppendLine($"GPS: {e.GPSLat:F4} N, {Mathf.Abs(e.GPSLon):F4} W");
                    sb.AppendLine($"PREV HASH: {SafeTruncate(e.PreviousHash, 12)}...");
                    sb.AppendLine($"SEAL HASH: <color=#FFD700>{SafeTruncate(e.EntryHash, 12)}...</color>");
                    sb.AppendLine("----------------------------------------");
                }
                logContentText.text = sb.ToString();
            }
        }

        if (logScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            logScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private string SafeTruncate(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value))
            return "NONE";

        value = value.Trim().ToUpperInvariant();
        return value.Length <= maxChars ? value : value.Substring(0, maxChars);
    }

    // ============================================================================================================
    //  EXPORT EVIDENCE DOSSIER TO DESKTOP
    // ============================================================================================================
    public void ExportLogToDesktopFile()
    {
        var ecu = FakeECU.Instance;
        if (ecu == null) return;

        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "QAL_ForensicLogs");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string filename = $"COURTROOM_EVIDENCE_DOSSIER_{ecu.VIN}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
            string fullPath = Path.Combine(folder, filename);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("==========================================================================================");
            sb.AppendLine("   STATUTORY VEHICLE FORENSIC BLACKBOX AUDIT TRAIL - CHAIN OF CUSTODY REPORT");
            sb.AppendLine("   COMPLIANCE: ISO/SAE 21434 / UNECE WP.29 R155 / STATUTORY EVIDENCE ACT");
            sb.AppendLine("==========================================================================================");
            sb.AppendLine($"VEHICLE IDENTIFICATION NUMBER (VIN): {ecu.VIN}");
            sb.AppendLine($"CALIBRATION IDENTIFIER:              {ecu.ECUCalibrationId}");
            sb.AppendLine($"ECU HARDWARE VERSION:                {ecu.ECUHardwareVersion}");
            sb.AppendLine($"BOOTLOADER REVISION:                 {ecu.ECUBootloaderVersion}");
            sb.AppendLine($"EXTRACTION TIMESTAMP:                {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss 'UTC'}");
            sb.AppendLine($"INVESTIGATING AUTHORITY:             SIMULATED STATUTORY ACCIDENT INVESTIGATION UNIT");
            sb.AppendLine($"WARRANT / OFFICER CREDENTIAL:        DEMO_OFFICER_4821_CRN");
            sb.AppendLine("------------------------------------------------------------------------------------------");
            sb.AppendLine("HARDWARE TAMPER AND SYSTEM INTEGRITY SUMMARY:");
            sb.AppendLine($"• BOOTLOADER FLASH ATTEMPTS:         {ecu.totalECUFlashAttempts:D4}");
            sb.AppendLine($"• HSM ROOT SECRET STATUS:            {(ecu.isHSMSecretIntact ? "VERIFIED INTACT" : "CORRUPTED")}");
            sb.AppendLine($"• MONOTONIC LIFETIME RESETS:         {ecu.totalFactoryResets:D4}");
            sb.AppendLine($"• WORKSHOP SERVICE GATEWAY SESSIONS: {ecu.totalServiceModeEntries:D4} ACCEPTED / {ecu.totalServiceModeFailures:D4} REJECTED");
            sb.AppendLine($"• STATUTORY FORENSIC SESSIONS:       {ecu.totalForensicModeEntries:D4} AUTHORIZED / {ecu.totalForensicModeFailures:D4} REJECTED");
            sb.AppendLine($"• TOTAL LOCKOUT PENALTIES IMPOSED:   {ecu.totalLockoutsTriggered:D4}");
            sb.AppendLine("==========================================================================================\n");

            sb.Append(ecu.ExportEventLog());

            File.WriteAllText(fullPath, sb.ToString());

            Debug.Log($"[FORENSIC EXPORT] Evidence dossier saved to: {fullPath}");

            if (DemoManager.Instance != null)
                DemoManager.Instance.ShowSuccessPopup($"EVIDENCE DOSSIER EXPORTED TO DESKTOP:\n{filename}");

            Application.OpenURL("file://" + folder);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FORENSIC EXPORT ERROR] {ex.Message}");
            if (DemoManager.Instance != null)
                DemoManager.Instance.ShowErrorPopup("FAILED TO EXPORT EVIDENCE DOSSIER.");
        }
    }

    // ============================================================================================================
    //  NAVIGATION BACK TO FORENSIC HUB (RESTORES FORENSIC HUB VIEW)
    // ============================================================================================================
    public void CloseSubPanel()
    {
        AuthorisedForensicPanelController hub = GetComponentInParent<AuthorisedForensicPanelController>(true);

        if (hub == null)
        {
            hub = UnityEngine.Object.FindAnyObjectByType<AuthorisedForensicPanelController>();
        }

        if (hub != null)
        {
            hub.CloseAllSubPanels();
            hub.RefreshHub();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}