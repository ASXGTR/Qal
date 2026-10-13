using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AuthorisedForensicPanelController : MonoBehaviour
{
    // ============================================================================================================
    //  AUTHORISED FORENSIC HUB CONTROLLER (AFP) - STRICT UPPERCASE & ZERO EMOJIS
    // ============================================================================================================

    [Header("Authority Context")]
    public TextMeshProUGUI headerAuthorityText;
    public TextMeshProUGUI warrantIdText;
    public TextMeshProUGUI timestampText;

    [Header("Sub-Panel References")]
    public GameObject identitySubPanel;        // Sub-Panel 1 (Hardware & UDS specs)
    public GameObject blackboxSubPanel;        // Sub-Panel 2 (Scrollable Log & Export)

    [Header("Hub Navigation Buttons")]
    public Button openIdentitySubPanelButton;
    public Button openBlackboxSubPanelButton;
    public Button exitForensicHubButton;

    [Header("Emergency Recovery Actuator (Shift-by-Wire)")]
    public Button toggleShiftByWireButton;
    public TextMeshProUGUI shiftByWireStatusText;
    public TextMeshProUGUI shiftByWireButtonText;

    private bool isTransmissionInNeutral = false;

    void Start()
    {
        if (openIdentitySubPanelButton != null)
        {
            openIdentitySubPanelButton.onClick.RemoveAllListeners();
            openIdentitySubPanelButton.onClick.AddListener(OnOpenIdentitySubPanel);
        }

        if (openBlackboxSubPanelButton != null)
        {
            openBlackboxSubPanelButton.onClick.RemoveAllListeners();
            openBlackboxSubPanelButton.onClick.AddListener(OnOpenBlackboxSubPanel);
        }

        if (exitForensicHubButton != null)
        {
            exitForensicHubButton.onClick.RemoveAllListeners();
            exitForensicHubButton.onClick.AddListener(OnExitForensicHub);
        }

        if (toggleShiftByWireButton != null)
        {
            toggleShiftByWireButton.onClick.RemoveAllListeners();
            toggleShiftByWireButton.onClick.AddListener(OnToggleShiftByWire);
        }

        CloseAllSubPanels();
    }

    void OnEnable()
    {
        RefreshHub();
    }

    public void RefreshHub()
    {
        if (headerAuthorityText != null)
            headerAuthorityText.text = "AUTHORITY: SIMULATED STATUTORY ACCIDENT INVESTIGATION UNIT";

        if (warrantIdText != null)
            warrantIdText.text = "WARRANT / OFFICER ID: DEMO_OFFICER_4821_CRN";

        if (timestampText != null)
            timestampText.text = $"SESSION UTC: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";

        UpdateShiftByWireUI();
    }

    // --- SUB-PANEL NAVIGATION ---
    public void OnOpenIdentitySubPanel()
    {
        if (identitySubPanel != null)
        {
            identitySubPanel.SetActive(true);
            identitySubPanel.transform.SetAsLastSibling();

            var ctrl = identitySubPanel.GetComponentInChildren<ForensicVehicleIdentityController>(true);
            if (ctrl != null) ctrl.Refresh();
        }
    }

    public void OnOpenBlackboxSubPanel()
    {
        if (blackboxSubPanel != null)
        {
            blackboxSubPanel.SetActive(true);
            blackboxSubPanel.transform.SetAsLastSibling();

            var ctrl = blackboxSubPanel.GetComponentInChildren<ForensicBlackboxController>(true);
            if (ctrl != null) ctrl.RefreshAll();
        }
    }

    public void CloseAllSubPanels()
    {
        if (identitySubPanel != null) identitySubPanel.SetActive(false);
        if (blackboxSubPanel != null) blackboxSubPanel.SetActive(false);
    }

    // --- RECOVERY ACTUATOR: SHIFT-BY-WIRE PARK-LOCK RELEASE ---
    public void OnToggleShiftByWire()
    {
        isTransmissionInNeutral = !isTransmissionInNeutral;

        if (FakeECU.Instance != null)
        {
            string action = isTransmissionInNeutral ? "ShiftByWire_ParkLock_Released_To_Neutral" : "ShiftByWire_ParkLock_Engaged";
            FakeECU.Instance.LogECUEvent(action, true, 0, "SbW_Actuator", "POLICE_FORENSIC", "FlatbedRecoveryWinch");
        }

        if (DemoManager.Instance != null)
        {
            string msg = isTransmissionInNeutral
                ? "SHIFT-BY-WIRE: PARK-LOCK FREED TO NEUTRAL FOR TOWING."
                : "SHIFT-BY-WIRE: PARK-LOCK RE-ENGAGED.";
            DemoManager.Instance.ShowSuccessPopup(msg);
        }

        UpdateShiftByWireUI();
    }

    private void UpdateShiftByWireUI()
    {
        if (shiftByWireStatusText != null)
        {
            shiftByWireStatusText.text = isTransmissionInNeutral
                ? "TRANSMISSION: <color=#00FF77>FREED (NEUTRAL FOR TOWING)</color>"
                : "TRANSMISSION: <color=#FFA500>LOCKED IN PARK (PARK-LOCK ENGAGED)</color>";
        }

        // NO EMOJIS, NO BRACKETED "(FLATBED RECOVERY)" TEXT
        if (shiftByWireButtonText != null)
        {
            shiftByWireButtonText.text = isTransmissionInNeutral
                ? "RE-ENGAGE PARK-LOCK"
                : "RELEASE PARK-LOCK TO NEUTRAL";
        }
    }

    public void OnExitForensicHub()
    {
        CloseAllSubPanels();
        if (DemoManager.Instance != null)
            DemoManager.Instance.CloseAuthorisedForensicPanel();
        else
            gameObject.SetActive(false);
    }
}