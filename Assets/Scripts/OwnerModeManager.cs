using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

public class OwnerModeManager : MonoBehaviour
{
    // ============================================================================================================
    //  OWNER MODE MANAGER — VEHICLE TELEMATICS & DIGITAL KEY DISTRIBUTION CONTROLLER
    // ============================================================================================================

    public static OwnerModeManager Instance { get; private set; }

    [Header("Panels")]
    public GameObject securityLayerPanel;
    public GameObject ownerModePanel;

    [Header("Security Layer UI")]
    public TMP_InputField passwordInput;
    public TextMeshProUGUI lockoutTimerText;
    public Button submitButton;
    public Button exitButton;

    [Header("Delivery Options Popup")]
    public GameObject deliveryOptionsPopupPanel;
    public Button sendViaBluetoothButton;
    public Button sendViaCloudButton;
    public Button deliveryCancelButton;

    [Header("Owner Mode Exit Button")]
    public Button ownerExitButton;

    [Header("Bluetooth UI")]
    public Button bluetoothButton;
    public TextMeshProUGUI bluetoothButtonText;
    public Image bluetoothIconImage;

    private bool bluetoothActive = false;

    [Header("Audio Listener")]
    public Button audioListenerButton;
    public float audioListenDuration = 30f;
    private Coroutine audioListenRoutine;

    [Header("Mic Indicator")]
    public Image micIndicatorImage;
    public Color micOffColor = new Color32(0xFF, 0x00, 0x00, 0xFF);
    public Color micOnColor = new Color32(0x00, 0xFF, 0x00, 0xFF);

    private UltrasonicDecoder decoder;

    [Header("Owner Mode Information Panel")]
    public OwnerModeInformationPanelController ownerInfo;

    [Header("Phone App Link (Smart Toggle)")]
    public Button phoneAppLinkButton;
    public TextMeshProUGUI phoneAppLinkButtonText;

    [Header("Disable QAL System")]
    public Button disableQALSystemButton;

    [Header("Owner Mode Audio Key")]
    public Button generatePasswordAudioKeyButton;

    [Header("Change Password UI")]
    public GameObject changePasswordPopUpPanel;
    public TMP_InputField newPasswordInputField;
    public Button changePasswordButton;
    public Button passwordConfirmButton;
    public Button passwordCancelButton;

    [Header("Delegated Guest Key Issuance Modal")]
    public GameObject shareKeyModalPanel;
    public TMP_InputField guestNameInputField;
    public TMP_Dropdown guestDurationDropdown;
    public Button openShareKeyModalButton;
    public Button createGuestKeyButton;
    public Button closeShareKeyModalButton;

    [Header("Guest Profiles Management In-Car UI")]
    public Button openGuestProfilesButton;
    public GameObject guestProfilesPanel;
    public Button closeGuestProfilesButton;
    public Transform guestProfileListContentHolder;
    public TextMeshProUGUI noGuestKeysNoticeText;

    [Header("Factory Reset UI")]
    public Button factoryResetButton;

    private float lastQALToggleTime = 0f;
    private Coroutine lockoutCountdownRoutine;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        decoder = UnityEngine.Object.FindAnyObjectByType<UltrasonicDecoder>();
        if (decoder != null)
            decoder.OnOwnerAudioPasswordDecoded += OnAudioPasswordReceived;

        if (securityLayerPanel != null) securityLayerPanel.SetActive(false);
        if (ownerModePanel != null) ownerModePanel.SetActive(false);
        if (deliveryOptionsPopupPanel != null) deliveryOptionsPopupPanel.SetActive(false);
        if (changePasswordPopUpPanel != null) changePasswordPopUpPanel.SetActive(false);
        if (shareKeyModalPanel != null) shareKeyModalPanel.SetActive(false);
        if (guestProfilesPanel != null) guestProfilesPanel.SetActive(false);
        if (lockoutTimerText != null) lockoutTimerText.text = "";

        HideOwnerInfo();

        if (submitButton != null)
        {
            submitButton.onClick.RemoveAllListeners();
            submitButton.onClick.AddListener(OnSubmitPassword);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(OnExitSecurityLayer);
        }

        if (ownerExitButton != null)
        {
            ownerExitButton.onClick.RemoveAllListeners();
            ownerExitButton.onClick.AddListener(OnExitOwnerMode);
        }

        if (bluetoothButton != null)
        {
            bluetoothButton.onClick.RemoveAllListeners();
            bluetoothButton.onClick.AddListener(OnBluetoothButtonPressed);
        }

        if (audioListenerButton != null)
        {
            audioListenerButton.onClick.RemoveAllListeners();
            audioListenerButton.onClick.AddListener(OnAudioListenerPressed);
        }

        if (generatePasswordAudioKeyButton != null)
        {
            generatePasswordAudioKeyButton.onClick.RemoveAllListeners();
            generatePasswordAudioKeyButton.onClick.AddListener(OnGenerateOwnerAudioPasswordPressed);
        }

        if (changePasswordButton != null)
        {
            changePasswordButton.onClick.RemoveAllListeners();
            changePasswordButton.onClick.AddListener(OnChangePasswordPressed);
        }

        if (passwordConfirmButton != null)
        {
            passwordConfirmButton.onClick.RemoveAllListeners();
            passwordConfirmButton.onClick.AddListener(OnConfirmPasswordChange);
        }

        if (passwordCancelButton != null)
        {
            passwordCancelButton.onClick.RemoveAllListeners();
            passwordCancelButton.onClick.AddListener(OnCancelPasswordChange);
        }

        if (phoneAppLinkButton != null)
        {
            phoneAppLinkButton.onClick.RemoveAllListeners();
            phoneAppLinkButton.onClick.AddListener(OnPhoneAppLinkPressed);
        }

        if (disableQALSystemButton != null)
        {
            disableQALSystemButton.onClick.RemoveAllListeners();
            disableQALSystemButton.onClick.AddListener(OnDisableQALSystemPressed);
        }

        if (sendViaBluetoothButton != null)
        {
            sendViaBluetoothButton.onClick.RemoveAllListeners();
            sendViaBluetoothButton.onClick.AddListener(OnSendViaBluetoothPressed);
        }

        if (sendViaCloudButton != null)
        {
            sendViaCloudButton.onClick.RemoveAllListeners();
            sendViaCloudButton.onClick.AddListener(OnSendViaCloudPressed);
        }

        if (deliveryCancelButton != null)
        {
            deliveryCancelButton.onClick.RemoveAllListeners();
            deliveryCancelButton.onClick.AddListener(OnDeliveryOptionsCancelPressed);
        }

        if (openShareKeyModalButton != null)
        {
            openShareKeyModalButton.onClick.RemoveAllListeners();
            openShareKeyModalButton.onClick.AddListener(OnOpenShareKeyModal);
        }

        if (closeShareKeyModalButton != null)
        {
            closeShareKeyModalButton.onClick.RemoveAllListeners();
            closeShareKeyModalButton.onClick.AddListener(OnCloseShareKeyModal);
        }

        if (createGuestKeyButton != null)
        {
            createGuestKeyButton.onClick.RemoveAllListeners();
            createGuestKeyButton.onClick.AddListener(OnCreateGuestKeyPressed);
        }

        if (openGuestProfilesButton != null)
        {
            openGuestProfilesButton.onClick.RemoveAllListeners();
            openGuestProfilesButton.onClick.AddListener(OnOpenGuestProfilesPressed);
        }

        if (closeGuestProfilesButton != null)
        {
            closeGuestProfilesButton.onClick.RemoveAllListeners();
            closeGuestProfilesButton.onClick.AddListener(OnCloseGuestProfilesPressed);
        }

        if (factoryResetButton != null)
        {
            factoryResetButton.onClick.RemoveAllListeners();
            factoryResetButton.onClick.AddListener(OnFactoryResetPressed);
        }

        bluetoothActive = false;

        if (bluetoothIconImage != null)
            bluetoothIconImage.gameObject.SetActive(false);

        if (bluetoothButtonText != null)
            bluetoothButtonText.color = new Color32(0x29, 0x86, 0xCB, 0xFF);

        if (micIndicatorImage != null)
            micIndicatorImage.color = micOffColor;

        UpdatePhoneAppButtonState();
    }

    public void UpdatePhoneAppButtonState()
    {
        if (phoneAppLinkButtonText != null)
        {
            if (FakeECU.Instance != null && FakeECU.Instance.IsDeviceLinked)
            {
                phoneAppLinkButtonText.text = "Unlink Device";
                phoneAppLinkButtonText.color = new Color32(0xFF, 0x00, 0x00, 0xFF);
            }
            else
            {
                phoneAppLinkButtonText.text = "Link Phone App";
                phoneAppLinkButtonText.color = new Color32(0x29, 0x86, 0xCB, 0xFF);
            }
        }
    }

    public void OnPhoneAppLinkPressed()
    {
        if (FakeECU.Instance == null)
        {
            ShowOwnerInfo("ECU unavailable.", 4.5f);
            return;
        }

        if (FakeECU.Instance.IsDeviceLinked)
        {
            string oldDev = FakeECU.Instance.LinkedDeviceId;
            FakeECU.Instance.ClearLinkedDevice();

#if UNITY_EDITOR
            FakeAPK.Unlink();
#endif
            UpdatePhoneAppButtonState();
            ShowOwnerInfo(
                $"<b>[ Mobile Device Unlinked ]</b>\n" +
                $"• Disconnected: <b>{oldDev}</b>\n" +
                "• Wireless digital key delivery deactivated.",
                4.5f
            );
            Debug.Log($"<color=#FFA500>[Owner Mode]</color> Disconnected mobile device {oldDev}");
        }
        else
        {
            string userId = UnityEngine.Random.Range(100000, 999999).ToString();
            string deviceId = "APK-DEV-" + UnityEngine.Random.Range(1000, 9999);
            string appVersion = "1.0.0-sim";
            string pairingToken = UnityEngine.Random.Range(1000, 9999).ToString();

#if UNITY_EDITOR
            FakeAPK.ReceivePairingRequest(userId, deviceId, appVersion, pairingToken);
#endif
            ShowOwnerInfo(
                "<b>[ Pairing Request Dispatched ]</b>\n" +
                $"Device ID: <b>{deviceId}</b> | Handshake PIN: <b>{pairingToken}</b>\n" +
                "<i>Please tap 'ACCEPT' inside the Fake APK window.</i>",
                6.0f
            );
        }
    }

    public void OnPhoneAppPairingResponse(bool accepted, string deviceId)
    {
        if (accepted)
        {
            ShowOwnerInfo(
                "<b>[ Smartphone Linked Successfully ]</b>\n" +
                $"• Device: <b>{deviceId}</b>\n" +
                "• Bluetooth and Cloud key dispatch now enabled.",
                4.5f
            );
        }
        else
        {
            ShowOwnerInfo($"Device <b>{deviceId}</b> declined pairing request.", 4.5f);
        }
        UpdatePhoneAppButtonState();
    }

    public void OnPhoneAppUnlinked()
    {
        ShowOwnerInfo("Mobile device disconnected.", 4.5f);
        UpdatePhoneAppButtonState();
    }

    // ============================================================================================================
    //  IN-CAR GUEST PROFILES MANAGEMENT
    // ============================================================================================================

    public void OnOpenGuestProfilesPressed()
    {
        if (guestProfilesPanel != null)
            guestProfilesPanel.SetActive(true);

        RefreshGuestProfilesUI();
        ShowOwnerInfo("Manage active guest profiles, review 6-digit fallback PINs, extend validity timers, re-enable, or remove profiles.", 5.0f);
    }

    public void OnCloseGuestProfilesPressed()
    {
        if (guestProfilesPanel != null)
            guestProfilesPanel.SetActive(false);

        HideOwnerInfo();
    }

    public void RefreshGuestProfilesUI()
    {
        if (FakeECU.Instance == null) return;

        var slots = FakeECU.Instance.TemporaryKeySlots;
        bool hasKeys = slots != null && slots.Count > 0;

        if (noGuestKeysNoticeText != null)
            noGuestKeysNoticeText.gameObject.SetActive(!hasKeys);

        if (guestProfileListContentHolder != null)
        {
            foreach (Transform child in guestProfileListContentHolder)
                Destroy(child.gameObject);

            if (hasKeys)
            {
                foreach (var slot in slots)
                {
                    CreateInCarGuestRow(slot);
                }
            }
        }
    }

    private void CreateInCarGuestRow(FakeECU.DelegatedKeySlot slot)
    {
        float cardHeight = slot.IsRevoked ? 200f : 280f;

        GameObject row = new GameObject($"Row_{slot.SlotId}", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        row.transform.SetParent(guestProfileListContentHolder, false);

        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0, cardHeight);

        Image cardBg = row.GetComponent<Image>();
        cardBg.color = new Color32(0x22, 0x2A, 0x38, 0xFF);

        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.minHeight = cardHeight;
        rowLayout.preferredHeight = cardHeight;
        rowLayout.flexibleWidth = 1f;

        VerticalLayoutGroup vlg = row.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(24, 24, 18, 18);
        vlg.spacing = 14;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // Info Text Area
        GameObject textObj = new GameObject("InfoText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        textObj.transform.SetParent(row.transform, false);

        float textHeight = slot.IsRevoked ? 100f : 120f;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(0, textHeight);

        LayoutElement textLayout = textObj.GetComponent<LayoutElement>();
        textLayout.minHeight = textHeight;
        textLayout.preferredHeight = textHeight;
        textLayout.flexibleWidth = 1f;

        TextMeshProUGUI txt = textObj.GetComponent<TextMeshProUGUI>();
        txt.fontSize = 17f;
        txt.richText = true;
        txt.lineSpacing = 16f;
        txt.color = Color.white;

        string statusTag = slot.IsRevoked
            ? "<color=#FF4444><b>[REVOKED / CANCELLED]</b></color>"
            : "<color=#00FF77><b>[ACTIVE PASS]</b></color>";

        string timeRemaining = "<color=#FF4444><b>EXPIRED</b></color>";

        if (DateTime.TryParse(slot.ExpiresTimestampUTC, null, DateTimeStyles.AdjustToUniversal, out DateTime exp))
        {
            TimeSpan rem = exp - DateTime.UtcNow;
            if (rem.TotalSeconds > 0)
            {
                int totalHours = (int)rem.TotalHours;
                int minutes = rem.Minutes;
                timeRemaining = $"<color=#00E5FF><b>{totalHours}h {minutes}m remaining</b></color> <color=#9EABB8>(Expires {exp:yyyy-MM-dd HH:mm} UTC)</color>";
            }
            else
            {
                timeRemaining = "<color=#FF4444><b>EXPIRED</b></color>";
            }
        }

        txt.text =
            $"<size=24><b>{slot.SlotId}: {slot.GuestLabel}</b></size>    {statusTag}\n" +
            $"<size=17><color=#B0BEC5>• Role / Permission:</color> <b>Drive-Only (Propulsion Authorization)</b></size>\n" +
            $"<size=19><color=#B0BEC5>• 6-Digit Override PIN:</color>  <b><color=#FFD700><size=22>[ {slot.KeyPassword} ]</size></color></b></size>\n" +
            $"<size=18><color=#B0BEC5>• Validity Countdown:</color>  {timeRemaining}</size>";

        // Action Container
        GameObject actionContainer = new GameObject("ActionContainer", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        actionContainer.transform.SetParent(row.transform, false);

        float actionHeight = slot.IsRevoked ? 50f : 100f;
        RectTransform actionRect = actionContainer.GetComponent<RectTransform>();
        actionRect.sizeDelta = new Vector2(0, actionHeight);

        LayoutElement actionLayout = actionContainer.GetComponent<LayoutElement>();
        actionLayout.minHeight = actionHeight;
        actionLayout.preferredHeight = actionHeight;
        actionLayout.flexibleWidth = 1f;

        VerticalLayoutGroup actionVlg = actionContainer.GetComponent<VerticalLayoutGroup>();
        actionVlg.spacing = 8;
        actionVlg.childControlWidth = true;
        actionVlg.childControlHeight = true;
        actionVlg.childForceExpandWidth = true;
        actionVlg.childForceExpandHeight = true;

        if (slot.IsRevoked)
        {
            GameObject revokedActionsRow = new GameObject("RevokedActionsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            revokedActionsRow.transform.SetParent(actionContainer.transform, false);

            LayoutElement rarLayout = revokedActionsRow.GetComponent<LayoutElement>();
            rarLayout.minHeight = 42f;
            rarLayout.preferredHeight = 42f;
            rarLayout.flexibleWidth = 1f;

            HorizontalLayoutGroup rarHlg = revokedActionsRow.GetComponent<HorizontalLayoutGroup>();
            rarHlg.spacing = 10;
            rarHlg.childControlWidth = true;
            rarHlg.childControlHeight = true;
            rarHlg.childForceExpandWidth = true;
            rarHlg.childForceExpandHeight = true;

            CreateSimpleButton(revokedActionsRow.transform, "🟢 Re-enable Guest Access", new Color32(0x2E, 0x7D, 0x32, 0xFF), () =>
            {
                OnReactivateGuestKey(slot.SlotId);
            }, fontSize: 15f);

            CreateSimpleButton(revokedActionsRow.transform, "🗑️ Remove Profile", new Color32(0x45, 0x5A, 0x64, 0xFF), () =>
            {
                OnDeleteGuestKey(slot.SlotId);
            }, fontSize: 15f);
        }
        else
        {
            GameObject topActionRow = new GameObject("TopActionRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            topActionRow.transform.SetParent(actionContainer.transform, false);

            LayoutElement topActionRowLayout = topActionRow.GetComponent<LayoutElement>();
            topActionRowLayout.minHeight = 40f;
            topActionRowLayout.preferredHeight = 40f;
            topActionRowLayout.flexibleWidth = 1f;

            HorizontalLayoutGroup topHlg = topActionRow.GetComponent<HorizontalLayoutGroup>();
            topHlg.spacing = 10;
            topHlg.childControlWidth = true;
            topHlg.childControlHeight = true;
            topHlg.childForceExpandWidth = true;
            topHlg.childForceExpandHeight = true;

            CreateSimpleButton(topActionRow.transform, "⚠️ Revoke Guest Access", new Color32(0xD3, 0x2F, 0x2F, 0xFF), () =>
            {
                OnRevokeGuestKey(slot.SlotId);
            }, fontSize: 15f);

            CreateSimpleButton(topActionRow.transform, "🗑️ Remove Profile", new Color32(0x45, 0x5A, 0x64, 0xFF), () =>
            {
                OnDeleteGuestKey(slot.SlotId);
            }, fontSize: 15f);

            // Row B: Quick Extension Chips
            GameObject extendRow = new GameObject("ExtendRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            extendRow.transform.SetParent(actionContainer.transform, false);

            LayoutElement extendRowLayout = extendRow.GetComponent<LayoutElement>();
            extendRowLayout.minHeight = 42f;
            extendRowLayout.preferredHeight = 42f;
            extendRowLayout.flexibleWidth = 1f;

            HorizontalLayoutGroup extendHlg = extendRow.GetComponent<HorizontalLayoutGroup>();
            extendHlg.spacing = 8;
            extendHlg.childControlWidth = true;
            extendHlg.childControlHeight = true;
            extendHlg.childForceExpandWidth = true;
            extendHlg.childForceExpandHeight = true;

            CreateSimpleButton(extendRow.transform, "+1h", new Color32(0x19, 0x76, 0xD2, 0xFF), () => { ExtendGuestKey(slot.SlotId, 1); }, fontSize: 16f);
            CreateSimpleButton(extendRow.transform, "+3h", new Color32(0x19, 0x76, 0xD2, 0xFF), () => { ExtendGuestKey(slot.SlotId, 3); }, fontSize: 16f);
            CreateSimpleButton(extendRow.transform, "+12h", new Color32(0x19, 0x76, 0xD2, 0xFF), () => { ExtendGuestKey(slot.SlotId, 12); }, fontSize: 16f);
            CreateSimpleButton(extendRow.transform, "+24h", new Color32(0x19, 0x76, 0xD2, 0xFF), () => { ExtendGuestKey(slot.SlotId, 24); }, fontSize: 16f);
            CreateSimpleButton(extendRow.transform, "+3d", new Color32(0x02, 0x88, 0xD1, 0xFF), () => { ExtendGuestKey(slot.SlotId, 72); }, fontSize: 16f);
            CreateSimpleButton(extendRow.transform, "+7d", new Color32(0x02, 0x88, 0xD1, 0xFF), () => { ExtendGuestKey(slot.SlotId, 168); }, fontSize: 16f);
            CreateSimpleButton(extendRow.transform, "+30d", new Color32(0x00, 0x97, 0xA7, 0xFF), () => { ExtendGuestKey(slot.SlotId, 720); }, fontSize: 16f);
        }
    }

    private void CreateSimpleButton(Transform parent, string label, Color32 color, UnityEngine.Events.UnityAction onClick, float fontSize = 16f)
    {
        GameObject btnObj = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        btnObj.transform.SetParent(parent, false);

        Image btnImg = btnObj.GetComponent<Image>();
        btnImg.color = color;

        Button btn = btnObj.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        LayoutElement le = btnObj.GetComponent<LayoutElement>();
        le.minHeight = 38f;
        le.preferredHeight = 42f;
        le.flexibleWidth = 1f;

        GameObject txtObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(btnObj.transform, false);

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        TextMeshProUGUI t = txtObj.GetComponent<TextMeshProUGUI>();
        t.text = $"<b>{label}</b>";
        t.fontSize = fontSize;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
    }

    public void ExtendGuestKey(string slotId, int hours)
    {
        if (FakeECU.Instance != null && FakeECU.Instance.ExtendTemporaryKey(slotId, hours))
        {
            ShowOwnerInfo($"Guest key [<b>{slotId}</b>] extended by <b>+{hours}h</b>.", 4.5f);
            RefreshGuestProfilesUI();
        }
    }

    public void OnRevokeGuestKey(string slotId)
    {
        if (FakeECU.Instance != null && FakeECU.Instance.RevokeTemporaryKey(slotId))
        {
            ShowOwnerInfo($"Guest key slot [<b>{slotId}</b>] revoked.", 4.5f);
            RefreshGuestProfilesUI();
        }
    }

    public void OnReactivateGuestKey(string slotId)
    {
        if (FakeECU.Instance != null && FakeECU.Instance.ReactivateTemporaryKey(slotId))
        {
            ShowOwnerInfo($"Guest key slot [<b>{slotId}</b>] re-enabled successfully.", 4.5f);
            RefreshGuestProfilesUI();
        }
    }

    public void OnDeleteGuestKey(string slotId)
    {
        if (FakeECU.Instance != null && FakeECU.Instance.DeleteTemporaryKeySlot(slotId))
        {
            ShowOwnerInfo($"Guest profile [<b>{slotId}</b>] removed from vehicle. Compliance metadata archived.", 4.5f);
            RefreshGuestProfilesUI();
        }
    }

    // ============================================================================================================
    //  GUEST KEY ISSUANCE MODAL
    // ============================================================================================================

    public void OnOpenShareKeyModal()
    {
        if (shareKeyModalPanel != null)
            shareKeyModalPanel.SetActive(true);

        if (guestNameInputField != null)
            guestNameInputField.text = "";

        ShowOwnerInfo("Issue a new delegated guest digital key (Drive-Only with 6-digit fallback PIN).", 5.0f);
    }

    public void OnCloseShareKeyModal()
    {
        if (shareKeyModalPanel != null)
            shareKeyModalPanel.SetActive(false);

        HideOwnerInfo();
    }

    public void OnCreateGuestKeyPressed()
    {
        if (FakeECU.Instance == null)
        {
            ShowOwnerInfo("ECU unavailable.", 4.5f);
            return;
        }

        string rawName = guestNameInputField != null ? guestNameInputField.text : "";
        string guestLabel = string.IsNullOrWhiteSpace(rawName) ? "Guest Driver" : rawName.Trim();

        int durationHours = 24;
        if (guestDurationDropdown != null)
        {
            switch (guestDurationDropdown.value)
            {
                case 0: durationHours = 24; break;
                case 1: durationHours = 168; break;
                case 2: durationHours = 720; break;
            }
        }

        bool success = FakeECU.Instance.IssueTemporaryKey(
            guestLabel,
            durationHours,
            out string generatedPassword,
            out string slotId
        );

        if (success)
        {
            if (QALEncoder.Instance != null)
            {
                QALEncoder.Instance.GenerateGuestAudio(generatedPassword, guestLabel, durationHours);
            }

            if (shareKeyModalPanel != null)
                shareKeyModalPanel.SetActive(false);

            ShowOwnerInfo(
                $"<b>[ Guest Digital Key Created ]</b>\n" +
                $"• Slot: <b>{slotId}</b> ({guestLabel})\n" +
                $"• 6-Digit Override PIN: <b>{generatedPassword}</b>\n" +
                $"• Validity: <b>{durationHours}h</b> (Drive-Only Propulsion)",
                6.0f
            );

            Debug.Log($"<color=#00FF00>[Owner Mode]</color> Issued {slotId} ({guestLabel}) | PIN: {generatedPassword}");
        }
        else
        {
            ShowOwnerInfo("Failed to issue key: Slot table full (Max 8 slots).", 4.5f);
        }
    }

    public void OnHoverPhoneAppLinkEnter()
    {
        if (FakeECU.Instance != null && FakeECU.Instance.IsDeviceLinked)
        {
            ShowOwnerInfo(
                "<b>[ Unlink Smartphone Device ]</b>\n" +
                $"• <b>Action:</b> Disconnects paired smartphone (<b>{FakeECU.Instance.LinkedDeviceId}</b>).\n" +
                "• <b>Purpose:</b> Severs wireless BLE and Cloud key delivery without resetting vehicle passwords."
            );
        }
        else
        {
            ShowOwnerInfo(
                "<b>[ Phone App Link ]</b>\n" +
                "• <b>Action:</b> Sends an authentication pairing handshake to the simulated mobile APK.\n" +
                "• <b>Purpose:</b> Requires confirmation in the APK before remote key distribution is unlocked."
            );
        }
    }
    public void OnHoverPhoneAppLinkExit() => HideOwnerInfo();

    public void OnHoverGuestProfilesEnter()
    {
        ShowOwnerInfo(
            "<b>[ View Delegated Guest Profiles ]</b>\n" +
            "• <b>Action:</b> Opens the in-vehicle active guest key manager.\n" +
            "• <b>Purpose:</b> View active driver passes, 6-digit override PINs, extend validity timers, re-enable, or remove profiles."
        );
    }
    public void OnHoverGuestProfilesExit() => HideOwnerInfo();

    public void OnHoverGenerateAudioEnter()
    {
        ShowOwnerInfo(
            "<b>[ Generate Master Audio Key ]</b>\n" +
            "• <b>Action:</b> Synthesizes an ultrasonic audio tone from your master Owner PIN.\n" +
            "• <b>Purpose:</b> Transmits master credentials to authorize ignition and settings."
        );
    }
    public void OnHoverGenerateAudioExit() => HideOwnerInfo();

    public void OnHoverShareKeyEnter()
    {
        ShowOwnerInfo(
            "<b>[ Issue Delegated Guest Key ]</b>\n" +
            "• <b>Action:</b> Opens the guest key issuance modal.\n" +
            "• <b>Purpose:</b> Create time-decayed digital keys and 6-digit fallback PINs for family or valets."
        );
    }
    public void OnHoverShareKeyExit() => HideOwnerInfo();

    public void OnHoverReceiveAudioEnter()
    {
        ShowOwnerInfo(
            "<b>[ Receive / Deliver Audio Key ]</b>\n" +
            "• <b>Action:</b> Opens the <i>Delivery Options</i> popup.\n" +
            "• <b>Purpose:</b> Select whether to beam the key over Bluetooth BLE or upload it to the Cloud."
        );
    }
    public void OnHoverReceiveAudioExit() => HideOwnerInfo();

    public void OnHoverChangePasswordEnter()
    {
        ShowOwnerInfo(
            "<b>[ Change Password ]</b>\n" +
            "• <b>Action:</b> Opens the <i>Change Password</i> modal dialog.\n" +
            "• <b>Purpose:</b> Securely updates the PBKDF2 AES-256 encrypted vault with a new master Owner PIN."
        );
    }
    public void OnHoverChangePasswordExit() => HideOwnerInfo();

    public void OnHoverBluetoothEnter()
    {
        if (bluetoothActive)
        {
            ShowOwnerInfo(
                "<b>[ Deactivate Bluetooth (BLE) ]</b>\n" +
                "• <b>Action:</b> Shuts down the vehicle's wireless BLE transceiver.\n" +
                "• <b>Purpose:</b> Disables remote key beaming and puts wireless link to sleep."
            );
        }
        else
        {
            ShowOwnerInfo(
                "<b>[ Activate Bluetooth (BLE) ]</b>\n" +
                "• <b>Action:</b> Powers on the vehicle's wireless BLE transceiver.\n" +
                "• <b>Purpose:</b> Enables paired external devices (Fake APK) to receive acoustic tokens over BLE."
            );
        }
    }
    public void OnHoverBluetoothExit() => HideOwnerInfo();

    public void OnHoverAudioListenerEnter()
    {
        ShowOwnerInfo(
            "<b>[ Audio Listener (Microphone) ]</b>\n" +
            "• <b>Action:</b> Opens a 30-second live microphone capture window.\n" +
            "• <b>Purpose:</b> Records near-field sound to decode ultrasonic pulses for vehicle unlock."
        );
    }
    public void OnHoverAudioListenerExit() => HideOwnerInfo();

    public void OnHoverDisableQALSystemEnter()
    {
        if (FakeECU.Instance != null && FakeECU.Instance.CurrentImmobilizerMode == FakeECU.ImmobilizerMode.Disabled)
        {
            ShowOwnerInfo(
                "<b>[ Enable QAL System ]</b>\n" +
                "• <b>Action:</b> Reactivates the acoustic immobilizer security layer.\n" +
                "• <b>Purpose:</b> Restores ECU ultrasonic authentication checks and lockouts."
            );
        }
        else
        {
            ShowOwnerInfo(
                "<b>[ Disable QAL System (Master Bypass) ]</b>\n" +
                "• <b>Action:</b> Completely shuts down the QAL immobilizer.\n" +
                "• <b>Purpose:</b> Demonstrates emergency master bypass; security locks go offline."
            );
        }
    }
    public void OnHoverDisableQALSystemExit() => HideOwnerInfo();

    public void OnHoverFactoryResetEnter()
    {
        ShowOwnerInfo(
            "<b>[ Factory Reset System ]</b>\n" +
            "• <b>Action:</b> Initiates a secure multi-stage vehicle de-provisioning sequence.\n" +
            "• <b>Purpose:</b> Wipes owner credentials, delegated guest keys, and phone pairings while preserving legal forensic blackbox audit data."
        );
    }
    public void OnHoverFactoryResetExit() => HideOwnerInfo();

    public void ShowOwnerInfo(string msg, float autoHideSeconds = 0f)
    {
        if (ownerInfo != null)
        {
            ownerInfo.gameObject.SetActive(true);
            ownerInfo.ShowInfo(msg, autoHideSeconds);
        }
    }

    public void HideOwnerInfo()
    {
        if (ownerInfo != null)
        {
            ownerInfo.HideInfo();
            ownerInfo.gameObject.SetActive(false);
        }
    }

    public void OnExitOwnerMode()
    {
        if (ownerModePanel != null) ownerModePanel.SetActive(false);
        if (changePasswordPopUpPanel != null) changePasswordPopUpPanel.SetActive(false);
        if (deliveryOptionsPopupPanel != null) deliveryOptionsPopupPanel.SetActive(false);
        if (shareKeyModalPanel != null) shareKeyModalPanel.SetActive(false);
        if (guestProfilesPanel != null) guestProfilesPanel.SetActive(false);
        HideOwnerInfo();

        // Enforce lock when exiting Owner Mode if system is active
        if (FakeECU.Instance != null && FakeECU.Instance.CurrentImmobilizerMode != FakeECU.ImmobilizerMode.Disabled)
        {
            FakeECU.Instance.ResetToLocked();
            if (DemoManager.Instance != null)
                DemoManager.Instance.ResetIgnitionUI();
        }
    }

    public void OnExitSecurityLayer()
    {
        if (securityLayerPanel != null) securityLayerPanel.SetActive(false);
        if (ownerModePanel != null) ownerModePanel.SetActive(false);
        HideOwnerInfo();

        // Enforce lock when cancelling security layer if system is active
        if (FakeECU.Instance != null && FakeECU.Instance.CurrentImmobilizerMode != FakeECU.ImmobilizerMode.Disabled)
        {
            FakeECU.Instance.ResetToLocked();
            if (DemoManager.Instance != null)
                DemoManager.Instance.ResetIgnitionUI();
        }
    }

    public void OnFactoryResetPressed()
    {
        if (FactoryResetManager.Instance != null)
        {
            FactoryResetManager.Instance.StartResetFromOwnerMode();
        }
        else
        {
            ShowOwnerInfo("Factory Reset Manager unavailable.", 4.5f);
        }
    }

    public void OnReceiveAudioKeyPressed()
    {
        if (deliveryOptionsPopupPanel != null)
            deliveryOptionsPopupPanel.SetActive(true);

        ShowOwnerInfo("Select your preferred key delivery channel (BLE or Cloud).", 5.0f);
    }

    public void OnDeliveryOptionsCancelPressed()
    {
        if (deliveryOptionsPopupPanel != null)
            deliveryOptionsPopupPanel.SetActive(false);

        ShowOwnerInfo("Delivery options closed.", 3.0f);
    }

    public void OnSendViaBluetoothPressed()
    {
        if (FakeECU.Instance == null)
        {
            ShowOwnerInfo("ECU unavailable.", 4.5f);
            return;
        }

        if (!FakeECU.Instance.IsDeviceLinked)
        {
            ShowOwnerInfo(
                "<b>[ Device Not Linked ]</b>\n" +
                "• Cannot send key payload: No mobile app is paired.\n" +
                "• <i>Please click 'Phone App Link' and accept in the APK first.</i>",
                5.0f
            );
            return;
        }

        if (!FakeECU.Instance.IsBluetoothActive)
        {
            ShowOwnerInfo(
                "<b>[ Bluetooth Inactive ]</b>\n" +
                "• Cannot send key payload: BLE Transceiver is OFF.\n" +
                "• <i>Please activate Bluetooth first in Owner Mode.</i>",
                5.0f
            );
            return;
        }

        string pw = FakeECU.Instance.GetOwnerPasswordForAudio();
        if (string.IsNullOrEmpty(pw))
        {
            ShowOwnerInfo("No active key in memory. Log into Owner Mode to load key.", 5.0f);
            return;
        }

        bool success = FakeECU.Instance.TransmitKeyViaBluetooth(pw);

        if (success)
        {
#if UNITY_EDITOR
            FakeAPK.ReceiveKeyFromVehicle(pw, "Bluetooth");
#endif
            if (deliveryOptionsPopupPanel != null)
                deliveryOptionsPopupPanel.SetActive(false);

            ShowOwnerInfo(
                "<b>[ Key Beamed via Bluetooth ]</b>\n" +
                $"• Payload transmitted to linked device (<b>{FakeECU.Instance.LinkedDeviceId}</b>).",
                5.0f
            );
        }
    }

    public void OnSendViaCloudPressed()
    {
        if (FakeECU.Instance == null)
        {
            ShowOwnerInfo("ECU unavailable.", 4.5f);
            return;
        }

        if (!FakeECU.Instance.IsDeviceLinked)
        {
            ShowOwnerInfo(
                "<b>[ Device Not Linked ]</b>\n" +
                "• Cannot upload key: No mobile app account is registered.\n" +
                "• <i>Please pair your mobile app via 'Phone App Link' first.</i>",
                5.0f
            );
            return;
        }

        string pw = FakeECU.Instance.GetOwnerPasswordForAudio();
        if (string.IsNullOrEmpty(pw))
        {
            ShowOwnerInfo("No active key in memory. Log into Owner Mode to load key.", 5.0f);
            return;
        }

        FakeECU.Instance.TransmitKeyViaCloud(pw);

#if UNITY_EDITOR
        FakeAPK.ReceiveKeyFromVehicle(pw, "Cloud");
#endif
        if (deliveryOptionsPopupPanel != null)
            deliveryOptionsPopupPanel.SetActive(false);

        ShowOwnerInfo(
            "<b>[ Key Uploaded to Cloud ]</b>\n" +
            $"• Digital key pushed to OEM Cloud Server for <b>{FakeECU.Instance.LinkedDeviceId}</b>.",
            5.0f
        );
    }

    public void OnBluetoothButtonPressed()
    {
        bluetoothActive = !bluetoothActive;

        if (FakeECU.Instance != null)
            FakeECU.Instance.SetBluetoothState(bluetoothActive);

        if (bluetoothActive)
        {
            if (bluetoothButtonText != null)
            {
                bluetoothButtonText.text = "Deactivate Bluetooth";
                bluetoothButtonText.color = new Color32(0xFF, 0x00, 0x00, 0xFF);
            }
            if (bluetoothIconImage != null) bluetoothIconImage.gameObject.SetActive(true);
            ShowOwnerInfo("Bluetooth BLE transceiver activated.", 4.0f);
        }
        else
        {
            if (bluetoothButtonText != null)
            {
                bluetoothButtonText.text = "Activate Bluetooth";
                bluetoothButtonText.color = new Color32(0x29, 0x86, 0xCB, 0xFF);
            }
            if (bluetoothIconImage != null) bluetoothIconImage.gameObject.SetActive(false);
            ShowOwnerInfo("Bluetooth BLE transceiver deactivated.", 4.0f);
        }
    }

    public void OnChangePasswordPressed()
    {
        if (changePasswordPopUpPanel != null) changePasswordPopUpPanel.SetActive(true);
        if (newPasswordInputField != null) newPasswordInputField.text = "";
        ShowOwnerInfo("Enter new Owner Mode password.", 5.0f);
    }

    public void OnCancelPasswordChange()
    {
        if (changePasswordPopUpPanel != null) changePasswordPopUpPanel.SetActive(false);
        ShowOwnerInfo("Password change cancelled.", 3.0f);
    }

    public void OnConfirmPasswordChange()
    {
        if (newPasswordInputField == null)
        {
            ShowOwnerInfo("Input field missing.", 4.5f);
            return;
        }

        string raw = newPasswordInputField.text.Trim();
        string sanitized = QALEncoder.Instance != null ? QALEncoder.Instance.SanitizePasswordPublic(raw) : raw.ToUpperInvariant();

        if (string.IsNullOrEmpty(sanitized))
        {
            ShowOwnerInfo("Password cannot be empty.", 4.5f);
            return;
        }

        if (sanitized.Length < 6)
        {
            ShowOwnerInfo("Password must be at least 6 characters.", 4.5f);
            return;
        }

        FakeECU.SetConsumerPassword(sanitized);

        if (QALEncoder.Instance != null)
        {
            QALEncoder.Instance.GenerateAudio(sanitized);
        }

        if (changePasswordPopUpPanel != null)
            changePasswordPopUpPanel.SetActive(false);

        ShowOwnerInfo($"Owner password updated in PBKDF2/AES vault & acoustic key synthesized.", 5.0f);

        if (FakeECU.Instance != null)
            FakeECU.Instance.LogECUEvent("OwnerPasswordChanged", true, 0, "OwnerMode", "MasterOwner", "Owner");
    }

    // ============================================================================================================
    //  SECURITY LAYER: UNIFIED WITH FAKEECU LOCKOUT (PATENT [0096]-[0097])
    // ============================================================================================================
    public void OpenSecurityLayer()
    {
        if (FakeECU.Instance != null && FakeECU.Instance.lockoutActive)
        {
            int remaining = FakeECU.Instance.RemainingCooldownSeconds;
            string formattedTime = remaining >= 60 ? $"{remaining / 60}m {remaining % 60}s" : $"{remaining}s";
            ShowOwnerInfo($"Authentication locked. Cooldown active: {formattedTime}.", 4.5f);

            if (securityLayerPanel != null) securityLayerPanel.SetActive(true);
            StartCoroutine(UpdateLockoutTimerText());
            return;
        }

        if (securityLayerPanel != null) securityLayerPanel.SetActive(true);
        if (ownerModePanel != null) ownerModePanel.SetActive(false);
        if (passwordInput != null) passwordInput.text = "";
        if (lockoutTimerText != null) lockoutTimerText.text = "";

        SetInputEnabled(true);
    }

    void OnSubmitPassword()
    {
        if (FakeECU.Instance == null)
        {
            ShowOwnerInfo("ECU unavailable.", 4.5f);
            return;
        }

        if (FakeECU.Instance.lockoutActive)
        {
            int rem = FakeECU.Instance.RemainingCooldownSeconds;
            string formattedTime = rem >= 60 ? $"{rem / 60}m {rem % 60}s" : $"{rem}s";
            ShowOwnerInfo($"Authentication locked ({formattedTime} remaining).", 4.5f);
            StartCoroutine(UpdateLockoutTimerText());
            return;
        }

        string entered = passwordInput != null ? passwordInput.text.Trim() : "";
        if (QALEncoder.Instance != null)
            entered = QALEncoder.Instance.SanitizePasswordPublic(entered);
        else
            entered = entered.ToUpperInvariant();

        if (string.IsNullOrEmpty(entered))
        {
            ShowOwnerInfo("Password field empty.", 4.5f);
            return;
        }

        // Delegate authentication exclusively to FakeECU PBKDF2/AES Vault (Case-Insensitive)
        bool ok = FakeECU.Instance.TryUserPassword(entered);

        if (ok)
        {
            if (lockoutCountdownRoutine != null)
                StopCoroutine(lockoutCountdownRoutine);

            if (lockoutTimerText != null) lockoutTimerText.text = "";
            if (securityLayerPanel != null) securityLayerPanel.SetActive(false);
            if (ownerModePanel != null) ownerModePanel.SetActive(true);

            UpdatePhoneAppButtonState();
            ShowOwnerInfo("Password accepted.", 4.5f);
        }
        else
        {
            if (FakeECU.Instance.lockoutActive)
            {
                int cd = FakeECU.Instance.RemainingCooldownSeconds;
                string formattedCooldown = cd >= 60 ? $"{cd / 60}m {cd % 60}s" : $"{cd}s";
                ShowOwnerInfo($"3 failed attempts: Cooldown active: {formattedCooldown}.", 5.0f);
                StartCoroutine(UpdateLockoutTimerText());
            }
            else
            {
                ShowOwnerInfo("Incorrect password.", 4.5f);
            }
        }
    }

    IEnumerator UpdateLockoutTimerText()
    {
        SetInputEnabled(false);

        while (FakeECU.Instance != null && FakeECU.Instance.lockoutActive)
        {
            int remaining = FakeECU.Instance.RemainingCooldownSeconds;
            if (lockoutTimerText != null)
                lockoutTimerText.text = FormatSeconds(remaining);

            yield return new WaitForSeconds(1f);
        }

        if (lockoutTimerText != null) lockoutTimerText.text = "";
        SetInputEnabled(true);
        ShowOwnerInfo("Lockout cooldown expired.", 4.5f);
    }

    string FormatSeconds(int seconds)
    {
        int minutes = seconds / 60;
        int secs = seconds % 60;
        return $"{minutes:00}.{secs:00}s";
    }

    void SetInputEnabled(bool enabled)
    {
        if (passwordInput != null) passwordInput.interactable = enabled;
        if (submitButton != null) submitButton.interactable = enabled;
    }

    // ============================================================================================================
    //  AUDIO LISTENER: DELEGATED TO ULTRASONICDECODER (NO MICROPHONE COLLISION)
    // ============================================================================================================
    public void OnAudioListenerPressed()
    {
        if (audioListenRoutine != null)
            return;

        if (micIndicatorImage != null)
            micIndicatorImage.color = micOnColor;

        UltrasonicDecoder dec = UnityEngine.Object.FindAnyObjectByType<UltrasonicDecoder>();
        if (dec != null)
        {
            dec.OnIgnitionStage1Pressed();
        }

        audioListenRoutine = StartCoroutine(AudioListenWindow());
        ShowOwnerInfo("Listening for ultrasonic acoustic token...", 5.0f);
    }

    private IEnumerator AudioListenWindow()
    {
        float remaining = audioListenDuration;
        while (remaining > 0f)
        {
            // If Stage 2 authorized while listening, exit window immediately
            if (FakeECU.Instance != null && FakeECU.Instance.CurrentState == FakeECU.ECUState.Stage2Authorized)
                break;

            remaining -= Time.deltaTime;
            yield return null;
        }

        if (micIndicatorImage != null)
            micIndicatorImage.color = micOffColor;

        audioListenRoutine = null;
    }

    // ============================================================================================================
    //  ACOUSTIC VERIFICATION HANDSHAKE
    // ============================================================================================================
    private void OnAudioPasswordReceived(string tokenSignal)
    {
        if (micIndicatorImage != null)
            micIndicatorImage.color = micOffColor;

        if (audioListenRoutine != null)
        {
            StopCoroutine(audioListenRoutine);
            audioListenRoutine = null;
        }

        // When UltrasonicDecoder succeeds, FakeECU has ALREADY verified the cryptographic HMAC!
        if (FakeECU.Instance != null && FakeECU.Instance.CurrentState == FakeECU.ECUState.Stage2Authorized)
        {
            ShowOwnerInfo("<b>[ Acoustic Authentication Verified ]</b>\n• Master cryptographic proof accepted.\n• Owner Mode Unlocked.", 4.5f);

            // Only transition to Owner Mode panel if the security layer was actively displayed (intended access)
            if (securityLayerPanel != null && securityLayerPanel.activeInHierarchy)
            {
                securityLayerPanel.SetActive(false);
                if (ownerModePanel != null) ownerModePanel.SetActive(true);
            }

            UpdatePhoneAppButtonState();
        }
        else
        {
            ShowOwnerInfo("Acoustic token signature rejected by vehicle ECU.", 4.5f);
        }
    }

    public void OnDisableQALSystemPressed()
    {
        if (Time.unscaledTime - lastQALToggleTime < 0.3f)
            return;

        lastQALToggleTime = Time.unscaledTime;

        if (FakeECU.Instance == null)
            return;

        if (FakeECU.Instance.CurrentImmobilizerMode == FakeECU.ImmobilizerMode.Disabled)
        {
            FakeECU.Instance.EnableImmobilizerSystem();

            // Re-lock the state machine and reset UI so starting requires a fresh token
            FakeECU.Instance.ResetToLocked();
            if (DemoManager.Instance != null)
                DemoManager.Instance.ResetIgnitionUI();

            if (disableQALSystemButton != null)
            {
                TextMeshProUGUI btnText = disableQALSystemButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null) btnText.text = "Disable QAL System";
            }
            ShowOwnerInfo("QAL Immobilizer System enabled and locked.", 4.5f);
            if (ownerModePanel != null) ownerModePanel.SetActive(true);
            return;
        }

        FakeECU.Instance.DisableImmobilizerSystem();
        if (disableQALSystemButton != null)
        {
            TextMeshProUGUI btnText = disableQALSystemButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = "Enable QAL System";
        }
        ShowOwnerInfo("QAL Immobilizer System disabled.", 4.5f);
        if (ownerModePanel != null) ownerModePanel.SetActive(false);
        if (securityLayerPanel != null) securityLayerPanel.SetActive(false);
    }

    public void OnGenerateOwnerAudioPasswordPressed()
    {
        if (FakeECU.Instance == null || !FakeECU.Instance.IsUserPasswordSet)
        {
            ShowOwnerInfo("No owner password set.", 4.5f);
            return;
        }

        string pw = FakeECU.Instance.GetOwnerPasswordForAudio();
        if (string.IsNullOrEmpty(pw))
        {
            ShowOwnerInfo("No active key in memory. Enter password in Owner Mode to load key.", 5.0f);
            return;
        }

        QALEncoder encoder = QALEncoder.Instance != null ? QALEncoder.Instance : UnityEngine.Object.FindAnyObjectByType<QALEncoder>();
        if (encoder == null)
        {
            ShowOwnerInfo("Encoder missing.", 4.5f);
            return;
        }

        encoder.GenerateAudio(pw);
        FakeECU.Instance.LogTemporaryPasswordGenerated(true);
        ShowOwnerInfo($"Owner audio key generated for active session ({pw}).", 4.5f);
    }
}