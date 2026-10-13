using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class PasswordManager : MonoBehaviour
{
    [Header("Buttons")]
    public Button changePasswordButton;
    public Button resetDemoButton;

    [Header("Panel Root")]
    public GameObject changePasswordPanel;

    // STEP 1 — VERIFY CURRENT PASSWORD
    [Header("Step 1 - Verify Current Password")]
    public GameObject changeStep1Root;
    public TMP_InputField currentPasswordInput;
    public Button currentPasswordEnterButton;

    public GameObject currentPasswordErrorPopupRoot;
    public TextMeshProUGUI currentPasswordErrorText;
    public Button currentPasswordErrorConfirmButton;

    // STEP 2 — ENTER NEW PASSWORD
    [Header("Step 2 - Enter New Password")]
    public GameObject changeStep2Root;
    public TMP_InputField changeNewPasswordInput;
    public TMP_InputField changeConfirmPasswordInput;

    public GameObject changeErrorPopupRoot;
    public TextMeshProUGUI changeErrorText;
    public Button changeErrorConfirmButton;

    public Button changeSaveButton;
    public Button changeCloseButton;

    private string cachedVerifiedCurrentPassword = "";
    private bool isSuccessPopupActive = false;

    void Start()
    {
        if (FakeECU.Instance != null && resetDemoButton != null)
        {
            resetDemoButton.gameObject.SetActive(FakeECU.Instance.mode == FakeECU.ECUMode.Demo);
        }

        if (changePasswordButton != null)
        {
            changePasswordButton.onClick.RemoveAllListeners();
            changePasswordButton.onClick.AddListener(OpenChangePasswordPanel);
        }

        if (resetDemoButton != null)
        {
            resetDemoButton.onClick.RemoveAllListeners();
            resetDemoButton.onClick.AddListener(ResetDemoECU);
        }

        if (currentPasswordEnterButton != null)
        {
            currentPasswordEnterButton.onClick.RemoveAllListeners();
            currentPasswordEnterButton.onClick.AddListener(VerifyCurrentPassword);
        }

        if (changeSaveButton != null)
        {
            changeSaveButton.onClick.RemoveAllListeners();
            changeSaveButton.onClick.AddListener(SaveChangedPassword);
        }

        if (changeCloseButton != null)
        {
            changeCloseButton.onClick.RemoveAllListeners();
            changeCloseButton.onClick.AddListener(CloseChangePanel);
        }

        if (currentPasswordErrorConfirmButton != null)
        {
            currentPasswordErrorConfirmButton.onClick.RemoveAllListeners();
            currentPasswordErrorConfirmButton.onClick.AddListener(() => currentPasswordErrorPopupRoot.SetActive(false));
        }

        if (changeErrorConfirmButton != null)
        {
            changeErrorConfirmButton.onClick.RemoveAllListeners();
            changeErrorConfirmButton.onClick.AddListener(OnConfirmMessageAcknowledged);
        }

        HideAllPanels();
        ClearAllFields();
    }

    void HideAllPanels()
    {
        if (changePasswordPanel != null) changePasswordPanel.SetActive(false);
        if (changeStep1Root != null) changeStep1Root.SetActive(false);
        if (changeStep2Root != null) changeStep2Root.SetActive(false);

        if (currentPasswordErrorPopupRoot != null) currentPasswordErrorPopupRoot.SetActive(false);
        if (changeErrorPopupRoot != null) changeErrorPopupRoot.SetActive(false);
    }

    void ClearTMP(TMP_InputField field)
    {
        if (field == null) return;
        field.text = "";
        field.SetTextWithoutNotify("");
        if (field.textComponent != null)
            field.textComponent.text = "";
        field.DeactivateInputField();
    }

    void ClearAllFields()
    {
        ClearTMP(currentPasswordInput);
        ClearTMP(changeNewPasswordInput);
        ClearTMP(changeConfirmPasswordInput);

        cachedVerifiedCurrentPassword = "";
        isSuccessPopupActive = false;

        if (currentPasswordErrorPopupRoot != null) currentPasswordErrorPopupRoot.SetActive(false);
        if (changeErrorPopupRoot != null) changeErrorPopupRoot.SetActive(false);
    }

    public void OpenChangePasswordPanel()
    {
        ClearAllFields();
        if (changePasswordPanel != null) changePasswordPanel.SetActive(true);

        if (FakeECU.Instance != null && !FakeECU.Instance.IsUserPasswordSet)
        {
            if (changeStep1Root != null) changeStep1Root.SetActive(false);
            if (changeStep2Root != null) changeStep2Root.SetActive(true);
        }
        else
        {
            if (changeStep1Root != null) changeStep1Root.SetActive(true);
            if (changeStep2Root != null) changeStep2Root.SetActive(false);
        }
    }

    public void CloseChangePanel()
    {
        if (FakeECU.Instance != null && !FakeECU.Instance.IsUserPasswordSet)
        {
            Debug.Log("<color=red>[Password Manager]</color> Prevented closing change panel because no password is set.");
            return;
        }

        ClearAllFields();
        if (changePasswordPanel != null) changePasswordPanel.SetActive(false);
    }

    // STEP 1 — VERIFY CURRENT PASSWORD
    void VerifyCurrentPassword()
    {
        if (FakeECU.Instance != null && FakeECU.Instance.lockoutActive)
        {
            currentPasswordErrorText.text = $"ECU is locked.\nTry again in {FakeECU.Instance.RemainingCooldownSeconds}s.";
            currentPasswordErrorPopupRoot.SetActive(true);
            return;
        }

        string current = currentPasswordInput.text.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(current) || (FakeECU.Instance != null && !FakeECU.Instance.TryUserPassword(current)))
        {
            currentPasswordErrorText.text = "Incorrect current password.";
            currentPasswordErrorPopupRoot.SetActive(true);
            return;
        }

        cachedVerifiedCurrentPassword = current;
        ClearTMP(currentPasswordInput);
        currentPasswordErrorPopupRoot.SetActive(false);

        changeStep1Root.SetActive(false);
        changeStep2Root.SetActive(true);
    }

    // STEP 2 — SAVE NEW PASSWORD
    void SaveChangedPassword()
    {
        // 1. Case-insensitive normalization
        string newPass = changeNewPasswordInput.text.Trim().ToUpperInvariant();
        string confirmPass = changeConfirmPasswordInput.text.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(newPass) || newPass.Length < 6)
        {
            ShowError("Password must be at least 6 characters.");
            return;
        }

        if (newPass != confirmPass)
        {
            ShowError("New passwords do not match.");
            return;
        }

        // 2. Character set compliance check (Prevents QALEncoder sanitizer mismatches!)
        if (!IsQALCharacterCompliant(newPass))
        {
            ShowError("Password contains invalid characters.\nUse only A-Z, 0-9, and dashes (-).");
            return;
        }

        if (!string.IsNullOrEmpty(cachedVerifiedCurrentPassword) && newPass == cachedVerifiedCurrentPassword)
        {
            ShowError("New password cannot be the same as old password.");
            return;
        }

        // 3. Register with FakeECU
        if (FakeECU.Instance != null)
        {
            // If first-time creation or rotation:
            FakeECU.Instance.RegisterNewMasterPassword(newPass);
        }

        // 4. Show success message (will close panel AFTER user clicks confirm)
        isSuccessPopupActive = true;
        changeErrorText.text = "Password updated successfully.\nOld audio keys revoked.";
        changeErrorPopupRoot.SetActive(true);
    }

    void ShowError(string message)
    {
        isSuccessPopupActive = false;
        changeErrorText.text = message;
        changeErrorPopupRoot.SetActive(true);
    }

    void OnConfirmMessageAcknowledged()
    {
        changeErrorPopupRoot.SetActive(false);

        // If it was a success message, now cleanly close the panel!
        if (isSuccessPopupActive)
        {
            isSuccessPopupActive = false;
            CloseChangePanel();
        }
    }

    // Ensures password only uses characters supported by the acoustic modem protocol
    bool IsQALCharacterCompliant(string password)
    {
        const string ALLOWED = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-";
        foreach (char c in password)
        {
            if (ALLOWED.IndexOf(c) < 0) return false;
        }
        return true;
    }

    void ResetDemoECU()
    {
        if (FakeECU.Instance == null || FakeECU.Instance.mode != FakeECU.ECUMode.Demo)
            return;

        Debug.Log("<color=orange>[Password Manager]</color> Performing FULL DEMO FACTORY RESET...");

        FakeECU.Instance.MasterDemoFactoryReset();

        if (changeStep1Root != null) changeStep1Root.SetActive(false);
        if (changeStep2Root != null) changeStep2Root.SetActive(true);

        CloseChangePanel();

        isSuccessPopupActive = false;
        changeErrorText.text = "ECU Reset Successfully.\nPassword cleared.";
        changeErrorPopupRoot.SetActive(true);
    }
}