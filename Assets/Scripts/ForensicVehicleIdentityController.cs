using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class ForensicVehicleIdentityController : MonoBehaviour
{
    // ============================================================================================================
    //  FORENSIC & FACTORY VEHICLE IDENTITY CONTROLLER — DUAL-PART DATA POPULATOR (STRICT UPPERCASE)
    // ============================================================================================================

    [Header("Navigation")]
    public Button backToHubButton;

    [Header("1. Basic Vehicle Info (Data Entry Fields Only)")]
    public TextMeshProUGUI brandValueText;
    public TextMeshProUGUI modelValueText;
    public TextMeshProUGUI yearValueText;
    public TextMeshProUGUI vinValueText;
    public TextMeshProUGUI serialValueText;
    public TextMeshProUGUI modeValueText;

    [Header("2. Challenge & Live Crypto State (Data Entry Fields Only)")]
    public TextMeshProUGUI seedValueText;
    public TextMeshProUGUI counterValueText;
    public TextMeshProUGUI lastTokenValueText;
    public TextMeshProUGUI validationResultValueText;
    public TextMeshProUGUI overrideStatusValueText;

    [Header("3. Technician & Factory UDS Metadata (Data Entry Fields Only)")]
    public TextMeshProUGUI hardwareVersionValueText;
    public TextMeshProUGUI softwareVersionValueText;
    public TextMeshProUGUI firmwareBuildValueText;
    public TextMeshProUGUI calibrationIdValueText;
    public TextMeshProUGUI variantValueText;
    public TextMeshProUGUI bootloaderVersionValueText;

    void Awake()
    {
        Refresh();
    }

    void Start()
    {
        if (backToHubButton != null)
        {
            backToHubButton.onClick.RemoveAllListeners();
            backToHubButton.onClick.AddListener(CloseSubPanel);
        }

        Refresh();
    }

    void OnEnable()
    {
        Refresh();
        StartCoroutine(RefreshNextFrameRoutine());
    }

    private IEnumerator RefreshNextFrameRoutine()
    {
        yield return null;
        Refresh();
    }

    public void Refresh()
    {
        var ecu = FakeECU.Instance;
        if (ecu == null) return;

        // -------------------------------------------------------------
        // 1. BASIC VEHICLE DATA (STRICT UPPERCASE)
        // -------------------------------------------------------------
        if (brandValueText != null)
            brandValueText.text = SafeUpper(ecu.VehicleBrand);

        if (modelValueText != null)
            modelValueText.text = SafeUpper(ecu.VehicleModel);

        if (yearValueText != null)
            yearValueText.text = SafeUpper(ecu.VehicleYear);

        if (vinValueText != null)
            vinValueText.text = SafeUpper(ecu.VIN);

        if (serialValueText != null)
            serialValueText.text = SafeUpper(ecu.serialNumber);

        if (modeValueText != null)
            modeValueText.text = ecu.mode.ToString().ToUpperInvariant();

        // -------------------------------------------------------------
        // 2. LIVE CRYPTOGRAPHIC AUTHENTICATION STATE (STRICT UPPERCASE)
        // -------------------------------------------------------------
        if (seedValueText != null)
            seedValueText.text = SafeUpper(ecu.currentSeed);

        if (counterValueText != null)
            counterValueText.text = ecu.FreshnessCounter.ToString();

        if (lastTokenValueText != null)
            lastTokenValueText.text = SafeUpper(ecu.lastTokenReceived);

        if (validationResultValueText != null)
        {
            string res = SafeUpper(ecu.lastValidationResult);
            if (res == "PASS")
                validationResultValueText.text = "<color=#00FF77>PASS</color>";
            else if (res == "FAIL")
                validationResultValueText.text = "<color=#FF4444>FAIL</color>";
            else
                validationResultValueText.text = res;
        }

        if (overrideStatusValueText != null)
        {
            overrideStatusValueText.text = ecu.serviceOverrideActive
                ? "<color=#FFA500>ACTIVE (MAINTENANCE BYPASS)</color>"
                : "<color=#00FF77>INACTIVE (NORMAL)</color>";
        }

        // -------------------------------------------------------------
        // 3. FACTORY & UDS METADATA (ISO 14229 - STRICT UPPERCASE)
        // -------------------------------------------------------------
        if (hardwareVersionValueText != null)
            hardwareVersionValueText.text = SafeUpper(ecu.ECUHardwareVersion);

        if (softwareVersionValueText != null)
            softwareVersionValueText.text = SafeUpper(ecu.ECUSoftwareVersion);

        if (firmwareBuildValueText != null)
            firmwareBuildValueText.text = SafeUpper(ecu.ECUFirmwareBuild);

        if (calibrationIdValueText != null)
            calibrationIdValueText.text = SafeUpper(ecu.ECUCalibrationId);

        if (variantValueText != null)
            variantValueText.text = SafeUpper(ecu.ECUVariant);

        if (bootloaderVersionValueText != null)
            bootloaderVersionValueText.text = SafeUpper(ecu.ECUBootloaderVersion);

        // Force Canvas to update immediately so no placeholder text is shown
        Canvas.ForceUpdateCanvases();
    }

    private string SafeUpper(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "NONE";

        return input.Trim().ToUpperInvariant();
    }

    // ============================================================================================================
    //  NAVIGATION BACK TO FORENSIC HUB (PREVENTS BLANK SCREEN)
    // ============================================================================================================
    public void CloseSubPanel()
    {
        // 1. Search for the parent Forensic Hub Controller
        AuthorisedForensicPanelController hub = GetComponentInParent<AuthorisedForensicPanelController>(true);

        if (hub == null)
        {
            // If not a direct parent in hierarchy, search the scene
            hub = Object.FindAnyObjectByType<AuthorisedForensicPanelController>();
        }

        if (hub != null)
        {
            hub.CloseAllSubPanels(); // Hides identity and blackbox panels cleanly
            hub.RefreshHub();        // Re-energizes hub title, warrant ID, and action buttons
        }
        else
        {
            // Standalone fallback
            gameObject.SetActive(false);
        }
    }

    // ============================================================================================================
    //  CONTEXTUAL POPUP HOOKS (Optional: attach to info buttons or EventTriggers on titles)
    // ============================================================================================================
    public void ShowVINInfo() => DemoInformationPanelController.Instance?.Show("VIN - Vehicle Identification Number bound to the ECU root certificate.");
    public void ShowSerialInfo() => DemoInformationPanelController.Instance?.Show("ECU Serial - Unique silicon ID assigned during assembly.");
    public void ShowSeedInfo() => DemoInformationPanelController.Instance?.Show("Current Challenge Seed - Ephemeral cryptographic random value emitted to prevent replay attacks.");
    public void ShowFreshnessInfo() => DemoInformationPanelController.Instance?.Show("Freshness Counter - Monotonically increasing counter ensuring each acoustic challenge is single-use.");
    public void ShowHardwareInfo() => DemoInformationPanelController.Instance?.Show("Hardware Version - Physical circuit board and ASIC revision (ISO 14229 DID 0xF180).");
    public void ShowCalibrationInfo() => DemoInformationPanelController.Instance?.Show("Calibration ID - Cryptographic hash of parameter tables (DID 0xF183).");
    public void ShowBootloaderInfo() => DemoInformationPanelController.Instance?.Show("Secure Bootloader - Root-of-Trust microcode responsible for validating ECU flash signatures.");
}