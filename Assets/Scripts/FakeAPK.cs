using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Security.Cryptography;

public class FakeAPK : EditorWindow
{
    // ============================================================================================================
    //  MASTER OWNER APK - SIMULATED MOBILE DEVICE & WIRELESS TELEMATICS CONTROLLER
    //  (ACCOUNT-BOUND ENCLAVE, 4-DIGIT VAULT PIN & OFFLINE RECOVERY KEY LIFECYCLE)
    // ============================================================================================================

    [MenuItem("QAL/Master Owner APK")]
    [MenuItem("Window/QAL/Master Owner APK")]
    public static void ShowWindow()
    {
        FakeAPK window = GetWindow<FakeAPK>("Master Owner APK");
        window.minSize = new Vector2(380, 600);
        window.maxSize = new Vector2(1600, 1600); // Expanded to allow full-screen maximizing
        window.Show();
    }

    public static FakeAPK Instance;

    // Persistent Account & Device Identity
    private static string deviceId = "APK-DEV-9156";
    private static string myUserId = "";
    private static string linkedVin = "QAL-DEMO-VIN-0001";
    private static bool isPaired = false;

    // Master Key State & Privacy Masking
    private static string storedMasterKey = "";
    private static string masterDeliveryChannel = "None";
    private static bool isMasterKeyVisible = false;

    // 4-Digit In-App Vault PIN & Session Lock
    private static string storedVaultPin = "";
    private static string inputVaultPin = "";
    private static string confirmVaultPin = "";
    private static string vaultPinError = "";
    private static bool isVaultUnlocked = false;
    private static bool isSettingUpVaultPin = false;

    // Master Offline Recovery Key (Tier 1 Disaster Recovery)
    private static string storedRecoveryKey = "";
    private static bool isRecoveryModalOpen = false;
    private static string inputRecoveryKey = "";
    private static string newRecoveredPassword = "";
    private static string recoveryStatusNotice = "";

    // Delegated Guest Key Model
    [System.Serializable]
    public class APKGuestKey
    {
        public string SlotId;
        public string GuestLabel;
        public string KeyPassword;
        public string ExpiresTimestampUTC;
        public bool IsRevoked;
    }

    [System.Serializable]
    private class APKGuestContainer
    {
        public List<APKGuestKey> keys = new List<APKGuestKey>();
    }

    private static List<APKGuestKey> storedGuestKeys = new List<APKGuestKey>();
    private static HashSet<string> revealedGuestPinSlots = new HashSet<string>();

    // In-App Guest Creation State
    private bool isCreatingGuestInApp = false;
    private string inAppGuestName = "";
    private int inAppGuestDurationIndex = 0; // 0=24h, 1=7d, 2=30d
    private readonly string[] inAppDurationLabels = new string[] { "24 Hours (1 Day)", "7 Days (1 Week)", "30 Days (1 Month)" };
    private readonly int[] inAppDurationHours = new int[] { 24, 168, 720 };
    private string inAppGuestFeedback = "";

    // Contact Book Model
    [System.Serializable]
    public class ContactEntry
    {
        public string ContactName;
        public string UserId;
    }

    [System.Serializable]
    private class ContactContainer
    {
        public List<ContactEntry> contacts = new List<ContactEntry>();
    }

    private static List<ContactEntry> contactBook = new List<ContactEntry>();
    private string newContactName = "";
    private string newContactUserId = "";

    // Sharing Dialog State
    private bool isShareModalOpen = false;
    private APKGuestKey selectedKeyToShare = null;
    private string shareRecipientUserId = "";
    private string shareStatusNotice = "";

    // Pairing Request Modal State
    private static bool hasPendingPairingRequest = false;
    private static string pendingUserId = "";
    private static string pendingDeviceId = "";
    private static string pendingAppVersion = "";
    private static string pendingToken = "";

    // Remote Factory Reset State
    private enum ResetStage { Idle, Stage1_Warning, Stage2_Confirm, Stage3_Password, Stage4_FinalConfirm, Stage5_WipingProgress }
    private static ResetStage currentResetStage = ResetStage.Idle;
    private static string resetPasswordInput = "";
    private static string resetErrorMessage = "";
    private static float wipeProgress = 0f;
    private static string wipeStatusText = "";

    // UI Tabs: 0 = Master Key, 1 = Guest Passes, 2 = Contacts, 3 = Security & Recovery
    private int selectedTab = 0;
    private Vector2 scrollPos;

    void OnEnable()
    {
        Instance = this;
        LoadPhoneState();
    }

    void OnFocus()
    {
        LoadPhoneState();
        SyncGuestKeysFromECU();
    }

    private static void LoadPhoneState()
    {
        if (!EditorPrefs.HasKey("APK_MY_USER_ID"))
        {
            myUserId = "USER-" + UnityEngine.Random.Range(1000, 9999);
            EditorPrefs.SetString("APK_MY_USER_ID", myUserId);
        }
        else
        {
            myUserId = EditorPrefs.GetString("APK_MY_USER_ID");
        }

        isPaired = EditorPrefs.GetBool("APK_PAIRED_STATE", false);
        storedMasterKey = EditorPrefs.GetString("APK_STORED_MASTER_KEY", "");
        masterDeliveryChannel = EditorPrefs.GetString("APK_MASTER_DELIVERY", "None");
        linkedVin = EditorPrefs.GetString("APK_LINKED_VIN", "QAL-DEMO-VIN-0001");
        storedVaultPin = EditorPrefs.GetString("APK_VAULT_PIN", "");
        storedRecoveryKey = EditorPrefs.GetString("APK_RECOVERY_KEY", "");

        string guestJson = EditorPrefs.GetString("APK_GUEST_KEYS_JSON", "");
        if (!string.IsNullOrEmpty(guestJson))
        {
            try { storedGuestKeys = JsonUtility.FromJson<APKGuestContainer>(guestJson).keys; } catch { }
        }

        string contactJson = EditorPrefs.GetString("APK_CONTACT_BOOK_JSON", "");
        if (!string.IsNullOrEmpty(contactJson))
        {
            try { contactBook = JsonUtility.FromJson<ContactContainer>(contactJson).contacts; } catch { }
        }
        else if (contactBook.Count == 0)
        {
            contactBook.Add(new ContactEntry { ContactName = "Sarah (Family)", UserId = "USER-GUEST-4421" });
            contactBook.Add(new ContactEntry { ContactName = "Airport Valet Service", UserId = "USER-VALET-9088" });
            SaveContacts();
        }
    }

    private static void SavePhoneState()
    {
        EditorPrefs.SetBool("APK_PAIRED_STATE", isPaired);
        EditorPrefs.SetString("APK_STORED_MASTER_KEY", storedMasterKey);
        EditorPrefs.SetString("APK_MASTER_DELIVERY", masterDeliveryChannel);
        EditorPrefs.SetString("APK_LINKED_VIN", linkedVin);
        EditorPrefs.SetString("APK_VAULT_PIN", storedVaultPin);
        EditorPrefs.SetString("APK_RECOVERY_KEY", storedRecoveryKey);

        APKGuestContainer gContainer = new APKGuestContainer { keys = storedGuestKeys };
        EditorPrefs.SetString("APK_GUEST_KEYS_JSON", JsonUtility.ToJson(gContainer));
    }

    private static void SaveContacts()
    {
        ContactContainer cContainer = new ContactContainer { contacts = contactBook };
        EditorPrefs.SetString("APK_CONTACT_BOOK_JSON", JsonUtility.ToJson(cContainer));
    }

    public static void ReceivePairingRequest(string uId, string devId, string appVer, string token)
    {
        hasPendingPairingRequest = true;
        pendingUserId = uId;
        pendingDeviceId = devId;
        pendingAppVersion = appVer;
        pendingToken = token;
        deviceId = devId;
        if (Instance != null) Instance.Repaint();
    }

    public static void ReceiveKeyFromVehicle(string keyPayload, string method)
    {
        storedMasterKey = keyPayload;
        masterDeliveryChannel = method;
        isMasterKeyVisible = false;

        // Automatically trigger 4-digit Vault PIN creation if not set yet
        if (string.IsNullOrEmpty(storedVaultPin))
        {
            isSettingUpVaultPin = true;
        }

        SavePhoneState();
        if (Instance != null) Instance.Repaint();
    }

    public static void ReceiveGuestKey(string slotId, string label, string password, string expiresUtc)
    {
        storedGuestKeys.RemoveAll(k => k.SlotId == slotId);
        storedGuestKeys.Add(new APKGuestKey { SlotId = slotId, GuestLabel = label, KeyPassword = password, ExpiresTimestampUTC = expiresUtc, IsRevoked = false });
        SavePhoneState();
        if (Instance != null) Instance.Repaint();
    }

    public static void Unlink()
    {
        isPaired = false;
        storedMasterKey = "";
        masterDeliveryChannel = "None";
        isMasterKeyVisible = false;
        storedVaultPin = "";
        storedRecoveryKey = "";
        isVaultUnlocked = false;
        isSettingUpVaultPin = false;
        isRecoveryModalOpen = false;
        storedGuestKeys.Clear();
        revealedGuestPinSlots.Clear();
        hasPendingPairingRequest = false;
        currentResetStage = ResetStage.Idle;
        SavePhoneState();
        if (Instance != null) Instance.Repaint();
    }

    private void SyncGuestKeysFromECU()
    {
        if (isPaired && FakeECU.Instance != null && FakeECU.Instance.TemporaryKeySlots != null)
        {
            storedGuestKeys.Clear();
            foreach (var slot in FakeECU.Instance.TemporaryKeySlots)
            {
                storedGuestKeys.Add(new APKGuestKey
                {
                    SlotId = slot.SlotId,
                    GuestLabel = slot.GuestLabel,
                    KeyPassword = slot.KeyPassword,
                    ExpiresTimestampUTC = slot.ExpiresTimestampUTC,
                    IsRevoked = slot.IsRevoked
                });
            }
            SavePhoneState();
        }
    }

    void OnGUI()
    {
        // Safe expanding scrollview for any window dimension
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        DrawHeader();

        if (hasPendingPairingRequest)
            DrawPairingModal();
        else if (isSettingUpVaultPin)
            DrawVaultPinSetupModal();
        else if (isRecoveryModalOpen)
            DrawRecoveryKeyModal();
        else if (currentResetStage != ResetStage.Idle)
            DrawRemoteResetSequence();
        else if (isShareModalOpen)
            DrawKeyTransferModal();
        else if (isPaired)
            DrawPairedDashboard();
        else
            DrawUnpairedScreen();

        EditorGUILayout.EndScrollView();
    }

    // ============================================================================================================
    //  HEADER DISPLAY
    // ============================================================================================================
    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("<b>MASTER OWNER APP</b>", GetRichTextStyle());
        GUILayout.FlexibleSpace();
        bool ble = FakeECU.Instance != null && FakeECU.Instance.IsBluetoothActive;
        GUILayout.Label(ble ? "<color=#2986CB><b>[BLE ON]</b></color>" : "<color=#888888>[BLE OFF]</color>", GetRichTextStyle());
        EditorGUILayout.EndHorizontal();

        GUILayout.Label($"Account ID: <b><color=yellow>{myUserId}</color></b> | Vehicle: <b>{(isPaired ? linkedVin : "Not Linked")}</b>", GetRichTextStyle());
        EditorGUILayout.EndVertical();
        GUILayout.Space(6);
    }

    private void DrawUnpairedScreen()
    {
        GUILayout.Space(30);
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("Vehicle Disconnected", EditorStyles.boldLabel);
        GUILayout.Label("1. Open in-car Owner Mode.\n2. Tap 'Link to User Device'.\n3. Accept pairing request on this screen.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(15);
        if (GUILayout.Button("Refresh Status", GUILayout.Height(32))) LoadPhoneState();
        EditorGUILayout.EndVertical();
    }

    private void DrawPairingModal()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("INCOMING PAIRING REQUEST", EditorStyles.boldLabel);
        GUILayout.Label("Vehicle is attempting to link with your account:", EditorStyles.wordWrappedLabel);
        GUILayout.Space(6);
        GUILayout.Label($"Handshake PIN: <b><size=16><color=yellow>{pendingToken}</color></size></b>", GetRichTextStyle());
        GUILayout.Label($"Device ID: {pendingDeviceId}", GetRichTextStyle());
        GUILayout.Space(12);

        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("ACCEPT & LINK", GUILayout.Height(35)))
        {
            isPaired = true;
            hasPendingPairingRequest = false;

            if (string.IsNullOrEmpty(storedVaultPin))
            {
                isSettingUpVaultPin = true;
            }

            SavePhoneState();
            if (FakeECU.Instance != null) FakeECU.Instance.SetLinkedDevice(deviceId);
            if (OwnerModeManager.Instance != null) OwnerModeManager.Instance.OnPhoneAppPairingResponse(true, deviceId);
        }

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("DECLINE", GUILayout.Height(35)))
        {
            hasPendingPairingRequest = false;
            if (OwnerModeManager.Instance != null) OwnerModeManager.Instance.OnPhoneAppPairingResponse(false, deviceId);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // ============================================================================================================
    //  MANDATORY 4-DIGIT VAULT PIN SETUP MODAL
    // ============================================================================================================
    private void DrawVaultPinSetupModal()
    {
        EditorGUILayout.BeginVertical("box");
        GUIStyle header = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.2f, 0.6f, 0.9f) } };
        GUILayout.Label("SECURITY ENCLAVE: SET 4-DIGIT VAULT PIN", header);
        GUILayout.Label("Set a private 4-digit PIN to secure your vehicle master key vault inside this application.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(10);

        inputVaultPin = EditorGUILayout.PasswordField("Enter 4-Digit PIN:", inputVaultPin);
        confirmVaultPin = EditorGUILayout.PasswordField("Confirm 4-Digit PIN:", confirmVaultPin);

        if (!string.IsNullOrEmpty(vaultPinError))
        {
            GUILayout.Space(6);
            GUILayout.Label(vaultPinError, GetRichTextStyle());
        }

        GUILayout.Space(14);
        GUI.backgroundColor = new Color(0.2f, 0.75f, 0.4f);
        if (GUILayout.Button("INITIALIZE SECURE VAULT", GUILayout.Height(36)))
        {
            if (inputVaultPin.Length != 4 || !int.TryParse(inputVaultPin, out _))
            {
                vaultPinError = "<color=red>PIN must be exactly 4 numerical digits (0-9).</color>";
            }
            else if (inputVaultPin != confirmVaultPin)
            {
                vaultPinError = "<color=red>PINs do not match.</color>";
            }
            else
            {
                storedVaultPin = inputVaultPin;
                isVaultUnlocked = true;
                isSettingUpVaultPin = false;
                vaultPinError = "";

                if (string.IsNullOrEmpty(storedRecoveryKey))
                {
                    storedRecoveryKey = "REC-" + UnityEngine.Random.Range(1000, 9999) + "-" + UnityEngine.Random.Range(1000, 9999) + "-" + UnityEngine.Random.Range(1000, 9999);
                }

                SavePhoneState();
                Debug.Log($"<color=#00FF00>[App Enclave]</color> 4-Digit Vault PIN provisioned. Recovery Key: {storedRecoveryKey}");
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();
    }

    // ============================================================================================================
    //  PAIRED DASHBOARD TABS
    // ============================================================================================================
    private void DrawPairedDashboard()
    {
        selectedTab = GUILayout.Toolbar(selectedTab, new string[] { "Master Key", "Guest Passes", "Contacts", "Security & Recovery" }, GUILayout.Height(28));
        GUILayout.Space(8);

        switch (selectedTab)
        {
            case 0: DrawMasterTab(); break;
            case 1: DrawGuestPassesTab(); break;
            case 2: DrawContactsTab(); break;
            case 3: DrawSecurityTab(); break;
        }
    }

    // ============================================================================================================
    //  TAB 0: MASTER KEY (VAULT GATED BEHIND 4-DIGIT PIN OR BIOMETRICS)
    // ============================================================================================================
    private void DrawMasterTab()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("MASTER OWNER KEY ENCLAVE", EditorStyles.boldLabel);

        if (!isVaultUnlocked)
        {
            EditorGUILayout.BeginVertical("helpBox");
            GUILayout.Label("🔒 <b>SECURE VAULT LOCKED</b>", GetRichTextStyle());
            GUILayout.Label("Enter your 4-digit Vault PIN (or use Biometric Unlock) to access transmission controls.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(8);

            inputVaultPin = EditorGUILayout.PasswordField("4-Digit PIN:", inputVaultPin);

            if (!string.IsNullOrEmpty(vaultPinError))
            {
                GUILayout.Label(vaultPinError, GetRichTextStyle());
            }

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
            if (GUILayout.Button("UNLOCK VAULT", GUILayout.Height(30)))
            {
                if (inputVaultPin == storedVaultPin)
                {
                    isVaultUnlocked = true;
                    vaultPinError = "";
                    inputVaultPin = "";
                }
                else
                {
                    vaultPinError = "<color=red>Incorrect 4-Digit PIN.</color>";
                }
            }

            GUI.backgroundColor = new Color(0.25f, 0.7f, 0.45f);
            if (GUILayout.Button("BIOMETRIC (FaceID)", GUILayout.Height(30)))
            {
                isVaultUnlocked = true;
                vaultPinError = "";
                inputVaultPin = "";
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            return;
        }

        // Vault is Unlocked
        if (!string.IsNullOrEmpty(storedMasterKey))
        {
            EditorGUILayout.BeginHorizontal();
            string displayPIN = isMasterKeyVisible ? storedMasterKey : new string('•', storedMasterKey.Length);
            GUILayout.Label($"Key Payload: <b>{displayPIN}</b>", GetRichTextStyle());
            GUILayout.FlexibleSpace();

            GUI.backgroundColor = isMasterKeyVisible ? new Color(0.85f, 0.45f, 0.45f) : new Color(0.35f, 0.65f, 0.85f);
            if (GUILayout.Button(isMasterKeyVisible ? "HIDE" : "SHOW", GUILayout.Width(60), GUILayout.Height(20)))
            {
                isMasterKeyVisible = !isMasterKeyVisible;
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            GUILayout.Label($"Delivery Channel: {masterDeliveryChannel}", GetRichTextStyle());
            GUILayout.Space(12);

            GUI.backgroundColor = new Color(0.16f, 0.65f, 0.35f);
            if (GUILayout.Button("🔊 TRANSMIT MASTER ACOUSTIC KEY (16-17 kHz)", GUILayout.Height(44)))
            {
                try
                {
                    if (QALEncoder.Instance != null)
                    {
                        QALEncoder.Instance.PlayAudio(storedMasterKey);
                    }
                    else
                    {
                        var enc = UnityEngine.Object.FindAnyObjectByType<QALEncoder>();
                        if (enc != null) enc.PlayAudio(storedMasterKey);
                        else Debug.LogWarning("[FakeAPK] QALEncoder not found in active scene.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[FakeAPK Audio Notice] {ex.Message}");
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);
            if (GUILayout.Button("Lock Vault Now", EditorStyles.miniButton))
            {
                isVaultUnlocked = false;
            }
        }
        else
        {
            GUILayout.Label("No Master Key synced.\nLog into in-car Owner Mode and select 'Receive / Deliver Audio Key'.", EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.EndVertical();
    }

    // ============================================================================================================
    //  TAB 1: DELEGATED GUEST PASSES (MASKED PINS & DIRECT CLIPBOARD EXPORT)
    // ============================================================================================================
    private void DrawGuestPassesTab()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("DELEGATED GUEST PASSES", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Sync", GUILayout.Width(55))) SyncGuestKeysFromECU();
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(8);

        // In-App Creation Suite
        EditorGUILayout.BeginVertical("helpBox");
        if (!isCreatingGuestInApp)
        {
            GUI.backgroundColor = new Color(0.18f, 0.65f, 0.35f);
            if (GUILayout.Button("+ Issue New Delegated Guest Pass", GUILayout.Height(32)))
            {
                isCreatingGuestInApp = true;
                inAppGuestName = "";
                inAppGuestFeedback = "";
            }
            GUI.backgroundColor = Color.white;
        }
        else
        {
            GUILayout.Label("<b>ISSUE GUEST DIGITAL PASS (IN-APP)</b>", GetRichTextStyle());
            inAppGuestName = EditorGUILayout.TextField("Guest Driver Name:", inAppGuestName);
            inAppGuestDurationIndex = EditorGUILayout.Popup("Validity Duration:", inAppGuestDurationIndex, inAppDurationLabels);

            if (!string.IsNullOrEmpty(inAppGuestFeedback))
            {
                GUILayout.Space(4);
                GUILayout.Label(inAppGuestFeedback, GetRichTextStyle());
            }

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("CREATE PASS", GUILayout.Height(30)))
            {
                if (FakeECU.Instance == null)
                {
                    inAppGuestFeedback = "<color=red>ECU is offline. Start the simulation to create passes.</color>";
                }
                else
                {
                    string label = string.IsNullOrWhiteSpace(inAppGuestName) ? "Guest Driver" : inAppGuestName.Trim();
                    int hours = inAppDurationHours[inAppGuestDurationIndex];

                    if (FakeECU.Instance.IssueTemporaryKey(label, hours, out string genPw, out string slotId))
                    {
                        if (QALEncoder.Instance != null)
                        {
                            QALEncoder.Instance.GenerateGuestAudio(genPw, label, hours);
                        }

                        // Automatically copy generated PIN to clipboard for out-of-band delivery to guest
                        EditorGUIUtility.systemCopyBuffer = genPw;

                        SyncGuestKeysFromECU();
                        isCreatingGuestInApp = false;
                        inAppGuestFeedback = "";

                        Debug.Log($"<color=#00FF00>[App Enclave]</color> Created Guest Pass: [{slotId}] {label}. 6-Digit PIN <b>{genPw}</b> copied to clipboard.");
                    }
                    else
                    {
                        inAppGuestFeedback = "<color=red>Maximum key slots reached (8 slots full).</color>";
                    }
                }
            }

            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                isCreatingGuestInApp = false;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(8);

        // Guest Passes List with Privacy Masking
        if (storedGuestKeys.Count > 0)
        {
            for (int i = 0; i < storedGuestKeys.Count; i++)
            {
                var guest = storedGuestKeys[i];
                EditorGUILayout.BeginVertical("box");

                string statusTag = guest.IsRevoked
                    ? " <color=#FF4444><b>[REVOKED]</b></color>"
                    : " <color=#00FF77><b>[ACTIVE PASS]</b></color>";

                GUILayout.Label($"<b>[{guest.SlotId}] {guest.GuestLabel}</b> {statusTag}", GetRichTextStyle());

                // Masked PIN display with SHOW/HIDE & COPY buttons
                EditorGUILayout.BeginHorizontal();
                bool isRevealed = revealedGuestPinSlots.Contains(guest.SlotId);
                string maskedPin = isRevealed ? guest.KeyPassword : new string('•', guest.KeyPassword.Length);
                GUILayout.Label($"Fallback PIN: <b><color=yellow><size=12>{maskedPin}</size></color></b>", GetRichTextStyle(), GUILayout.Width(170));

                GUI.backgroundColor = isRevealed ? new Color(0.85f, 0.45f, 0.45f) : new Color(0.35f, 0.65f, 0.85f);
                if (GUILayout.Button(isRevealed ? "HIDE" : "SHOW", GUILayout.Width(50), GUILayout.Height(18)))
                {
                    if (isRevealed) revealedGuestPinSlots.Remove(guest.SlotId);
                    else revealedGuestPinSlots.Add(guest.SlotId);
                }

                GUI.backgroundColor = new Color(0.2f, 0.7f, 0.4f);
                if (GUILayout.Button("Copy PIN", GUILayout.Width(70), GUILayout.Height(18)))
                {
                    EditorGUIUtility.systemCopyBuffer = guest.KeyPassword;
                    Debug.Log($"<color=#00FF00>[App Enclave]</color> PIN for [{guest.GuestLabel}] copied to clipboard.");
                }
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();

                if (DateTime.TryParse(guest.ExpiresTimestampUTC, null, DateTimeStyles.AdjustToUniversal, out DateTime exp))
                {
                    TimeSpan rem = exp - DateTime.UtcNow;
                    if (rem.TotalSeconds > 0)
                    {
                        int totalHours = (int)rem.TotalHours;
                        int minutes = rem.Minutes;
                        GUILayout.Label($"Validity: {totalHours}h {minutes}m remaining (Expires {exp:yyyy-MM-dd HH:mm} UTC)", EditorStyles.wordWrappedLabel);
                    }
                    else
                    {
                        GUILayout.Label("<color=#FF4444><b>EXPIRED</b></color>", GetRichTextStyle());
                    }
                }

                GUILayout.Space(6);
                EditorGUILayout.BeginHorizontal();

                if (!guest.IsRevoked)
                {
                    GUI.backgroundColor = new Color(0.29f, 0.75f, 0.45f);
                    if (GUILayout.Button("Play Audio", GUILayout.Height(24)))
                    {
                        try
                        {
                            if (QALEncoder.Instance != null)
                                QALEncoder.Instance.PlayGuestAudio(guest.KeyPassword);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[FakeAPK Audio Notice] {ex.Message}");
                        }
                    }

                    GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
                    if (GUILayout.Button("Share", GUILayout.Height(24)))
                    {
                        selectedKeyToShare = guest;
                        isShareModalOpen = true;
                        shareRecipientUserId = "";
                        shareStatusNotice = "";
                    }

                    GUI.backgroundColor = new Color(0.9f, 0.4f, 0.2f);
                    if (GUILayout.Button("Revoke", GUILayout.Height(24)))
                    {
                        if (FakeECU.Instance != null) FakeECU.Instance.RevokeTemporaryKey(guest.SlotId);
                        guest.IsRevoked = true;
                        SavePhoneState();
                        SyncGuestKeysFromECU();
                    }
                }
                else
                {
                    GUI.backgroundColor = new Color(0.18f, 0.65f, 0.35f);
                    if (GUILayout.Button("Re-enable", GUILayout.Height(24)))
                    {
                        if (FakeECU.Instance != null) FakeECU.Instance.ReactivateTemporaryKey(guest.SlotId);
                        guest.IsRevoked = false;
                        SavePhoneState();
                        SyncGuestKeysFromECU();
                    }
                }

                // Delete / Remove Profile Button
                GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
                if (GUILayout.Button("Delete", GUILayout.Height(24)))
                {
                    if (FakeECU.Instance != null) FakeECU.Instance.DeleteTemporaryKeySlot(guest.SlotId);
                    revealedGuestPinSlots.Remove(guest.SlotId);
                    storedGuestKeys.RemoveAt(i);
                    SavePhoneState();
                    SyncGuestKeysFromECU();
                    break;
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUILayout.Space(4);
            }
        }
        else
        {
            GUILayout.Label("No guest passes provisioned.\nUse the button above to issue a delegated key.", EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndVertical();
    }

    // ============================================================================================================
    //  TAB 2: CONTACT BOOK
    // ============================================================================================================
    private void DrawContactsTab()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("USER DIRECTORY & CONTACTS", EditorStyles.boldLabel);
        GUILayout.Space(6);

        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("Add New Contact", EditorStyles.boldLabel);
        newContactName = EditorGUILayout.TextField("Contact Name:", newContactName);
        newContactUserId = EditorGUILayout.TextField("User ID (e.g. USER-4421):", newContactUserId);
        if (GUILayout.Button("Save Contact", GUILayout.Height(26)))
        {
            if (!string.IsNullOrEmpty(newContactName) && !string.IsNullOrEmpty(newContactUserId))
            {
                contactBook.Add(new ContactEntry { ContactName = newContactName.Trim(), UserId = newContactUserId.Trim().ToUpper() });
                SaveContacts();
                newContactName = "";
                newContactUserId = "";
            }
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);
        GUILayout.Label("Saved Contacts:", EditorStyles.boldLabel);

        for (int i = 0; i < contactBook.Count; i++)
        {
            var c = contactBook[i];
            EditorGUILayout.BeginHorizontal("box");
            GUILayout.Label($"<b>{c.ContactName}</b>\nID: <color=cyan>{c.UserId}</color>", GetRichTextStyle());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(28)))
            {
                contactBook.RemoveAt(i);
                SaveContacts();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    // ============================================================================================================
    //  TAB 3: SECURITY, DISASTER RECOVERY & REMOTE DE-PROVISIONING
    // ============================================================================================================
    private void DrawSecurityTab()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("VAULT SECURITY & DISASTER RECOVERY", EditorStyles.boldLabel);
        GUILayout.Space(6);

        // Emergency Recovery Key Section
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("🔑 <b>OFFLINE DISASTER RECOVERY KEY</b>", GetRichTextStyle());
        GUILayout.Label("If you forget your master vehicle password, you can use your offline recovery key to reset your master vault without a dealership visit:", EditorStyles.wordWrappedLabel);
        GUILayout.Space(4);

        if (!string.IsNullOrEmpty(storedRecoveryKey))
        {
            GUILayout.Label($"Your Recovery Key: <b><color=yellow><size=13>{storedRecoveryKey}</size></color></b>", GetRichTextStyle());
            GUILayout.Label("<i>(Store this offline securely like a recovery seed phrase).</i>", GetRichTextStyle());
        }

        GUILayout.Space(6);
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
        if (GUILayout.Button("Recover / Reset Master Key via Recovery Key", GUILayout.Height(30)))
        {
            isRecoveryModalOpen = true;
            inputRecoveryKey = "";
            newRecoveredPassword = "";
            recoveryStatusNotice = "";
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();

        GUILayout.Space(12);

        // Remote Factory Wipe Section
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("⚠️ <b>STATUTORY VEHICLE DE-PROVISIONING</b>", GetRichTextStyle());
        GUILayout.Label("Remotely wipes all vehicle credentials, revokes digital passes, and unlinks this device.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(8);

        GUI.backgroundColor = new Color(0.9f, 0.2f, 0.2f);
        if (GUILayout.Button("Remote Factory Reset (Wipe Vehicle)", GUILayout.Height(36)))
        {
            currentResetStage = ResetStage.Stage1_Warning;
            resetPasswordInput = "";
            resetErrorMessage = "";
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndVertical();
    }

    // ============================================================================================================
    //  TIER 1 DISASTER RECOVERY MODAL
    // ============================================================================================================
    private void DrawRecoveryKeyModal()
    {
        EditorGUILayout.BeginVertical("box");
        GUIStyle header = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.2f, 0.6f, 0.9f) } };
        GUILayout.Label("DISASTER RECOVERY: RESET MASTER CREDENTIALS", header);
        GUILayout.Label("Enter your 16-character offline recovery key to re-derive access to your vehicle vault:", EditorStyles.wordWrappedLabel);
        GUILayout.Space(8);

        inputRecoveryKey = EditorGUILayout.TextField("Recovery Key:", inputRecoveryKey);
        newRecoveredPassword = EditorGUILayout.PasswordField("New Master Password:", newRecoveredPassword);

        if (!string.IsNullOrEmpty(recoveryStatusNotice))
        {
            GUILayout.Space(6);
            GUILayout.Label(recoveryStatusNotice, GetRichTextStyle());
        }

        GUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("VERIFY & RE-PROVISION VAULT", GUILayout.Height(32)))
        {
            if (string.Equals(inputRecoveryKey.Trim(), storedRecoveryKey, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(newRecoveredPassword) || newRecoveredPassword.Length < 6)
                {
                    recoveryStatusNotice = "<color=red>New password must be at least 6 characters.</color>";
                }
                else
                {
                    storedMasterKey = newRecoveredPassword;
                    if (FakeECU.Instance != null)
                    {
                        FakeECU.Instance.ForceSetPassword(newRecoveredPassword);
                    }
                    if (QALEncoder.Instance != null)
                    {
                        QALEncoder.Instance.GenerateAudio(newRecoveredPassword);
                    }
                    SavePhoneState();
                    isRecoveryModalOpen = false;
                    Debug.Log("<color=#00FF00>[Disaster Recovery]</color> Master Vault successfully re-provisioned via Offline Recovery Key.");
                }
            }
            else
            {
                recoveryStatusNotice = "<color=red>Invalid Recovery Key.</color>";
            }
        }

        GUI.backgroundColor = Color.white;
        if (GUILayout.Button("Cancel", GUILayout.Height(32)))
        {
            isRecoveryModalOpen = false;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // ============================================================================================================
    //  KEY TRANSFER MODAL (CLOUD & BLE BROADCAST)
    // ============================================================================================================
    private void DrawKeyTransferModal()
    {
        EditorGUILayout.BeginVertical("box");
        GUIStyle title = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.2f, 0.6f, 0.9f) } };
        GUILayout.Label("SECURE DIGITAL KEY DISPATCH", title);
        GUILayout.Space(5);

        GUILayout.Label($"Sending Key: <b>[{selectedKeyToShare.SlotId}] {selectedKeyToShare.GuestLabel}</b>", GetRichTextStyle());
        GUILayout.Label("Role: <b>Drive-Only Interlock</b>", GetRichTextStyle());
        GUILayout.Space(10);

        GUILayout.Label("Select Delivery Channel:", EditorStyles.boldLabel);

        // Method 1: In-App Cloud Dispatch
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("1. In-App Cloud Dispatch (By User ID)", EditorStyles.boldLabel);
        shareRecipientUserId = EditorGUILayout.TextField("Recipient User ID:", shareRecipientUserId);

        if (contactBook.Count > 0)
        {
            GUILayout.Label("Or select from contacts:", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            foreach (var contact in contactBook)
            {
                if (GUILayout.Button(contact.ContactName, EditorStyles.miniButton))
                    shareRecipientUserId = contact.UserId;
            }
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(6);
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
        if (GUILayout.Button("Send Key via Cloud", GUILayout.Height(30)))
        {
            if (!string.IsNullOrEmpty(shareRecipientUserId))
                DispatchKeyViaCloud(selectedKeyToShare, shareRecipientUserId.Trim().ToUpper());
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();

        GUILayout.Space(8);

        // Method 2: Bluetooth BLE Nearby Broadcast
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("2. Bluetooth BLE Nearby Broadcast", EditorStyles.boldLabel);
        GUILayout.Label("Broadcasts key payload to nearby secondary phones over BLE advertising packets.", EditorStyles.wordWrappedLabel);
        GUI.backgroundColor = new Color(0.16f, 0.52f, 0.8f);
        if (GUILayout.Button("Beam Key via Bluetooth BLE", GUILayout.Height(30)))
        {
            DispatchKeyViaBLE(selectedKeyToShare);
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();

        if (!string.IsNullOrEmpty(shareStatusNotice))
        {
            GUILayout.Space(8);
            GUILayout.Label(shareStatusNotice, GetRichTextStyle());
        }

        GUILayout.Space(15);
        if (GUILayout.Button("Close / Back", GUILayout.Height(28)))
            isShareModalOpen = false;

        EditorGUILayout.EndVertical();
    }

    private void DispatchKeyViaCloud(APKGuestKey key, string targetUserId)
    {
        EditorGUIUtility.systemCopyBuffer = key.KeyPassword;
        shareStatusNotice = $"<color=green>PIN for <b>{key.GuestLabel}</b> copied to clipboard. Dispatched to <b>{targetUserId}</b>.</color>";
        Debug.Log($"<color=#00FF00>[P2P Key Dispatch]</color> Key [{key.SlotId}] dispatched to <b>{targetUserId}</b> via Secure Cloud.");
    }

    private void DispatchKeyViaBLE(APKGuestKey key)
    {
        EditorGUIUtility.systemCopyBuffer = key.KeyPassword;
        shareStatusNotice = $"<color=green>Key [{key.SlotId}] beamed over Bluetooth BLE. PIN copied to clipboard.</color>";
        Debug.Log($"<color=#2986CB>[BLE Key Beam]</color> Beamed key [{key.SlotId}] over BLE Nearby Broadcast.");
    }

    // ============================================================================================================
    //  REMOTE FACTORY RESET WORKFLOW
    // ============================================================================================================
    private void DrawRemoteResetSequence()
    {
        EditorGUILayout.BeginVertical("helpBox");
        switch (currentResetStage)
        {
            case ResetStage.Stage1_Warning:
                GUILayout.Label("WARNING: REMOTE FACTORY RESET", EditorStyles.boldLabel);
                GUILayout.Label("Permanently wipes vehicle credentials, revokes digital keys, and unpairs this device.", EditorStyles.wordWrappedLabel);
                GUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Continue", GUILayout.Height(30))) currentResetStage = ResetStage.Stage2_Confirm;
                if (GUILayout.Button("Cancel", GUILayout.Height(30))) currentResetStage = ResetStage.Idle;
                EditorGUILayout.EndHorizontal();
                break;

            case ResetStage.Stage2_Confirm:
                GUILayout.Label("CONFIRM VEHICLE DE-PROVISIONING", EditorStyles.boldLabel);
                GUILayout.Label("Are you sure you want to decouple this device and wipe vehicle memory?", EditorStyles.wordWrappedLabel);
                GUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Yes, Proceed", GUILayout.Height(30))) currentResetStage = ResetStage.Stage3_Password;
                if (GUILayout.Button("Cancel", GUILayout.Height(30))) currentResetStage = ResetStage.Idle;
                EditorGUILayout.EndHorizontal();
                break;

            case ResetStage.Stage3_Password:
                GUILayout.Label("ENTER MASTER OWNER PASSWORD", EditorStyles.boldLabel);
                resetPasswordInput = EditorGUILayout.PasswordField("Password:", resetPasswordInput);
                if (!string.IsNullOrEmpty(resetErrorMessage)) GUILayout.Label(resetErrorMessage, EditorStyles.label);
                GUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Verify", GUILayout.Height(30))) VerifyResetPw();
                if (GUILayout.Button("Cancel", GUILayout.Height(30))) currentResetStage = ResetStage.Idle;
                EditorGUILayout.EndHorizontal();
                break;

            case ResetStage.Stage4_FinalConfirm:
                GUILayout.Label("FINAL WARNING: REMOTE WIPE", EditorStyles.boldLabel);
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("PERFORM REMOTE RESET", GUILayout.Height(35))) StartWipe();
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("Cancel", GUILayout.Height(25))) currentResetStage = ResetStage.Idle;
                break;

            case ResetStage.Stage5_WipingProgress:
                GUILayout.Label("DE-PROVISIONING IN PROGRESS", EditorStyles.boldLabel);
                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 25), wipeProgress, wipeStatusText);
                break;
        }
        EditorGUILayout.EndVertical();
    }

    private void VerifyResetPw()
    {
        string p = resetPasswordInput.Trim();
        if (QALEncoder.Instance != null) p = QALEncoder.Instance.SanitizePasswordPublic(p);

        bool ok = FakeECU.Instance != null && FakeECU.Instance.TryUserPassword(p);
        if (ok)
        {
            resetErrorMessage = "";
            currentResetStage = ResetStage.Stage4_FinalConfirm;
        }
        else
        {
            resetErrorMessage = "<color=red>Incorrect Master Password.</color>";
        }
    }

    private void StartWipe()
    {
        currentResetStage = ResetStage.Stage5_WipingProgress;
        EditorApplication.update += WipeUpdate;
        wipeProgress = 0f;
    }

    private void WipeUpdate()
    {
        wipeProgress += 0.01f;
        wipeStatusText = "Executing statutory ownership de-provisioning...";
        Repaint();

        if (wipeProgress >= 1f)
        {
            EditorApplication.update -= WipeUpdate;
            if (FactoryResetManager.Instance != null) FactoryResetManager.Instance.StartResetFromRemoteAPK();
            else if (FakeECU.Instance != null) FakeECU.Instance.FactoryResetAllData();
            Unlink();
        }
    }

    private GUIStyle GetRichTextStyle() => new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
}