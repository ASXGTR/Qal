using UnityEngine;
using UnityEditor;
using System;

public class FakeAPK_Dealer : EditorWindow
{
    // ============================================================================================================
    //  DEALERSHIP SHOWROOM APK — AUTHORISED RETAILER DEMONSTRATION & VEHICLE PREPARATION APP
    // ============================================================================================================

    [MenuItem("QAL/Dealership Showroom APK")]
    [MenuItem("Window/QAL/Dealership Showroom APK")]
    public static void ShowWindow()
    {
        FakeAPK_Dealer window = GetWindow<FakeAPK_Dealer>("Dealership APK");
        window.minSize = new Vector2(380, 680);
        window.maxSize = new Vector2(440, 850);
        window.Show();
    }

    public static FakeAPK_Dealer Instance;

    // Dealership Session Credentials
    private static string dealerStationId = "DEALER-LIV01";
    private static string staffId = "STAFF-8842";
    private static string targetVin = "QAL-DEMO-VIN-0001";
    private const string SHOWROOM_DEMO_KEY = "DEALER-DEFAULT";

    // Staff Login State
    private static bool isAuthenticated = false;
    private static string inputStationCode = "DEALER-LIV01";
    private static string loginError = "";

    // Showroom Demo Mode State
    private static bool isTransmitting = false;
    private Vector2 scrollPos;

    void OnEnable()
    {
        Instance = this;
        LoadSessionState();
    }

    private static void LoadSessionState()
    {
        dealerStationId = EditorPrefs.GetString("DEALER_APK_STATION_ID", "DEALER-LIV01");
        staffId = EditorPrefs.GetString("DEALER_APK_STAFF_ID", "STAFF-8842");
        isAuthenticated = EditorPrefs.GetBool("DEALER_APK_AUTH_STATE", false);
    }

    private static void SaveSessionState()
    {
        EditorPrefs.SetString("DEALER_APK_STATION_ID", dealerStationId);
        EditorPrefs.SetString("DEALER_APK_STAFF_ID", staffId);
        EditorPrefs.SetBool("DEALER_APK_AUTH_STATE", isAuthenticated);
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        DrawHeader();

        if (!isAuthenticated)
        {
            DrawStaffLogin();
        }
        else
        {
            DrawShowroomConsole();
        }

        EditorGUILayout.EndScrollView();
    }

    // ============================================================================================================
    //  HEADER DISPLAY
    // ============================================================================================================
    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("<b>QAL AUTHORISED DEALERSHIP RETAIL APP</b>", GetRichTextStyle());
        GUILayout.Label("SHOWROOM DEMONSTRATION AND VEHICLE PREPARATION", EditorStyles.miniLabel);
        GUILayout.Space(4);

        if (isAuthenticated)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"STATION: <color=yellow><b>{dealerStationId}</b></color> | REP: <b>{staffId}</b>", GetRichTextStyle());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("LOGOUT", GUILayout.Width(65), GUILayout.Height(18)))
            {
                isAuthenticated = false;
                SaveSessionState();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space(8);
    }

    // ============================================================================================================
    //  STAGE 1: DEALERSHIP STAFF LOGIN
    // ============================================================================================================
    private void DrawStaffLogin()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("DEALERSHIP STAFF AUTHENTICATION", EditorStyles.boldLabel);
        GUILayout.Label("ENTER ACCREDITED DEALERSHIP STATION CODE TO INITIALISE SHOWROOM DEMONSTRATION TOOLS.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(12);

        inputStationCode = EditorGUILayout.TextField("STATION CODE:", inputStationCode);

        if (!string.IsNullOrEmpty(loginError))
        {
            GUILayout.Label(loginError, GetRichTextStyle());
        }

        GUILayout.Space(12);
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
        if (GUILayout.Button("AUTHENTICATE TERMINAL", GUILayout.Height(36)))
        {
            if (string.Equals(inputStationCode.Trim(), "DEALER-LIV01", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(inputStationCode.Trim(), "DEALER-DEFAULT", StringComparison.OrdinalIgnoreCase))
            {
                isAuthenticated = true;
                loginError = "";
                SaveSessionState();
            }
            else
            {
                loginError = "<color=red>INVALID DEALERSHIP ACCREDITATION CODE.</color>";
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndVertical();
    }

    // ============================================================================================================
    //  STAGE 2: ACTIVE SHOWROOM CONSOLE
    // ============================================================================================================
    private void DrawShowroomConsole()
    {
        // 1. VEHICLE CONTEXT
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("CONNECTED SHOWROOM VEHICLE", EditorStyles.boldLabel);
        GUILayout.Label($"TARGET VIN: <b>{targetVin}</b>", GetRichTextStyle());
        GUILayout.Label("VEHICLE STATE: <b>PRE-DELIVERY / SHOWROOM MODE</b>", GetRichTextStyle());
        GUILayout.Label($"DEMO TOKEN PAYLOAD: <b>{SHOWROOM_DEMO_KEY}</b>", GetRichTextStyle());
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // 2. ACOUSTIC TRANSMISSION INTERFACE (CUSTOMER DEMO)
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("CUSTOMER ACOUSTIC DEMONSTRATION", EditorStyles.boldLabel);
        GUILayout.Label("DEMONSTRATES PHYSICAL-PRESENCE GATING BY TRANSMITTING A NEAR-ULTRASONIC 16-17 KHZ TOKEN THROUGH THIS DEVICE'S SPEAKER.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(14);

        GUI.backgroundColor = new Color(0.16f, 0.65f, 0.35f);
        if (GUILayout.Button("TRANSMIT SHOWROOM ACOUSTIC TOKEN (16-17 KHZ)", GUILayout.Height(48)))
        {
            TransmitShowroomAcousticKey();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(8);
        if (isTransmitting)
        {
            GUILayout.Label("<color=#00FF77><b>TRANSMITTING MASKED ACOUSTIC TOKEN TO CABIN MICROPHONE...</b></color>", GetRichTextStyle());
        }

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // 3. SHOWROOM EXPLAINER CARDS FOR CUSTOMERS
        EditorGUILayout.BeginVertical("helpBox");
        GUILayout.Label("CUSTOMER WALKTHROUGH TALKING POINTS:", EditorStyles.boldLabel);
        GUILayout.Label("1. PHYSICAL PRESENCE: Unlike RF keys, ultrasonic sound is trapped inside the cabin glass and weatherstripping, preventing relay theft from outside the driveway.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(4);
        GUILayout.Label("2. ZERO COGNITIVE FRICTION: The customer never has to type a password while driving; the phone handles cryptographic handshake automatically.", EditorStyles.wordWrappedLabel);
        GUILayout.Space(4);
        GUILayout.Label("3. ZERO SPECIAL HARDWARE: Uses standard car speakers and cabin microphones already installed at the factory.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndVertical();

        GUILayout.Space(14);

        // 4. PREPARATION FOR CUSTOMER DELIVERY
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("VEHICLE HANDOVER PREPARATION", EditorStyles.boldLabel);
        GUILayout.Label("WHEN THE BUYER COLLECTS THE VEHICLE, USE 'RESET DEMO / RETURN TO MODE SELECT' ON THE IN-CAR SCREEN TO WIPE DEALER TEST TOKENS AND LAUNCH INITIAL OWNER VAULT SETUP.", EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndVertical();
    }

    private void TransmitShowroomAcousticKey()
    {
        isTransmitting = true;

        if (QALEncoder.Instance != null)
        {
            // Synthesize and play the dealer showroom acoustic token
            QALEncoder.Instance.PlayAudio(SHOWROOM_DEMO_KEY);

            if (FakeECU.Instance != null)
            {
                FakeECU.Instance.LogECUEvent("ShowroomAcousticTransmission", true, 0, "Acoustic_CPFSK", "DEALER_SHOWROOM", "SalesDemoTablet");
            }
        }
        else
        {
            Debug.LogWarning("[DEALERSHIP APK] QALEncoder instance not found in active scene.");
        }
    }

    private GUIStyle GetRichTextStyle() => new GUIStyle(EditorStyles.label) { richText = true };
}