using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

public class FakeAPK_Guest : EditorWindow
{
    // ============================================================================================================
    //  GUEST DRIVER MOBILE APP SIMULATOR (SECONDARY USER)
    //  ZERO-KNOWLEDGE PIN AUTHENTICATION & DELEGATED DIGITAL KEY ENCLAVE
    // ============================================================================================================

    [MenuItem("QAL/Guest Driver APK (Secondary)")]
    [MenuItem("Window/QAL/Guest Driver APK (Secondary)")]
    public static void ShowWindow()
    {
        FakeAPK_Guest window = GetWindow<FakeAPK_Guest>("Guest Driver APK");
        window.minSize = new Vector2(380, 580);
        window.maxSize = new Vector2(500, 800);
        window.Show();
    }

    public static FakeAPK_Guest Instance;

    private static string guestUserId = "";
    private static string guestDeviceId = "GUEST-PHONE-5510";

    [System.Serializable]
    public class ReceivedGuestPass
    {
        public string SlotId;
        public string GuestLabel;
        public string KeyPassword;
        public string ExpiresTimestampUTC;
        public string LinkedVIN;
        public string DeliveryMethod;
        public string SenderUserId;
    }

    [System.Serializable]
    private class GuestPassWallet
    {
        public List<ReceivedGuestPass> passes = new List<ReceivedGuestPass>();
    }

    private static List<ReceivedGuestPass> myPassWallet = new List<ReceivedGuestPass>();
    private static ReceivedGuestPass activeLoggedInPass = null;

    // Login Portal State
    private string loginPinInput = "";
    private string loginErrorMessage = "";
    private bool isSessionPinRevealed = false;
    private int failedAttempts = 0;
    private float lockoutTimeRemaining = 0f;
    private double lastLockoutTick = 0;

    private Vector2 scrollPos;

    void OnEnable()
    {
        Instance = this;
        LoadGuestState();
    }

    void OnFocus()
    {
        LoadGuestState();
        VerifyActiveSessionValidity();
        Repaint();
    }

    private static void LoadGuestState()
    {
        if (!EditorPrefs.HasKey("APK_GUEST_USER_ID"))
        {
            guestUserId = "USER-GUEST-" + UnityEngine.Random.Range(1000, 9999);
            EditorPrefs.SetString("APK_GUEST_USER_ID", guestUserId);
        }
        else
        {
            guestUserId = EditorPrefs.GetString("APK_GUEST_USER_ID");
        }

        string json = EditorPrefs.GetString("APK_GUEST_WALLET_JSON", "");
        if (!string.IsNullOrEmpty(json))
        {
            try { myPassWallet = JsonUtility.FromJson<GuestPassWallet>(json).passes; } catch { }
        }

        string activeJson = EditorPrefs.GetString("APK_GUEST_ACTIVE_SESSION_JSON", "");
        if (!string.IsNullOrEmpty(activeJson))
        {
            try { activeLoggedInPass = JsonUtility.FromJson<ReceivedGuestPass>(activeJson); } catch { activeLoggedInPass = null; }
        }
    }

    private static void SaveGuestState()
    {
        GuestPassWallet wallet = new GuestPassWallet { passes = myPassWallet };
        EditorPrefs.SetString("APK_GUEST_WALLET_JSON", JsonUtility.ToJson(wallet));

        if (activeLoggedInPass != null)
        {
            EditorPrefs.SetString("APK_GUEST_ACTIVE_SESSION_JSON", JsonUtility.ToJson(activeLoggedInPass));
        }
        else
        {
            EditorPrefs.DeleteKey("APK_GUEST_ACTIVE_SESSION_JSON");
        }
    }

    // ============================================================================================================
    //  STATIC API: CALLED BY MASTER APK WHEN DISPATCHING / LINKING KEYS
    // ============================================================================================================
    public static void ReceiveTransferredKey(string slotId, string label, string password, string expiresUtc, string vin, string method, string senderId)
    {
        LoadGuestState();

        // Extract pure digits if needed
        string cleanPassword = Regex.Replace(password ?? "", @"[^\d]", "");
        if (string.IsNullOrEmpty(cleanPassword)) cleanPassword = password;

        myPassWallet.RemoveAll(p => p.SlotId == slotId);
        myPassWallet.Insert(0, new ReceivedGuestPass
        {
            SlotId = slotId,
            GuestLabel = label,
            KeyPassword = cleanPassword,
            ExpiresTimestampUTC = expiresUtc,
            LinkedVIN = string.IsNullOrEmpty(vin) ? "QAL-DEMO-VIN-0001" : vin,
            DeliveryMethod = method,
            SenderUserId = senderId
        });

        SaveGuestState();

        if (Instance != null) Instance.Repaint();
        Debug.Log($"<color=#00FF00>[Guest APK]</color> Pass <b>{slotId} ({label})</b> received. Enter PIN to log in.");
    }

    public static void AutoLinkCreatedPass(string slotId, string label, string password, string expiresUtc, string vin, string method, string senderId)
    {
        ReceiveTransferredKey(slotId, label, password, expiresUtc, vin, method, senderId);
    }

    // ============================================================================================================
    //  GUI RENDERING
    // ============================================================================================================
    void OnGUI()
    {
        UpdateLockoutTimer();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawHeader();

        if (activeLoggedInPass != null)
        {
            DrawActiveGuestSession();
        }
        else
        {
            DrawSecurePinLoginPortal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("📱 <b>GUEST DRIVER APPLICATION</b>", GetRichTextStyle());
        GUILayout.Label($"Device ID: <b>{guestDeviceId}</b>  |  Session: <b>{(activeLoggedInPass != null ? "<color=#00FF77>AUTHENTICATED</color>" : "<color=#AAAAAA>LOCKED</color>")}</b>", GetRichTextStyle());
        GUILayout.Label("Role: <b>Delegated Driver (Drive-Only Interlock)</b>", GetRichTextStyle());
        EditorGUILayout.EndVertical();
        GUILayout.Space(6);
    }

    // ============================================================================================================
    //  VIEW 1: ZERO-KNOWLEDGE SECURE PIN LOGIN PORTAL (NO OTHER PINS / PASSES EXPOSED)
    // ============================================================================================================
    private void DrawSecurePinLoginPortal()
    {
        EditorGUILayout.BeginVertical("box");
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.2f, 0.7f, 1f) } };
        GUILayout.Label("🔑 GUEST AUTHENTICATION", titleStyle);
        GUILayout.Label("Enter the 6-digit numeric PIN provided by the vehicle owner to unlock your digital pass:", EditorStyles.wordWrappedLabel);
        GUILayout.Space(12);

        GUI.enabled = (lockoutTimeRemaining <= 0);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Enter PIN:", GUILayout.Width(75));
        loginPinInput = EditorGUILayout.PasswordField(loginPinInput, GUILayout.Height(26));
        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;

        if (lockoutTimeRemaining > 0)
        {
            GUILayout.Space(6);
            GUILayout.Label($"<color=red><b>TOO MANY FAILED ATTEMPTS. LOCKOUT: {Mathf.CeilToInt(lockoutTimeRemaining)}S REMAINING.</b></color>", GetRichTextStyle());
        }
        else if (!string.IsNullOrEmpty(loginErrorMessage))
        {
            GUILayout.Space(6);
            GUILayout.Label(loginErrorMessage, GetRichTextStyle());
        }

        GUILayout.Space(12);

        GUI.backgroundColor = new Color(0.2f, 0.75f, 0.4f);
        if (GUILayout.Button("AUTHENTICATE & UNLOCK KEY", GUILayout.Height(36)))
        {
            if (lockoutTimeRemaining <= 0)
                AttemptPinAuthentication(loginPinInput.Trim());
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndVertical();

        GUILayout.Space(15);
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("<b>Privacy & Anti-Tamper Policy:</b>", GetRichTextStyle());
        GUILayout.Label("• Access credentials are partitioned. Valet and guest operators have zero visibility into other driver keys.\n• 3 consecutive invalid submissions trigger an anti-brute-force freeze.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndVertical();
    }

    private void AttemptPinAuthentication(string pin)
    {
        if (string.IsNullOrEmpty(pin))
        {
            loginErrorMessage = "<color=red>PIN cannot be empty.</color>";
            return;
        }

        // Normalize input: Strip any "GUEST-", spaces, or dashes to isolate the pure digits
        string cleanInputPin = Regex.Replace(pin.Trim(), @"[^\d]", "");

        if (cleanInputPin.Length < 4)
        {
            loginErrorMessage = "<color=red>PIN must be at least 4 to 6 numeric digits.</color>";
            return;
        }

        ReceivedGuestPass foundPass = null;

        // 1. Search directly in FakeECU.Instance.TemporaryKeySlots (DelegatedKeySlot)
        if (FakeECU.Instance != null && FakeECU.Instance.TemporaryKeySlots != null)
        {
            foreach (var slot in FakeECU.Instance.TemporaryKeySlots)
            {
                if (slot.IsRevoked) continue;

                string cleanSlotPin = Regex.Replace(slot.KeyPassword ?? "", @"[^\d]", "");

                if (string.Equals(cleanInputPin, cleanSlotPin, StringComparison.Ordinal) ||
                    string.Equals(pin.Trim(), slot.KeyPassword, StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(slot.ExpiresTimestampUTC, null, DateTimeStyles.AdjustToUniversal, out DateTime exp))
                    {
                        if (DateTime.UtcNow > exp)
                        {
                            HandleFailedLogin("This guest pass has expired.");
                            return;
                        }
                    }

                    foundPass = new ReceivedGuestPass
                    {
                        SlotId = slot.SlotId,
                        GuestLabel = slot.GuestLabel,
                        KeyPassword = cleanSlotPin,
                        ExpiresTimestampUTC = slot.ExpiresTimestampUTC,
                        LinkedVIN = "QAL-DEMO-VIN-0001",
                        DeliveryMethod = "Direct ECU Authentication",
                        SenderUserId = "MASTER-OWNER"
                    };
                    break;
                }
            }
        }

        // 2. Fallback: Search local wallet
        if (foundPass == null && myPassWallet.Count > 0)
        {
            foreach (var p in myPassWallet)
            {
                string cleanWalletPin = Regex.Replace(p.KeyPassword ?? "", @"[^\d]", "");
                if (string.Equals(cleanInputPin, cleanWalletPin, StringComparison.Ordinal) ||
                    string.Equals(pin.Trim(), p.KeyPassword, StringComparison.OrdinalIgnoreCase))
                {
                    foundPass = p;
                    foundPass.KeyPassword = cleanWalletPin;
                    break;
                }
            }
        }

        if (foundPass != null)
        {
            activeLoggedInPass = foundPass;
            SaveGuestState();
            failedAttempts = 0;
            loginErrorMessage = "";
            loginPinInput = "";
            isSessionPinRevealed = false;
            GUI.FocusControl(null);
            Debug.Log($"<color=#00FF00>[Guest APK]</color> Authenticated as: <b>[{foundPass.SlotId}] {foundPass.GuestLabel}</b>");
        }
        else
        {
            HandleFailedLogin("Invalid Guest PIN.");
        }
    }

    private void HandleFailedLogin(string error)
    {
        failedAttempts++;
        if (failedAttempts >= 3)
        {
            lockoutTimeRemaining = 30f;
            lastLockoutTick = EditorApplication.timeSinceStartup;
            loginErrorMessage = "";
        }
        else
        {
            loginErrorMessage = $"<color=red>{error} (Attempt {failedAttempts}/3)</color>";
        }
    }

    private void UpdateLockoutTimer()
    {
        if (lockoutTimeRemaining > 0)
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - lastLockoutTick);
            lastLockoutTick = now;
            lockoutTimeRemaining -= delta;

            if (lockoutTimeRemaining <= 0)
            {
                lockoutTimeRemaining = 0;
                failedAttempts = 0;
            }
            Repaint();
        }
    }

    // ============================================================================================================
    //  VIEW 2: ACTIVE AUTHENTICATED GUEST ENCLAVE
    // ============================================================================================================
    private void DrawActiveGuestSession()
    {
        EditorGUILayout.BeginVertical("box");

        // Active Driver Banner
        EditorGUILayout.BeginHorizontal();
        GUIStyle activeHeader = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.1f, 0.85f, 0.35f) } };
        GUILayout.Label($"🟢 ACTIVE PASS: {activeLoggedInPass.GuestLabel.ToUpper()}", activeHeader);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Log Out", EditorStyles.miniButton, GUILayout.Width(75)))
        {
            activeLoggedInPass = null;
            SaveGuestState();
            isSessionPinRevealed = false;
            return;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(8);

        // Details Container
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label($"Vehicle VIN: <b>{activeLoggedInPass.LinkedVIN}</b>", GetRichTextStyle());
        GUILayout.Label($"Key Slot: <b>{activeLoggedInPass.SlotId}</b>  |  Driver: <b>{activeLoggedInPass.GuestLabel}</b>", GetRichTextStyle());

        // Masked PIN Display
        EditorGUILayout.BeginHorizontal();
        string cleanPin = Regex.Replace(activeLoggedInPass.KeyPassword ?? "", @"[^\d]", "");
        string pinDisplay = isSessionPinRevealed ? cleanPin : new string('•', cleanPin.Length);
        GUILayout.Label($"Fallback In-Car PIN: <b><color=yellow><size=13>{pinDisplay}</size></color></b>", GetRichTextStyle());
        GUILayout.FlexibleSpace();

        GUI.backgroundColor = isSessionPinRevealed ? new Color(0.85f, 0.45f, 0.45f) : new Color(0.35f, 0.65f, 0.85f);
        if (GUILayout.Button(isSessionPinRevealed ? "HIDE" : "SHOW", GUILayout.Width(55), GUILayout.Height(18)))
        {
            isSessionPinRevealed = !isSessionPinRevealed;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // Remaining Duration
        if (DateTime.TryParse(activeLoggedInPass.ExpiresTimestampUTC, null, DateTimeStyles.AdjustToUniversal, out DateTime exp))
        {
            TimeSpan rem = exp - DateTime.UtcNow;
            if (rem.TotalSeconds > 0)
            {
                int hours = (int)rem.TotalHours;
                GUILayout.Label($"Validity: <b>{hours}h {rem.Minutes}m remaining</b> (Expires {exp:yyyy-MM-dd HH:mm} UTC)", GetRichTextStyle());
            }
            else
            {
                GUILayout.Label("<color=red><b>PASS EXPIRED</b></color>", GetRichTextStyle());
            }
        }
        EditorGUILayout.EndVertical();

        GUILayout.Space(14);

        // 🔊 Near-Ultrasonic Transmission Trigger
        GUI.backgroundColor = new Color(0.16f, 0.65f, 0.35f);
        if (GUILayout.Button("🔊 TRANSMIT GUEST ACOUSTIC KEY (16–17 kHz)", GUILayout.Height(46)))
        {
            TransmitAcousticKey(cleanPin);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(6);
        GUILayout.Label("<i>Hold phone near vehicle interior microphone and tap above to authorize Stage 2 ignition.</i>", GetRichTextStyle());

        GUILayout.Space(16);

        // Forget / Clear Session from this phone
        GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
        if (GUILayout.Button("Forget Pass on This Device", GUILayout.Height(24)))
        {
            myPassWallet.RemoveAll(p => p.SlotId == activeLoggedInPass.SlotId);
            activeLoggedInPass = null;
            SaveGuestState();
            isSessionPinRevealed = false;
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndVertical();
    }

    private void TransmitAcousticKey(string pin)
    {
        try
        {
            string cleanDigits = Regex.Replace(pin ?? "", @"[^\d]", "");

            if (QALEncoder.Instance != null)
            {
                QALEncoder.Instance.PlayGuestAudio(cleanDigits);
                Debug.Log($"<color=#00FF00>[Guest APK]</color> Acoustic Key emitted for PIN: <b>{cleanDigits}</b>");
            }
            else
            {
                var encoder = UnityEngine.Object.FindAnyObjectByType<QALEncoder>();
                if (encoder != null) encoder.PlayGuestAudio(cleanDigits);
                else Debug.LogWarning("[Guest APK] QALEncoder not found in active scene.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Guest APK Audio Error] {ex.Message}");
        }
    }

    private void VerifyActiveSessionValidity()
    {
        if (activeLoggedInPass != null && FakeECU.Instance != null && FakeECU.Instance.TemporaryKeySlots != null)
        {
            var slot = FakeECU.Instance.TemporaryKeySlots.Find(s => s.SlotId == activeLoggedInPass.SlotId);
            if (slot == null || slot.IsRevoked)
            {
                Debug.LogWarning($"[Guest APK] Active session [{activeLoggedInPass.SlotId}] was revoked or removed by owner.");
                activeLoggedInPass = null;
                SaveGuestState();
            }
        }
    }

    private GUIStyle GetRichTextStyle() => new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
}