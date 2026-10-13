using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class AuthorisedTechnicianPanelController : MonoBehaviour
{
    // ============================================================================================================
    //  AUTHORISED TECHNICIAN PANEL (ATP) — WORKSHOP BAY CONTROLLER
    // ============================================================================================================

    [Header("Vehicle Identity & Context Fields")]
    public TextMeshProUGUI manufacturerText;
    public TextMeshProUGUI modelText;
    public TextMeshProUGUI yearText;
    public TextMeshProUGUI vinText;

    [Header("Dual Status Displays")]
    public TextMeshProUGUI statusText;          // Immobilizer Status (Active / Bypass)
    public TextMeshProUGUI lockoutStatusText;   // Lockout Status (Clear / Locked Out Countdown)

    [Header("Workshop Service Controls")]
    public Button toggleQALSystemButton;
    public TextMeshProUGUI toggleQALButtonText;
    public Button clearLockoutButton;
    public Button exitBayButton;

    private bool isResettingLockout = false;

    void Awake()
    {
        // Auto-resolve toggle button child text if unassigned
        if (toggleQALButtonText == null && toggleQALSystemButton != null)
            toggleQALButtonText = toggleQALSystemButton.GetComponentInChildren<TextMeshProUGUI>();

        // Pre-populate immediately on Awake
        Refresh();
    }

    void Start()
    {
        // Wire Toggle Button
        if (toggleQALSystemButton != null)
        {
            toggleQALSystemButton.onClick.RemoveAllListeners();
            toggleQALSystemButton.onClick.AddListener(OnToggleQALSystem);
        }

        // Wire Clear Lockout Button
        if (clearLockoutButton != null)
        {
            clearLockoutButton.onClick.RemoveAllListeners();
            clearLockoutButton.onClick.AddListener(OnClearLockout);
        }

        // Wire Exit Button
        if (exitBayButton != null)
        {
            exitBayButton.onClick.RemoveAllListeners();
            exitBayButton.onClick.AddListener(OnExitBay);
        }

        // Populate immediately on Start
        Refresh();
    }

    void OnEnable()
    {
        isResettingLockout = false;

        // Immediate paint on enable
        Refresh();

        // 1-frame delayed pass to guarantee TextMeshPro redraws if waking up
        StartCoroutine(RefreshNextFrameRoutine());
    }

    private IEnumerator RefreshNextFrameRoutine()
    {
        yield return null;
        Refresh();
    }

    void Update()
    {
        // Live countdown while panel is active on screen
        if (FakeECU.Instance == null || isResettingLockout) return;

        if (FakeECU.Instance.lockoutActive)
        {
            if (lockoutStatusText != null)
            {
                int remaining = FakeECU.Instance.RemainingCooldownSeconds;
                lockoutStatusText.text = $"Lockout Status: <color=#FF4444><b>LOCKED OUT (Time Remaining: {remaining}s)</b></color>";
            }
        }
    }

    public void Refresh()
    {
        if (FakeECU.Instance == null) return;

        // Auto-resolve toggle button child text if it wasn't ready earlier
        if (toggleQALButtonText == null && toggleQALSystemButton != null)
            toggleQALButtonText = toggleQALSystemButton.GetComponentInChildren<TextMeshProUGUI>();

        // 1. Static Metadata Displays
        if (manufacturerText != null)
            manufacturerText.text = $"Manufacturer: <b>{FakeECU.Instance.VehicleBrand}</b>";

        if (modelText != null)
            modelText.text = $"Model: <b>{FakeECU.Instance.VehicleModel}</b>";

        if (yearText != null)
            yearText.text = $"Year: <b>{FakeECU.Instance.VehicleYear}</b>";

        if (vinText != null)
            vinText.text = $"VIN: <b>{FakeECU.Instance.VIN}</b>";

        // 2. Immobilizer State
        bool isBypassed = (FakeECU.Instance.CurrentImmobilizerMode == FakeECU.ImmobilizerMode.Disabled) ||
                          (FakeECU.Instance.CurrentState == FakeECU.ECUState.MaintenanceMode);

        if (statusText != null)
        {
            statusText.text = isBypassed
                ? "Immobilizer Status: <color=#FFA500><b>MAINTENANCE BYPASS (DISABLED)</b></color>"
                : "Immobilizer Status: <color=#00FF77><b>QAL IMMOBILIZER ACTIVE</b></color>";
        }

        // 3. Lockout Status
        if (!isResettingLockout && lockoutStatusText != null)
        {
            if (FakeECU.Instance.lockoutActive)
            {
                int remaining = FakeECU.Instance.RemainingCooldownSeconds;
                lockoutStatusText.text = $"Lockout Status: <color=#FF4444><b>LOCKED OUT (Time Remaining: {remaining}s)</b></color>";
            }
            else
            {
                lockoutStatusText.text = "Lockout Status: <color=#00FF77><b>CLEAR</b></color>";
            }
        }

        // 4. Toggle Button Text
        if (toggleQALButtonText != null)
        {
            toggleQALButtonText.text = isBypassed ? "Enable QAL System" : "Disable QAL System";
        }

        // Force Unity UI Canvas to render the updated text components immediately
        Canvas.ForceUpdateCanvases();
    }

    // --- BUTTON 1: TOGGLE IMMOBILIZER ---
    public void OnToggleQALSystem()
    {
        if (FakeECU.Instance == null) return;

        bool isBypassed = (FakeECU.Instance.CurrentImmobilizerMode == FakeECU.ImmobilizerMode.Disabled) ||
                          (FakeECU.Instance.CurrentState == FakeECU.ECUState.MaintenanceMode);

        if (isBypassed)
        {
            FakeECU.Instance.EnableImmobilizerSystem();
            if (DemoManager.Instance != null)
                DemoManager.Instance.ShowSuccessPopup("QAL Immobilizer System enabled.");
        }
        else
        {
            FakeECU.Instance.DisableImmobilizerSystem();
            if (DemoManager.Instance != null)
                DemoManager.Instance.ShowSuccessPopup("QAL Immobilizer System disabled.");
        }

        Refresh();
    }

    // --- BUTTON 2: CLEAR LOCKOUT ---
    public void OnClearLockout()
    {
        if (FakeECU.Instance == null || isResettingLockout) return;

        StartCoroutine(ResetLockoutRoutine());
    }

    private IEnumerator ResetLockoutRoutine()
    {
        isResettingLockout = true;

        if (clearLockoutButton != null)
            clearLockoutButton.interactable = false;

        if (lockoutStatusText != null)
            lockoutStatusText.text = "Lockout Status: <color=#FFA500><b>RESETTING LOCKOUT...</b></color>";

        yield return new WaitForSeconds(1.2f);

        FakeECU.Instance.ResetLockout();
        FakeECU.Instance.LogECUEvent("LockoutClearedByTechnician", true, 0, "DealerTerminal", "DEALER_LIV_01", "WorkshopSupervisor");

        if (lockoutStatusText != null)
            lockoutStatusText.text = "Lockout Status: <color=#00FF77><b>CLEAR</b></color>";

        if (DemoManager.Instance != null)
            DemoManager.Instance.ShowSuccessPopup("Anti-Tamper Lockout Cleared. Timers reset.");

        if (clearLockoutButton != null)
            clearLockoutButton.interactable = true;

        isResettingLockout = false;
        Refresh();
    }

    // --- BUTTON 3: EXIT BAY ---
    public void OnExitBay()
    {
        if (DemoManager.Instance != null)
            DemoManager.Instance.CloseAuthorisedTechnicianPanel();
        else
            gameObject.SetActive(false);
    }
}