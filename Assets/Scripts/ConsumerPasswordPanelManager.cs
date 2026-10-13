using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text;

// ============================================================================================================
//  CONSUMER PASSWORD PANEL MANAGER (INITIAL ONBOARDING & VAULT INITIALIZATION)
// ============================================================================================================
//
//  PURPOSE:
//  Controls initial master password creation during customer delivery / onboarding.
//  Strictly enforces zero plaintext storage: passes the sanitized PIN to FakeECU.SetConsumerPassword()
//  which derives a 256-bit AES key via PBKDF2 (100,000 iterations) to lock the root vehicleSecret.
//  No plaintext password is ever written to non-volatile disk (PlayerPrefs), satisfying Patent [0100].
//
// ============================================================================================================

public class ConsumerPasswordPanelManager : MonoBehaviour
{
    private const string PASSWORD_SET_KEY = "ConsumerPasswordSet";

    [Header("Input Fields & Error Popup")]
    public TMP_InputField newPasswordField;       // User enters new password
    public TMP_InputField confirmPasswordField;   // User confirms password
    public ErrorPopup errorPopup;                 // UI popup for validation errors

    [Header("Confirmation Popup")]
    public GameObject confirmCancelPopupObject;   // "Are you sure?" confirmation popup
    public Button confirmButton;                  // Confirm password creation button
    public Button cancelButton;                   // Cancel password creation button

    [Header("Scene Loader")]
    public SceneLoader sceneLoader;               // Loads DemoScene after password creation

    private void Awake()
    {
        // Hide confirmation popup on initial startup
        if (confirmCancelPopupObject != null)
            confirmCancelPopupObject.SetActive(false);
    }

    private void Start()
    {
        // Wire buttons if assigned
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmPassword);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelPassword);
        }
    }

    // ============================================================================================================
    //  SAVE BUTTON PRESSED — VALIDATION
    // ============================================================================================================
    public void OnSavePasswordPressed()
    {
        string pw = newPasswordField != null ? newPasswordField.text.Trim() : "";
        string confirm = confirmPasswordField != null ? confirmPasswordField.text.Trim() : "";

        // Basic length and match validation
        if (string.IsNullOrWhiteSpace(pw) || pw.Length < 6 || pw != confirm)
        {
            if (errorPopup != null)
                errorPopup.ShowError("Password must be at least 6 characters and match.");
            return;
        }

        if (errorPopup != null)
            errorPopup.Hide();

        // Show confirmation modal
        if (confirmCancelPopupObject != null)
            confirmCancelPopupObject.SetActive(true);
    }

    // ============================================================================================================
    //  CONFIRM BUTTON — MASTER VAULT INITIALIZATION & AUDIO KEY SYNTHESIS
    // ============================================================================================================
    public void OnConfirmPassword()
    {
        // 1. Safe extraction of raw password
        string rawPassword = newPasswordField != null ? newPasswordField.text.Trim() : "";

        if (string.IsNullOrEmpty(rawPassword))
        {
            if (errorPopup != null)
                errorPopup.ShowError("Password field is empty.");
            return;
        }

        // 2. Safe sanitization (uses QALEncoder if present, otherwise fallback)
        string finalPassword = "";
        if (QALEncoder.Instance != null)
        {
            finalPassword = QALEncoder.Instance.SanitizePasswordPublic(rawPassword);
        }
        else
        {
            finalPassword = FallbackSanitizePassword(rawPassword);
        }

        if (string.IsNullOrEmpty(finalPassword) || finalPassword.Length < 6)
        {
            if (errorPopup != null)
                errorPopup.ShowError("Password must contain at least 6 valid alphanumeric characters.");
            return;
        }

        // 3. Update FakeECU PBKDF2 Vault (Strictly cryptographic — zero plaintext on disk)
        try
        {
            FakeECU.SetConsumerPassword(finalPassword);
            PlayerPrefs.SetInt(PASSWORD_SET_KEY, 1);
            PlayerPrefs.Save();

            Debug.Log($"<color=#00FF00>[Initial Provisioning]</color> Master Owner Vault provisioned via PBKDF2/AES-256.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[ECU Provisioning Error] " + e.Message);
        }

        // 4. Synthesize the first master audio key immediately if encoder is present
        if (QALEncoder.Instance != null)
        {
            QALEncoder.Instance.GenerateAudio(finalPassword);
        }

        // 5. Hide confirmation popup
        if (confirmCancelPopupObject != null)
            confirmCancelPopupObject.SetActive(false);

        // 6. Transition to main automotive demo scene
        if (sceneLoader != null)
        {
            sceneLoader.LoadDemoScene();
        }
        else
        {
            Debug.LogWarning("[Scene Transition] SceneLoader reference missing. Attempting direct SceneManager load...");
            UnityEngine.SceneManagement.SceneManager.LoadScene(1);
        }
    }

    // ============================================================================================================
    //  CANCEL BUTTON
    // ============================================================================================================
    public void OnCancelPassword()
    {
        if (confirmCancelPopupObject != null)
            confirmCancelPopupObject.SetActive(false);
    }

    // ============================================================================================================
    //  INTERNAL FALLBACK SANITIZER
    // ============================================================================================================
    private string FallbackSanitizePassword(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";

        string allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-";
        raw = raw.ToUpper();

        StringBuilder clean = new StringBuilder();
        foreach (char c in raw)
        {
            if (allowed.IndexOf(c) >= 0)
                clean.Append(c);
        }

        return clean.ToString();
    }
}