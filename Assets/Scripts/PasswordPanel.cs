using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PasswordPanel : MonoBehaviour
{
    // ---------------------------------------------------------
    // UI REFERENCES
    // ---------------------------------------------------------
    // The password workflow is split into two stages:
    //
    //   1. Verify current password (if one exists)
    //   2. Enter and confirm new password
    //
    // The second stage remains hidden until the current password
    // is successfully verified.
    [Header("UI")]
    public GameObject passwordPanelRoot;

    public GameObject stepVerifyRoot;          // Step 1: verify existing password
    public TMP_InputField currentPasswordInput;
    public Button verifyButton;

    public GameObject stepNewPasswordRoot;     // Step 2: enter new password
    public TMP_InputField newPasswordInput;
    public TMP_InputField confirmPasswordInput;
    public Button saveButton;

    public Button closeButton;                 // Close without saving

    // ---------------------------------------------------------
    // UNIFIED ERROR POPUP
    // ---------------------------------------------------------
    public GameObject errorPopup;
    public TextMeshProUGUI errorText;

    // ---------------------------------------------------------
    // INTERNAL STATE
    // ---------------------------------------------------------
    private bool passwordExists = false;
    private bool verified = false;

    // ---------------------------------------------------------
    // UNITY LIFECYCLE: Start()
    // ---------------------------------------------------------
    void Start()
    {
        passwordExists = FakeECU.Instance.IsUserPasswordSet;

        stepVerifyRoot.SetActive(false);
        stepNewPasswordRoot.SetActive(false);
        errorPopup.SetActive(false);

        verifyButton.onClick.AddListener(OnVerifyCurrent);
        saveButton.onClick.AddListener(OnSaveNewPassword);
        closeButton.onClick.AddListener(ClosePanel);

        InitializeFlow();
    }

    // ---------------------------------------------------------
    // INITIAL FLOW SETUP
    // ---------------------------------------------------------
    void InitializeFlow()
    {
        if (!passwordExists)
        {
            // No password exists ? skip verification
            stepVerifyRoot.SetActive(false);
            stepNewPasswordRoot.SetActive(true);
        }
        else
        {
            // Password exists ? verify first
            stepVerifyRoot.SetActive(true);
            stepNewPasswordRoot.SetActive(false);
        }
    }

    // ---------------------------------------------------------
    // VERIFY CURRENT PASSWORD
    // ---------------------------------------------------------
    void OnVerifyCurrent()
    {
        string current = currentPasswordInput.text.ToUpper();

        if (!FakeECU.Instance.TryUserPassword(current))
        {
            ShowError("Incorrect current password.");
            return;
        }

        verified = true;

        stepVerifyRoot.SetActive(false);
        stepNewPasswordRoot.SetActive(true);
    }

    // ---------------------------------------------------------
    // SAVE NEW PASSWORD
    // ---------------------------------------------------------
    void OnSaveNewPassword()
    {
        if (passwordExists && !verified)
        {
            ShowError("Verify your current password first.");
            return;
        }

        string newRaw = newPasswordInput.text;
        string confirmRaw = confirmPasswordInput.text;

        // ---------------------------------------------------------
        // EMPTY PASSWORD
        // ---------------------------------------------------------
        if (string.IsNullOrEmpty(newRaw))
        {
            ShowError("Password cannot be empty.");
            return;
        }

        // ---------------------------------------------------------
        // LENGTH CHECK (5–18 characters)
        // ---------------------------------------------------------
        if (newRaw.Length < 5)
        {
            ShowError("Password is too short.\nMinimum length is 5.");
            return;
        }

        if (newRaw.Length > 18)
        {
            ShowError("Password is too long.\nMaximum length is 18.");
            return;
        }

        // ---------------------------------------------------------
        // INVALID CHARACTERS (A–Z, 0–9 only)
        // ---------------------------------------------------------
        foreach (char c in newRaw)
        {
            bool valid =
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9');

            if (!valid)
            {
                ShowError("Invalid characters used.\nAllowed: A–Z and 0–9.");
                return;
            }
        }

        // ---------------------------------------------------------
        // CONFIRMATION MATCH
        // ---------------------------------------------------------
        if (newRaw != confirmRaw)
        {
            ShowError("Passwords do not match.");
            return;
        }

        // ---------------------------------------------------------
        // UPDATE ECU PASSWORD
        // ---------------------------------------------------------
        bool ok = FakeECU.Instance.ChangeUserPassword(
            passwordExists ? currentPasswordInput.text.ToUpper() : "",
            newRaw.ToUpper()
        );

        if (!ok)
        {
            ShowError("Failed to update ECU password.");
            return;
        }

        ClosePanel();
    }

    // ---------------------------------------------------------
    // DISPLAY ERROR POPUP
    // ---------------------------------------------------------
    void ShowError(string msg)
    {
        errorText.text = msg;
        errorPopup.SetActive(true);
    }

    // ---------------------------------------------------------
    // CLOSE PANEL
    // ---------------------------------------------------------
    void ClosePanel()
    {
        passwordPanelRoot.SetActive(false);
    }
}
