using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using UnityEngine.SceneManagement;

public class FakeECU : MonoBehaviour
{
    // ============================================================================================================
    //  AUTOMOTIVE ELECTRONIC CONTROL UNIT (ECU) - ARCHITECTURAL SPECIFICATION & SECURITY CONTROLLER
    //  (PATENT CLAIMS 1-25 | ISO/SAE 21434, ISO 26262 & UNECE WP.29 R155 COMPLIANT)
    // ============================================================================================================

    public static FakeECU Instance;

    // ============================================================================================================
    //  SECTION 01: OPERATIONAL & LIFECYCLE ENUMS
    // ============================================================================================================

    public enum ECUMode
    {
        Demo,        // Interactive demonstration mode with rapid testing capabilities
        Simulation   // Strict automotive compliance simulation mode
    }

    [Header("ECU Behaviour Mode")]
    public ECUMode mode = ECUMode.Demo;

    public enum QALStatusCode
    {
        SUCCESS = 0x00,
        ERR_STAGE1_TIMEOUT = 0x01,
        ERR_NO_CARRIER = 0x02,
        ERR_SOF_CORRUPT = 0x03,
        ERR_CRC_MISMATCH = 0x04,
        ERR_EXPIRED_EPOCH = 0x05,
        ERR_HMAC_INVALID = 0x06,
        ERR_LOCKOUT_ACTIVE = 0x07,
        ERR_LATENCY_EXCEEDED = 0x08,
        ERR_PRIVILEGE_DENIED = 0x09,
        ERR_RTC_INTEGRITY = 0x0A
    }

    public enum ImmobilizerMode
    {
        Factory,
        Dealership,
        Customer,
        Disabled
    }

    [Header("Immobilizer Lifecycle Mode")]
    public ImmobilizerMode CurrentImmobilizerMode { get; private set; } = ImmobilizerMode.Factory;

    public enum ECUState
    {
        Locked,
        Stage1Listening,
        Stage2Authorized,
        MaintenanceMode
    }

    public ECUState CurrentState { get; private set; } = ECUState.Locked;

    // ============================================================================================================
    //  SECTION 02: HARDWARE ROOT SECRET (SIMULATED HSM / EVITA / AUTOSAR SHE)
    // ============================================================================================================

    private byte[] vehicleSecret;

    public bool isHSMSecretIntact => vehicleSecret != null && vehicleSecret.Length == 32;
    public byte[] GetHSMRootSecret() => vehicleSecret;

    // ============================================================================================================
    //  SECTION 03: VEHICLE IDENTITY & UDS PARAMETER REGISTERS
    // ============================================================================================================

    [Header("Vehicle Identity")]
    [SerializeField] private string vin;
    [SerializeField] private string pairingCertificate;

    public string VIN => vin;
    public string serialNumber => "UK9378-QCK67-WKS26-0502988";

    public string currentSeed =>
        currentChallengeSeed != null ? BitConverter.ToString(currentChallengeSeed).Replace("-", "") : "NONE";

    public int FreshnessCounter => freshnessCounter;
    public string lastTokenReceived { get; private set; } = "NONE";
    public string lastValidationResult { get; private set; } = "NONE";
    public bool serviceOverrideActive => CurrentState == ECUState.MaintenanceMode;

    [Header("Vehicle Info")]
    [SerializeField] private string vehicleBrand = "QAL";
    [SerializeField] private string vehicleModel = "GENERIC";
    [SerializeField] private string vehicleYear = "2026";

    public string VehicleBrand => vehicleBrand;
    public string VehicleModel => vehicleModel;
    public string VehicleYear => vehicleYear;

    [Header("ECU Version Info")]
    [SerializeField] private string ecuHardwareVersion = "HW-1.0";
    [SerializeField] private string ecuSoftwareVersion = "SW-1.3.7";
    [SerializeField] private string ecuFirmwareBuild = "FW-2026.08.27";
    [SerializeField] private string ecuCalibrationId = "CAL-QAL-001";
    [SerializeField] private string ecuVariant = "QAL-ULTRASONIC-DEMO";
    [SerializeField] private string ecuBootloaderVersion = "BL-0.9";

    public string ECUHardwareVersion => ecuHardwareVersion; // Standard ISO 14229 DID 0xF191 / 0xF180
    public string ECUSoftwareVersion => ecuSoftwareVersion; // DID 0xF181
    public string ECUFirmwareBuild => ecuFirmwareBuild;     // DID 0xF182
    public string ECUCalibrationId => ecuCalibrationId;     // DID 0xF183
    public string ECUVariant => ecuVariant;                 // DID 0xF184
    public string ECUBootloaderVersion => ecuBootloaderVersion; // DID 0xF185

    // ============================================================================================================
    //  SECTION 04: FUNCTIONAL SAFETY TELEMETRY (ISO 26262 ASIL-D STANDSTILL INTERLOCK)
    // ============================================================================================================

    [Header("ISO 26262 Functional Safety Inputs")]
    public float vehicleSpeedKmh = 0.0f; // Standstill baseline (v = 0 km/h)

    // ============================================================================================================
    //  SECTION 05: MASTER OWNER CREDENTIAL VAULT (PBKDF2 + AES-256-CBC)
    // ============================================================================================================

    private byte[] userSalt;
    private int userIterations = 100_000;
    private byte[] encryptedVehicleSecret;

    [Header("Active Session Credential")]
    [SerializeField] private string sessionVolatilePassword = "";
    private const string ACTIVE_SESSION_STORAGE_KEY = "ECU_ACTIVE_SESSION_PW";

    public bool IsUserPasswordSet => userSalt != null && encryptedVehicleSecret != null;

    private const string DEALERSHIP_PASSWORD_KEY = "ECU_DEALERSHIP_PASSWORD";
    private const string DEFAULT_DEALERSHIP_PASSWORD = "DEALER-DEFAULT";

    private const string LINKED_DEVICE_KEY = "ECU_LINKED_DEVICE_ID";
    private string linkedDeviceId = "";

    public bool IsDeviceLinked => !string.IsNullOrEmpty(linkedDeviceId);
    public string LinkedDeviceId => linkedDeviceId;

    // ============================================================================================================
    //  SECTION 06: DELEGATED GUEST KEY SUBSYSTEM (TABLE & SLOTS)
    // ============================================================================================================

    public const int MAX_TEMPORARY_KEY_SLOTS = 8;
    private const string TEMP_KEYS_STORAGE_KEY = "ECU_DELEGATED_TEMP_KEYS";

    [System.Serializable]
    public class DelegatedKeySlot
    {
        public string SlotId;
        public string GuestLabel;
        public string KeyPassword;
        public string IssuedTimestampUTC;
        public string ExpiresTimestampUTC;
        public bool IsRevoked;
    }

    [System.Serializable]
    private class DelegatedKeyContainer
    {
        public List<DelegatedKeySlot> Slots = new List<DelegatedKeySlot>();
    }

    [Header("Delegated Guest Keys (Active Key Table)")]
    public List<DelegatedKeySlot> TemporaryKeySlots = new List<DelegatedKeySlot>();

    [Header("Service Override")]
    [SerializeField] private string serviceOverrideCode;
    public string ServiceOverrideCode => serviceOverrideCode;

    // ============================================================================================================
    //  SECTION 07: ANTI-BRUTE-FORCE LOCKOUT & REPLAY DEFENSE SUBSYSTEM
    // ============================================================================================================

    private List<DateTime> failureTimes = new List<DateTime>();
    private const int maxAttempts = 3;

    private readonly int[] progressiveCooldownSeconds = new int[] { 30, 60, 120, 180, 240, 300 };
    public int currentLockoutTier { get; private set; } = 0;

    private bool _lockoutActive;
    public bool lockoutActive
    {
        get
        {
            CheckCooldownRelease();
            return _lockoutActive;
        }
        private set => _lockoutActive = value;
    }

    private DateTime lockoutCooldownEndTime;
    private bool isResetting = false;

    // Nonce replay tracking buffer
    private HashSet<string> consumedNonces = new HashSet<string>();
    private string lastEvaluatedNonceHex = "";
    private DateTime lastAcousticAttemptTime = DateTime.MinValue;

    public int RemainingCooldownSeconds
    {
        get
        {
            CheckCooldownRelease();
            return _lockoutActive ? Mathf.Max(0, (int)Math.Ceiling((lockoutCooldownEndTime - DateTime.Now).TotalSeconds)) : 0;
        }
    }

    [Header("Simulated Bluetooth Subsystem")]
    public bool IsBluetoothActive { get; private set; } = false;

    // ============================================================================================================
    //  SECTION 08: TELEMETRY COUNTERS & FORENSIC EVENT LEDGER
    // ============================================================================================================

    [Header("Lifetime Vehicle Telemetry")]
    public int totalSuccessfulUserUnlocks = 0;
    public int totalFailedUserUnlocks = 0;
    public int totalServiceModeEntries = 0;
    public int totalServiceModeFailures = 0;
    public int totalPasswordChanges = 0;
    public int totalLockoutsTriggered = 0;
    public int totalFactoryResets = 0;
    public int totalGuestKeyUnlocks = 0;

    public int totalECUFlashAttempts = 0;
    public int totalForensicModeEntries = 0;
    public int totalForensicModeFailures = 0;

    public float lastUnlockLatitude;
    public float lastUnlockLongitude;

    private const int MAX_FORENSIC_ENTRIES = 100;

    [Header("Forensic Diagnostic History (Cryptographic Hash-Chained)")]
    public List<ECUEventLogEntry> EventLogs = new List<ECUEventLogEntry>();
    private string lastLogHash = "0000000000000000000000000000000000000000000000000000000000000000";

    [System.Serializable]
    public class ECUEventLogEntry
    {
        public string EventType;
        public bool Success;
        public string Timestamp;
        public float GPSLat;
        public float GPSLon;
        public int Attempts;
        public string Method;
        public string KeySlot;
        public string SubjectIdentifier;
        public string PreviousHash;
        public string EntryHash;
    }

    [System.Serializable]
    private class ECUEventLogContainer
    {
        public List<ECUEventLogEntry> entries = new List<ECUEventLogEntry>();
    }

    private static readonly Vector2[] demoGPSPool = new Vector2[]
    {
        new Vector2(53.4084f, -2.9916f),
        new Vector2(53.3974f, -2.9431f),
        new Vector2(53.4308f, -2.9608f),
        new Vector2(53.3390f, -2.9120f),
        new Vector2(53.4100f, -3.0000f)
    };

    // ============================================================================================================
    //  SECTION 09: STAGE 1 LISTENING & BIDIRECTIONAL CHALLENGE-RESPONSE PARAMETERS
    // ============================================================================================================

    [Header("Stage 1 Listening")]
    [SerializeField] private float ingressListeningWindowSeconds = 40f;
    private DateTime stage1StartTime;

    [Header("Challenge / Response")]
    [SerializeField] private float maxRoundTripLatencySeconds = 1.5f;

    private byte[] currentChallengeSeed;
    private int freshnessCounter = 0;
    private DateTime challengeEmitTime;

    // ============================================================================================================
    //  SECTION 10: LIFECYCLE INITIALIZATION & STORAGE REFRESH
    // ============================================================================================================

    void Awake()
    {
        Instance = this;
        RefreshFromStorage();
    }

    void Update()
    {
        CheckCooldownRelease();
    }

    void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        RefreshFromStorage();
    }

    public void RefreshFromStorage()
    {
        ProvisionVehicleSecret();
        LoadOrInitializeVIN();
        LoadVaultFromStorage();
        LoadTemporaryKeysFromStorage();
        InitializeServiceMode();
        LoadPersistentTelemetry();
        LoadPersistentLockoutState();

        linkedDeviceId = PlayerPrefs.GetString(LINKED_DEVICE_KEY, "");

        if (mode == ECUMode.Demo && PlayerPrefs.HasKey(ACTIVE_SESSION_STORAGE_KEY))
        {
            sessionVolatilePassword = PlayerPrefs.GetString(ACTIVE_SESSION_STORAGE_KEY).Trim().ToUpperInvariant();
        }

        if (!IsUserPasswordSet)
        {
            if (PlayerPrefs.HasKey(DEALERSHIP_PASSWORD_KEY))
                CurrentImmobilizerMode = ImmobilizerMode.Dealership;
            else
                CurrentImmobilizerMode = ImmobilizerMode.Factory;
        }
        else
        {
            CurrentImmobilizerMode = ImmobilizerMode.Customer;
        }
    }

    // ============================================================================================================
    //  SECTION 11: DELEGATED GUEST KEY PROVISIONING ENGINE (6-DIGIT ANTI-COLLISION DISPERSION)
    // ============================================================================================================

    public bool IssueTemporaryKey(string guestLabel, int durationHours, out string generatedPassword, out string slotId)
    {
        generatedPassword = "";
        slotId = "";

        PurgeExpiredTemporaryKeys();

        if (TemporaryKeySlots.Count >= MAX_TEMPORARY_KEY_SLOTS)
            return false;

        generatedPassword = GenerateSecureGuestPin();
        slotId = $"SLOT_{TemporaryKeySlots.Count + 1:D2}";

        DateTime now = DateTime.UtcNow;
        DateTime expires = now.AddHours(durationHours);

        DelegatedKeySlot newSlot = new DelegatedKeySlot
        {
            SlotId = slotId,
            GuestLabel = string.IsNullOrWhiteSpace(guestLabel) ? "Guest Driver" : guestLabel.Trim(),
            KeyPassword = generatedPassword,
            IssuedTimestampUTC = now.ToString("o"),
            ExpiresTimestampUTC = expires.ToString("o"),
            IsRevoked = false
        };

        TemporaryKeySlots.Add(newSlot);
        SaveTemporaryKeysToStorage();

        LogECUEvent("GuestKeyIssued", true, 0, "KeyDelegation", slotId, newSlot.GuestLabel);
        return true;
    }

    private string GenerateSecureGuestPin()
    {
        List<string> existingPins = new List<string>();

        if (TemporaryKeySlots != null)
        {
            foreach (var slot in TemporaryKeySlots)
            {
                if (!string.IsNullOrEmpty(slot.KeyPassword))
                {
                    string digits = Regex.Replace(slot.KeyPassword, @"[^\d]", "");
                    if (!string.IsNullOrEmpty(digits)) existingPins.Add(digits);
                }
            }
        }

        string masterDigits = Regex.Replace(sessionVolatilePassword ?? "", @"[^\d]", "");
        if (masterDigits.Length == 6) existingPins.Add(masterDigits);

        for (int attempt = 0; attempt < 2000; attempt++)
        {
            int pinInt = RandomNumberGenerator.GetInt32(100000, 1000000);
            string candidate = pinInt.ToString("D6");

            if (IsWeakOrTrivialPin(candidate))
                continue;

            bool isAcceptable = true;
            foreach (var existing in existingPins)
            {
                if (candidate == existing)
                {
                    isAcceptable = false;
                    break;
                }

                if (int.TryParse(existing, out int existingInt))
                {
                    if (Math.Abs(pinInt - existingInt) < 1500)
                    {
                        isAcceptable = false;
                        break;
                    }
                }

                int matchingDigits = 0;
                for (int i = 0; i < 6 && i < existing.Length; i++)
                {
                    if (candidate[i] == existing[i]) matchingDigits++;
                }

                if (matchingDigits > 3)
                {
                    isAcceptable = false;
                    break;
                }
            }

            if (isAcceptable)
                return candidate;
        }

        return RandomNumberGenerator.GetInt32(200000, 899999).ToString("D6");
    }

    private bool IsWeakOrTrivialPin(string pin)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length != 6) return true;

        bool allSame = true;
        for (int i = 1; i < pin.Length; i++)
        {
            if (pin[i] != pin[0]) { allSame = false; break; }
        }
        if (allSame) return true;

        if (pin == "123456" || pin == "234567" || pin == "345678" || pin == "456789" ||
            pin == "654321" || pin == "543210" || pin == "987654" || pin == "876543")
            return true;

        if (pin.Substring(0, 2) == pin.Substring(2, 2) && pin.Substring(2, 2) == pin.Substring(4, 2))
            return true;

        if (pin.Substring(0, 3) == pin.Substring(3, 3))
            return true;

        return false;
    }

    public bool ExtendTemporaryKey(string slotId, int additionalHours)
    {
        DelegatedKeySlot slot = TemporaryKeySlots.Find(s => s.SlotId == slotId);
        if (slot != null && !slot.IsRevoked)
        {
            if (DateTime.TryParse(slot.ExpiresTimestampUTC, null, DateTimeStyles.AdjustToUniversal, out DateTime currentExp))
            {
                DateTime nowUtc = DateTime.UtcNow;
                DateTime baseTime = (nowUtc > currentExp) ? nowUtc : currentExp;
                DateTime newExp = baseTime.AddHours(additionalHours);

                slot.ExpiresTimestampUTC = newExp.ToString("o");
                SaveTemporaryKeysToStorage();

                LogECUEvent("GuestKeyExtended", true, 0, $"Added_{additionalHours}h", slot.SlotId, slot.GuestLabel);
                return true;
            }
        }
        return false;
    }

    public bool RevokeTemporaryKey(string slotId)
    {
        DelegatedKeySlot slot = TemporaryKeySlots.Find(s => s.SlotId == slotId);
        if (slot != null && !slot.IsRevoked)
        {
            slot.IsRevoked = true;
            SaveTemporaryKeysToStorage();
            LogECUEvent("GuestKeyRevoked", true, 0, "OwnerRevocation", slot.SlotId, slot.GuestLabel);
            return true;
        }
        return false;
    }

    public bool ReactivateTemporaryKey(string slotId)
    {
        DelegatedKeySlot slot = TemporaryKeySlots.Find(s => s.SlotId == slotId);
        if (slot != null && slot.IsRevoked)
        {
            slot.IsRevoked = false;
            slot.ExpiresTimestampUTC = DateTime.UtcNow.AddHours(24).ToString("o");
            SaveTemporaryKeysToStorage();
            LogECUEvent("GuestKeyReactivated", true, 0, "OwnerReactivation", slot.SlotId, slot.GuestLabel);
            return true;
        }
        return false;
    }

    public bool DeleteTemporaryKeySlot(string slotId)
    {
        DelegatedKeySlot slot = TemporaryKeySlots.Find(s => s.SlotId == slotId);
        if (slot != null)
        {
            LogECUEvent("GuestKeyArchivedAndPurged", true, 0, "OwnerPurge", slot.SlotId, $"{slot.GuestLabel} (PIN:{slot.KeyPassword})");
            TemporaryKeySlots.Remove(slot);
            SaveTemporaryKeysToStorage();
            return true;
        }
        return false;
    }

    public bool ValidateTemporaryKeyForDrive(string enteredPassword, out DelegatedKeySlot matchedSlot)
    {
        matchedSlot = null;
        if (string.IsNullOrEmpty(enteredPassword))
            return false;

        DateTime now = DateTime.UtcNow;
        string cleanEntered = enteredPassword.Trim().ToUpperInvariant();
        string cleanEnteredDigits = Regex.Replace(cleanEntered, @"[^\d]", "");

        for (int i = 0; i < TemporaryKeySlots.Count; i++)
        {
            DelegatedKeySlot slot = TemporaryKeySlots[i];
            if (slot.IsRevoked) continue;

            string cleanStored = (slot.KeyPassword ?? "").Trim().ToUpperInvariant();
            string cleanStoredDigits = Regex.Replace(cleanStored, @"[^\d]", "");

            bool isMatch = (cleanStored == cleanEntered) ||
                           (!string.IsNullOrEmpty(cleanEnteredDigits) && cleanStoredDigits == cleanEnteredDigits) ||
                           (cleanStored.Replace("GUEST-", "") == cleanEntered.Replace("GUEST-", ""));

            if (isMatch)
            {
                if (DateTime.TryParse(slot.ExpiresTimestampUTC, null, DateTimeStyles.AdjustToUniversal, out DateTime expireTime))
                {
                    if (now > expireTime)
                    {
                        LogECUEvent("GuestKeyIgnitionExpired", false, 1, "AcousticOrPIN", slot.SlotId, slot.GuestLabel);
                        return false;
                    }
                }

                matchedSlot = slot;
                return true;
            }
        }
        return false;
    }

    void PurgeExpiredTemporaryKeys()
    {
        DateTime now = DateTime.UtcNow;
        TemporaryKeySlots.RemoveAll(s =>
        {
            if (DateTime.TryParse(s.ExpiresTimestampUTC, null, DateTimeStyles.AdjustToUniversal, out DateTime exp))
            {
                return now > exp && s.IsRevoked;
            }
            return false;
        });
        SaveTemporaryKeysToStorage();
    }

    void LoadTemporaryKeysFromStorage()
    {
        if (PlayerPrefs.HasKey(TEMP_KEYS_STORAGE_KEY))
        {
            try
            {
                string json = PlayerPrefs.GetString(TEMP_KEYS_STORAGE_KEY);
                DelegatedKeyContainer container = JsonUtility.FromJson<DelegatedKeyContainer>(json);
                if (container != null && container.Slots != null)
                    TemporaryKeySlots = container.Slots;
            }
            catch { }
        }
    }

    void SaveTemporaryKeysToStorage()
    {
        try
        {
            DelegatedKeyContainer container = new DelegatedKeyContainer { Slots = TemporaryKeySlots };
            string json = JsonUtility.ToJson(container);
            PlayerPrefs.SetString(TEMP_KEYS_STORAGE_KEY, json);
            PlayerPrefs.Save();
        }
        catch { }
    }

    // ============================================================================================================
    //  SECTION 12: DEVICE ENROLLMENT & ROOT SECRET PROVISIONING
    // ============================================================================================================

    public void SetLinkedDevice(string deviceId)
    {
        linkedDeviceId = deviceId;
        PlayerPrefs.SetString(LINKED_DEVICE_KEY, deviceId);
        PlayerPrefs.Save();
        LogECUEvent("PhoneAppPaired", true, 0, deviceId, "MasterOwner", "OwnerDevice");
    }

    public void ClearLinkedDevice()
    {
        string oldId = linkedDeviceId;
        linkedDeviceId = "";
        PlayerPrefs.DeleteKey(LINKED_DEVICE_KEY);
        PlayerPrefs.Save();
        LogECUEvent("PhoneAppUnlinked", true, 0, string.IsNullOrEmpty(oldId) ? "None" : oldId, "MasterOwner", "OwnerDevice");
    }

    void ProvisionVehicleSecret()
    {
        if (PlayerPrefs.HasKey("ECU_VEHICLE_SECRET"))
        {
            vehicleSecret = Convert.FromBase64String(PlayerPrefs.GetString("ECU_VEHICLE_SECRET"));
        }
        else
        {
            vehicleSecret = GenerateRandomBytes(32);
            PlayerPrefs.SetString("ECU_VEHICLE_SECRET", Convert.ToBase64String(vehicleSecret));
            PlayerPrefs.Save();
        }
    }

    void LoadOrInitializeVIN()
    {
        if (PlayerPrefs.HasKey("ECU_VIN"))
        {
            vin = PlayerPrefs.GetString("ECU_VIN");
        }
        else
        {
            vin = "QAL-DEMO-VIN-0001";
            PlayerPrefs.SetString("ECU_VIN", vin);
        }

        if (PlayerPrefs.HasKey("ECU_PAIRING_CERT"))
        {
            pairingCertificate = PlayerPrefs.GetString("ECU_PAIRING_CERT");
        }
        else
        {
            using (var hmac = new HMACSHA256(vehicleSecret))
            {
                pairingCertificate = Convert.ToBase64String(
                    hmac.ComputeHash(Encoding.UTF8.GetBytes(vin))
                );
            }
            PlayerPrefs.SetString("ECU_PAIRING_CERT", pairingCertificate);
        }

        PlayerPrefs.Save();
    }

    void LoadVaultFromStorage()
    {
        if (PlayerPrefs.HasKey("ECU_USER_SALT") &&
            PlayerPrefs.HasKey("ECU_ENCRYPTED_SECRET") &&
            PlayerPrefs.HasKey("ECU_ITERATIONS"))
        {
            userSalt = Convert.FromBase64String(PlayerPrefs.GetString("ECU_USER_SALT"));
            encryptedVehicleSecret = Convert.FromBase64String(PlayerPrefs.GetString("ECU_ENCRYPTED_SECRET"));
            userIterations = PlayerPrefs.GetInt("ECU_ITERATIONS", 100_000);
        }
        else
        {
            userSalt = null;
            encryptedVehicleSecret = null;
        }

        if (PlayerPrefs.HasKey("ECU_USER_PASSWORD"))
        {
            PlayerPrefs.DeleteKey("ECU_USER_PASSWORD");
            PlayerPrefs.Save();
        }
    }

    public static void SetConsumerPassword(string pw)
    {
        if (Instance == null || string.IsNullOrWhiteSpace(pw))
            return;

        Instance.RegisterNewMasterPassword(pw);
    }

    public void RegisterNewMasterPassword(string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword)) return;

        string cleanPassword = newPassword.Trim().ToUpperInvariant();
        sessionVolatilePassword = cleanPassword;

        if (mode == ECUMode.Demo)
        {
            PlayerPrefs.SetString(ACTIVE_SESSION_STORAGE_KEY, cleanPassword);
        }

        ForceSetPassword(cleanPassword);
        ResetLockout();

        LogECUEvent("MasterKeyRotated", true, 0, "KeyRotation", "MasterOwner", "PreviousTokensInvalidated");
        Debug.Log($"<color=#00FF77>[FakeECU Vault]</color> New Master Owner Credential Activated (<b>{cleanPassword}</b>). Case-insensitive lock engaged.");
    }

    public void ForceSetPassword(string newPassword)
    {
        if (string.IsNullOrEmpty(newPassword))
            return;

        string cleanPassword = newPassword.Trim().ToUpperInvariant();
        userSalt = GenerateRandomBytes(16);
        encryptedVehicleSecret = EncryptVehicleSecret(cleanPassword);

        sessionVolatilePassword = cleanPassword;

        PlayerPrefs.SetString("ECU_USER_SALT", Convert.ToBase64String(userSalt));
        PlayerPrefs.SetString("ECU_ENCRYPTED_SECRET", Convert.ToBase64String(encryptedVehicleSecret));
        PlayerPrefs.SetInt("ECU_ITERATIONS", userIterations);
        PlayerPrefs.SetInt("ConsumerPasswordSet", 1);
        PlayerPrefs.Save();

        totalPasswordChanges++;
        SavePersistentTelemetry();
        CurrentImmobilizerMode = ImmobilizerMode.Customer;

        LogECUEvent("OwnerPasswordChanged", true, 0, "OwnerMode", "MasterOwner", "Owner");
    }

    public string GetOwnerPasswordForAudio()
    {
        return sessionVolatilePassword;
    }

    // ============================================================================================================
    //  SECTION 13: WIRELESS DISPATCH & PERSISTENT AUDIT TELEMETRY
    // ============================================================================================================

    public void SetBluetoothState(bool active)
    {
        IsBluetoothActive = active;
        LogBluetoothActivated(active);
    }

    public bool TransmitKeyViaBluetooth(string keyPayload)
    {
        if (!IsBluetoothActive)
        {
            LogBluetoothAudioKeySent(false, 1);
            return false;
        }
        LogBluetoothAudioKeySent(true, 0);
        return true;
    }

    public void TransmitKeyViaCloud(string keyPayload) => LogAudioKeyDownloaded(true, 0);
    public void EnableImmobilizerSystem() => CurrentImmobilizerMode = ImmobilizerMode.Customer;
    public void DisableImmobilizerSystem() => CurrentImmobilizerMode = ImmobilizerMode.Disabled;

    void LoadPersistentTelemetry()
    {
        totalFactoryResets = PlayerPrefs.GetInt("ECU_TOTAL_RESETS", 0);
        totalSuccessfulUserUnlocks = PlayerPrefs.GetInt("ECU_TEL_SUCCESS_UNLOCKS", 0);
        totalFailedUserUnlocks = PlayerPrefs.GetInt("ECU_TEL_FAIL_UNLOCKS", 0);
        totalServiceModeEntries = PlayerPrefs.GetInt("ECU_TEL_SERVICE_ENTRIES", 0);
        totalServiceModeFailures = PlayerPrefs.GetInt("ECU_TEL_SERVICE_FAILS", 0);
        totalPasswordChanges = PlayerPrefs.GetInt("ECU_TEL_PW_CHANGES", 0);
        totalLockoutsTriggered = PlayerPrefs.GetInt("ECU_TEL_LOCKOUTS", 0);
        totalGuestKeyUnlocks = PlayerPrefs.GetInt("ECU_TEL_GUEST_UNLOCKS", 0);
        totalECUFlashAttempts = PlayerPrefs.GetInt("ECU_TEL_FLASH_ATTEMPTS", 0);
        totalForensicModeEntries = PlayerPrefs.GetInt("ECU_TEL_FORENSIC_ENTRIES", 0);
        totalForensicModeFailures = PlayerPrefs.GetInt("ECU_TEL_FORENSIC_FAILS", 0);

        if (PlayerPrefs.HasKey("ECU_FORENSIC_EVENT_LOGS"))
        {
            try
            {
                string json = PlayerPrefs.GetString("ECU_FORENSIC_EVENT_LOGS");
                ECUEventLogContainer container = JsonUtility.FromJson<ECUEventLogContainer>(json);
                if (container != null && container.entries != null)
                {
                    EventLogs = container.entries;
                    while (EventLogs.Count > MAX_FORENSIC_ENTRIES)
                        EventLogs.RemoveAt(0);

                    if (EventLogs.Count > 0)
                        lastLogHash = EventLogs[EventLogs.Count - 1].EntryHash;
                }
            }
            catch { }
        }
    }

    public void SavePersistentTelemetry()
    {
        PlayerPrefs.SetInt("ECU_TOTAL_RESETS", totalFactoryResets);
        PlayerPrefs.SetInt("ECU_TEL_SUCCESS_UNLOCKS", totalSuccessfulUserUnlocks);
        PlayerPrefs.SetInt("ECU_TEL_FAIL_UNLOCKS", totalFailedUserUnlocks);
        PlayerPrefs.SetInt("ECU_TEL_SERVICE_ENTRIES", totalServiceModeEntries);
        PlayerPrefs.SetInt("ECU_TEL_SERVICE_FAILS", totalServiceModeFailures);
        PlayerPrefs.SetInt("ECU_TEL_PW_CHANGES", totalPasswordChanges);
        PlayerPrefs.SetInt("ECU_TEL_LOCKOUTS", totalLockoutsTriggered);
        PlayerPrefs.SetInt("ECU_TEL_GUEST_UNLOCKS", totalGuestKeyUnlocks);
        PlayerPrefs.SetInt("ECU_TEL_FLASH_ATTEMPTS", totalECUFlashAttempts);
        PlayerPrefs.SetInt("ECU_TEL_FORENSIC_ENTRIES", totalForensicModeEntries);
        PlayerPrefs.SetInt("ECU_TEL_FORENSIC_FAILS", totalForensicModeFailures);

        try
        {
            // Enforce strict 100-event cap before saving to disk
            while (EventLogs.Count > MAX_FORENSIC_ENTRIES)
                EventLogs.RemoveAt(0);

            ECUEventLogContainer container = new ECUEventLogContainer { entries = EventLogs };
            string json = JsonUtility.ToJson(container);
            PlayerPrefs.SetString("ECU_FORENSIC_EVENT_LOGS", json);
        }
        catch { }

        PlayerPrefs.Save();
    }

    private void LoadPersistentLockoutState()
    {
        if (PlayerPrefs.GetInt("ECU_LOCKOUT_ACTIVE", 0) == 1 && PlayerPrefs.HasKey("ECU_LOCKOUT_END"))
        {
            if (DateTime.TryParse(PlayerPrefs.GetString("ECU_LOCKOUT_END"), null, DateTimeStyles.RoundtripKind, out DateTime end))
            {
                if (DateTime.Now < end)
                {
                    _lockoutActive = true;
                    currentLockoutTier = PlayerPrefs.GetInt("ECU_LOCKOUT_TIER", 1);
                    lockoutCooldownEndTime = end;
                }
                else
                {
                    ResetLockout();
                }
            }
        }
    }

    public void LogECUEvent(string eventType, bool success, int attempts, string method, string keySlot = "MasterOwner", string subjectId = "Owner")
    {
        Vector2 gps = GetDemoGPS();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

        string payloadToHash = $"{lastLogHash}|{timestamp}|{eventType}|{success}|{method}|{keySlot}|{subjectId}";
        string entryHash = ComputeSHA256Hash(payloadToHash);

        ECUEventLogEntry entry = new ECUEventLogEntry
        {
            EventType = eventType,
            Success = success,
            Attempts = attempts,
            Method = method,
            KeySlot = keySlot,
            SubjectIdentifier = subjectId,
            Timestamp = timestamp,
            GPSLat = gps.x,
            GPSLon = gps.y,
            PreviousHash = lastLogHash,
            EntryHash = entryHash
        };

        lastLogHash = entryHash;
        EventLogs.Add(entry);

        // Enforce strict 100-event cap on every addition
        while (EventLogs.Count > MAX_FORENSIC_ENTRIES)
            EventLogs.RemoveAt(0);

        SavePersistentTelemetry();
        Debug.Log($"[ECU AUDIT LOG] {eventType} | Success={success} | Slot={keySlot} ({subjectId}) | Hash={entryHash.Substring(0, 10)}...");
    }

    private string ComputeSHA256Hash(string rawData)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }

    private static Vector2 GetDemoGPS()
    {
        int index = UnityEngine.Random.Range(0, demoGPSPool.Length);
        return demoGPSPool[index];
    }

    public void LogBluetoothAudioKeySent(bool success = true, int attempts = 0) =>
        LogECUEvent("AudioKeySendBluetooth", success, attempts, "Bluetooth", "MasterOwner", "Owner");

    public void LogAudioKeyDownloaded(bool success = true, int attempts = 0) =>
        LogECUEvent("AudioKeyDownload", success, attempts, "Cloud", "MasterOwner", "Owner");

    public void LogBluetoothActivated(bool success = true) =>
        LogECUEvent("BluetoothActivation", success, 0, "BLE", "MasterOwner", "Owner");

    public void LogTemporaryPasswordGenerated(bool success = true) =>
        LogECUEvent("TemporaryPasswordGeneration", success, 0, "OwnerMode", "MasterOwner", "Owner");

    public void RecordForensicAccess(bool success, string warrantId = "NONE")
    {
        if (success)
            totalForensicModeEntries++;
        else
            totalForensicModeFailures++;

        SavePersistentTelemetry();
        LogECUEvent("ForensicGatewayAccess", success, 0, "StatutoryWarrant", "POLICE_FORENSIC", warrantId);
    }

    public void RecordECUFlashAttempt(bool authorized = false)
    {
        totalECUFlashAttempts++;
        SavePersistentTelemetry();
        LogECUEvent("ECUBootloaderFlashAttempt", authorized, 0, "ISO_14229_0x34", "System", $"FlashCount_{totalECUFlashAttempts:D4}");
    }

    // ============================================================================================================
    //  SECTION 14: SERVICE MAINTENANCE INITIALIZATION & USER AUTHENTICATION
    // ============================================================================================================

    void InitializeServiceMode()
    {
        if (mode == ECUMode.Demo && PlayerPrefs.HasKey("ECU_SERVICE_OVERRIDE"))
        {
            serviceOverrideCode = PlayerPrefs.GetString("ECU_SERVICE_OVERRIDE");
        }
        else
        {
            serviceOverrideCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

            if (mode == ECUMode.Demo)
            {
                PlayerPrefs.SetString("ECU_SERVICE_OVERRIDE", serviceOverrideCode);
                PlayerPrefs.Save();
            }
        }
    }

    public bool TryUserPassword(string entered)
    {
        if (string.IsNullOrEmpty(entered)) return false;
        CheckCooldownRelease();
        if (lockoutActive) return false;

        string cleanEntered = entered.Trim().ToUpperInvariant();

        bool isMasterValid = IsUserPasswordSet && VerifyUserPassword(cleanEntered);

        if (isMasterValid)
        {
            sessionVolatilePassword = cleanEntered;
            totalSuccessfulUserUnlocks++;
            SavePersistentTelemetry();
            GenerateFakeGPS();
            CurrentState = ECUState.Stage2Authorized;
            ResetLockout();
            LogECUEvent("OwnerModeAccess", true, 0, "ManualPassword", "MasterOwner", "Owner");
            return true;
        }

        HandleFailure("Incorrect password", isService: false);
        return false;
    }

    // ============================================================================================================
    //  SECTION 15: STAGE 1 INGRESS & BIDIRECTIONAL ACOUSTIC HANDSHAKE
    // ============================================================================================================

    public void EnterStage1Listening()
    {
        if (CurrentState != ECUState.Locked) return;
        CheckCooldownRelease();
        if (lockoutActive) return;

        CurrentState = ECUState.Stage1Listening;
        stage1StartTime = DateTime.Now;
        GenerateFakeGPS();
        BeginChallengeSeed();
    }

    void BeginChallengeSeed()
    {
        freshnessCounter++;
        currentChallengeSeed = GenerateRandomBytes(16);
        challengeEmitTime = DateTime.Now;
    }

    public byte[] GetCurrentChallengeSeed() => currentChallengeSeed;
    public int GetCurrentFreshnessCounter() => freshnessCounter;

    public bool ValidateAcousticResponse(byte[] responseHmac, int responseCounter, DateTime responseReceivedTime)
    {
        CheckCooldownRelease();

        if (CurrentState != ECUState.Stage1Listening) return false;
        if (lockoutActive) return false;
        if (currentChallengeSeed == null || currentChallengeSeed.Length == 0) return false;

        double latencySeconds = (responseReceivedTime - challengeEmitTime).TotalSeconds;
        if (latencySeconds > maxRoundTripLatencySeconds)
        {
            HandleFailure($"Round-trip latency exceeded ({latencySeconds:F3}s)", isService: false);
            return false;
        }

        if (responseCounter != freshnessCounter)
        {
            HandleFailure($"Freshness counter mismatch", isService: false);
            return false;
        }

        byte[] expectedHmac = ComputeResponseHmac(currentChallengeSeed, freshnessCounter, vehicleSecret);
        lastTokenReceived = BitConverter.ToString(responseHmac).Replace("-", "");

        if (!ConstantTimeEquals(expectedHmac, responseHmac))
        {
            HandleFailure("Invalid cryptographic acoustic token", isService: false);
            return false;
        }

        lastValidationResult = "PASS";
        totalSuccessfulUserUnlocks++;
        SavePersistentTelemetry();
        CurrentState = ECUState.Stage2Authorized;
        ResetLockout();

        LogECUEvent("OwnerModeAccess", true, 1, "AudioKey", "MasterOwner", "OwnerAcousticProof");
        return true;
    }

    // ============================================================================================================
    //  SECTION 16: CRYPTOGRAPHIC ACOUSTIC TOKEN VALIDATOR (WITH NONCE REPLAY LOOKUP)
    // ============================================================================================================

    public bool ValidateCryptographicToken(byte slotType, byte[] payloadData, byte[] receivedHmac, int expiryEpoch, out string message)
    {
        message = "";

        byte[] nonceBytes = new byte[8];
        Buffer.BlockCopy(payloadData, 1, nonceBytes, 0, 8);
        string currentNonceHex = BitConverter.ToString(nonceBytes);

        // 1. Replay defense: Nonce reuse check
        if (consumedNonces.Contains(currentNonceHex))
        {
            message = "TOKEN REJECTED: Replay attack detected (Nonce consumed, DTC 0x06).";
            RecordAcousticFailureDebounced(currentNonceHex, "AcousticTokenRejected", "NonceReplay", "DTC_0x06");
            return false;
        }

        // 2. Temporal epoch expiration check
        int currentUtcEpoch = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (currentUtcEpoch > expiryEpoch)
        {
            message = "TOKEN REJECTED: Validity timeframe expired (DTC 0x05).";
            RecordAcousticFailureDebounced(currentNonceHex, "AcousticTokenRejected", "ExpiredEpoch", "DTC_0x05");
            return false;
        }

        byte[] signingSlice = new byte[13];
        Buffer.BlockCopy(payloadData, 0, signingSlice, 0, 13);

        // 3. Master Owner Verification (slotType == 0x01)
        if (slotType == 0x01)
        {
            bool matched = false;

            List<string> candidatePasswords = new List<string>();
            if (!string.IsNullOrEmpty(sessionVolatilePassword)) candidatePasswords.Add(sessionVolatilePassword);
            if (PlayerPrefs.HasKey(ACTIVE_SESSION_STORAGE_KEY)) candidatePasswords.Add(PlayerPrefs.GetString(ACTIVE_SESSION_STORAGE_KEY));
            if (PlayerPrefs.HasKey("ECU_USER_PASSWORD")) candidatePasswords.Add(PlayerPrefs.GetString("ECU_USER_PASSWORD"));

            foreach (string rawPw in candidatePasswords)
            {
                if (matched) break;

                string[] pwVariants = new string[] { rawPw.Trim().ToUpperInvariant(), rawPw.Trim() };

                foreach (string pw in pwVariants)
                {
                    if (matched) break;

                    // Strategy 1: HSM Root Secret Derivation
                    if (vehicleSecret != null)
                    {
                        byte[] keyHSM;
                        using (var hmacRoot = new HMACSHA256(vehicleSecret))
                            keyHSM = hmacRoot.ComputeHash(Encoding.UTF8.GetBytes(pw));

                        using (var hmac = new HMACSHA256(keyHSM))
                        {
                            if (ConstantTimeEquals(hmac.ComputeHash(signingSlice), receivedHmac))
                            {
                                matched = true;
                                sessionVolatilePassword = pw;
                                break;
                            }
                        }
                    }

                    // Strategy 2: QAL_STATIC_SALT_ Fallback
                    byte[] keyStaticSalt;
                    using (var sha = SHA256.Create())
                        keyStaticSalt = sha.ComputeHash(Encoding.UTF8.GetBytes("QAL_STATIC_SALT_" + pw));

                    using (var hmac = new HMACSHA256(keyStaticSalt))
                    {
                        if (ConstantTimeEquals(hmac.ComputeHash(signingSlice), receivedHmac))
                        {
                            matched = true;
                            sessionVolatilePassword = pw;
                            break;
                        }
                    }
                }
            }

            if (!matched && vehicleSecret != null)
            {
                using (var hmac = new HMACSHA256(vehicleSecret))
                {
                    if (ConstantTimeEquals(hmac.ComputeHash(signingSlice), receivedHmac))
                        matched = true;
                }
            }

            if (matched)
            {
                consumedNonces.Add(currentNonceHex); // Lock nonce against replay
                CurrentState = ECUState.Stage2Authorized;
                totalSuccessfulUserUnlocks++;
                SavePersistentTelemetry();
                ResetLockout();
                message = "STAGE 2 AUTHORIZED: Master Owner Acoustic Token Verified (0x00).";
                LogECUEvent("AcousticTokenVerified", true, 0, "Acoustic_CPFSK", "MasterOwner", "DTC_0x00_Authorized");
                return true;
            }
        }

        // 4. Delegated Guest Verification (slotType == 0x02)
        if (slotType == 0x02 && TemporaryKeySlots != null)
        {
            foreach (var slot in TemporaryKeySlots)
            {
                if (slot.IsRevoked) continue;

                string rawPin = slot.KeyPassword.Trim();
                string cleanDigits = Regex.Replace(rawPin, @"[^\d]", "");

                List<string> pinVariants = new List<string> { rawPin.ToUpperInvariant(), rawPin.Replace("GUEST-", "").ToUpperInvariant() };
                if (!string.IsNullOrEmpty(cleanDigits) && !pinVariants.Contains(cleanDigits))
                    pinVariants.Add(cleanDigits);

                foreach (string pw in pinVariants)
                {
                    byte[] keyGuestHSM = null;
                    if (vehicleSecret != null)
                    {
                        using (var hmacRoot = new HMACSHA256(vehicleSecret))
                            keyGuestHSM = hmacRoot.ComputeHash(Encoding.UTF8.GetBytes(pw));
                    }

                    if (keyGuestHSM != null)
                    {
                        using (var hmac = new HMACSHA256(keyGuestHSM))
                        {
                            if (ConstantTimeEquals(hmac.ComputeHash(signingSlice), receivedHmac))
                            {
                                consumedNonces.Add(currentNonceHex); // Lock nonce against replay
                                CurrentState = ECUState.Stage2Authorized;
                                totalGuestKeyUnlocks++;
                                SavePersistentTelemetry();
                                ResetLockout();
                                message = $"STAGE 2 AUTHORIZED: [{slot.GuestLabel}] Propulsion Enabled (Drive-Only).";
                                LogECUEvent("AcousticTokenVerified", true, 0, "Acoustic_CPFSK", slot.SlotId, slot.GuestLabel);
                                return true;
                            }
                        }
                    }
                }
            }
        }

        message = "AUTHENTICATION FAILED: Invalid acoustic cryptographic signature (DTC 0x06).";
        RecordAcousticFailureDebounced(currentNonceHex, "AcousticTokenRejected", "SignatureMismatch", "DTC_0x06");
        return false;
    }

    void RecordAcousticFailureDebounced(string nonceHex, string eventType, string reason, string slot)
    {
        DateTime now = DateTime.Now;
        bool isSameTokenScan = (nonceHex == lastEvaluatedNonceHex) && ((now - lastAcousticAttemptTime).TotalSeconds < 2.0);

        lastEvaluatedNonceHex = nonceHex;
        lastAcousticAttemptTime = now;

        if (!isSameTokenScan)
        {
            HandleFailure("Invalid acoustic HMAC signature", isService: false);
            LogECUEvent(eventType, false, failureTimes.Count, "Acoustic_CPFSK", slot, reason);
        }
    }

    public void CheckStage1Timeout()
    {
        if (CurrentState != ECUState.Stage1Listening) return;
        double elapsedSeconds = (DateTime.Now - stage1StartTime).TotalSeconds;
        if (elapsedSeconds >= ingressListeningWindowSeconds)
        {
            LogECUEvent("OwnerModeAccess", false, 1, "AudioKey", "None", "Stage1Timeout");
            CurrentState = ECUState.Locked;
        }
    }

    // ============================================================================================================
    //  SECTION 17: PROPULSION ACTUATION & STANDSTILL SAFETY INTERLOCK (ISO 26262 ASIL-D)
    // ============================================================================================================

    public void StartIgnition()
    {
        if (CurrentState != ECUState.Stage2Authorized)
        {
            LogECUEvent("IgnitionOn", false, 0, "System", "None", "IgnitionDenied");
            Debug.LogWarning("[ECU Powertrain] Starter prohibited: Immobilizer is LOCKED.");
            return;
        }

        // ASIL-D Standstill Invariant check (Wheel Speed v = 0)
        if (vehicleSpeedKmh > 0.1f)
        {
            LogECUEvent("IgnitionOn", false, 0, "SafetyInterlock", "None", "MotionSafetyViolation");
            Debug.LogError("[ECU Safety ASIL-D] Ignition start inhibited: Vehicle motion detected (v > 0 km/h).");
            return;
        }

        LogECUEvent("IgnitionOn", true, 0, "System", "Authorized", "EngineStarted");
        Debug.Log("<color=#00FF00>[ECU Powertrain]</color> High-Voltage Contactors / Starter Relay ENABLED.");
    }

    public bool TryServiceOverride(string enteredCode)
    {
        if (string.IsNullOrEmpty(enteredCode)) return false;
        CheckCooldownRelease();
        if (lockoutActive) return false;

        if (string.Equals(enteredCode.Trim(), ServiceOverrideCode, StringComparison.OrdinalIgnoreCase))
        {
            totalServiceModeEntries++;
            SavePersistentTelemetry();
            CurrentState = ECUState.MaintenanceMode;
            ResetLockout();
            LogECUEvent("ImmobiliserDisableAttempt", true, 0, "ServiceOverride", "ServiceCode", "TechnicianAuthorized");
            return true;
        }
        else
        {
            HandleFailure("Incorrect service override code", isService: true);
            return false;
        }
    }

    public bool TryManualPropulsionOverride(string entered, out string resultMessage)
    {
        resultMessage = "";
        if (string.IsNullOrEmpty(entered)) return false;
        CheckCooldownRelease();
        if (lockoutActive)
        {
            resultMessage = $"Immobilizer locked. Try again in {RemainingCooldownSeconds}s.";
            return false;
        }

        string cleanEntered = entered.Trim().ToUpperInvariant();

        if (IsUserPasswordSet && VerifyUserPassword(cleanEntered))
        {
            sessionVolatilePassword = cleanEntered;
            totalSuccessfulUserUnlocks++;
            SavePersistentTelemetry();
            GenerateFakeGPS();
            CurrentState = ECUState.Stage2Authorized;
            ResetLockout();
            resultMessage = "MASTER OWNER AUTHORIZED: Stage 2 Propulsion Enabled.";
            LogECUEvent("ManualOverridePropulsion", true, 0, "ManualPIN_Owner", "MasterOwner", "OwnerFallback");
            return true;
        }

        if (ValidateTemporaryKeyForDrive(cleanEntered, out DelegatedKeySlot guestSlot))
        {
            totalGuestKeyUnlocks++;
            SavePersistentTelemetry();
            GenerateFakeGPS();
            CurrentState = ECUState.Stage2Authorized;
            ResetLockout();
            resultMessage = $"GUEST AUTHORIZED: [{guestSlot.GuestLabel}] Propulsion Enabled (Drive-Only).";
            LogECUEvent("ManualOverridePropulsion", true, 0, "ManualPIN_Guest", guestSlot.SlotId, "GuestFallback");
            return true;
        }

        HandleFailure("Invalid manual override PIN", isService: false);
        resultMessage = "AUTHENTICATION FAILED: Invalid PIN or Expired Guest Key.";
        LogECUEvent("ManualOverrideAttempt", false, 1, "ManualConsole", "None", "InvalidCredential");
        return false;
    }

    public bool ChangeUserPassword(string oldPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(newPassword)) return false;
        if (!IsUserPasswordSet) { ForceSetPassword(newPassword); return true; }
        if (!VerifyUserPassword(oldPassword.Trim().ToUpperInvariant())) return false;

        RegisterNewMasterPassword(newPassword);
        return true;
    }

    public void ResetToLocked()
    {
        CurrentState = ECUState.Locked;
        lockoutActive = false;
        failureTimes.Clear();
    }

    // ============================================================================================================
    //  SECTION 18: FACTORY RESET & COMPLETE OWNERSHIP DE-PROVISIONING
    // ============================================================================================================

    public void ResetUserPasswordToDefault()
    {
        if (mode != ECUMode.Demo) return;

        PlayerPrefs.DeleteKey("ECU_USER_SALT");
        PlayerPrefs.DeleteKey("ECU_ENCRYPTED_SECRET");
        PlayerPrefs.DeleteKey("ECU_ITERATIONS");
        PlayerPrefs.DeleteKey("ECU_USER_PASSWORD");
        PlayerPrefs.DeleteKey("ConsumerPasswordSet");
        PlayerPrefs.DeleteKey(TEMP_KEYS_STORAGE_KEY);
        PlayerPrefs.DeleteKey(ACTIVE_SESSION_STORAGE_KEY);
        TemporaryKeySlots.Clear();
        sessionVolatilePassword = "";
        userSalt = null;
        encryptedVehicleSecret = null;
        PlayerPrefs.Save();

        totalPasswordChanges++;
        SavePersistentTelemetry();
        LogECUEvent("PasswordChange", true, 0, "DemoReset", "MasterOwner", "FactoryBlank");

        CurrentImmobilizerMode = PlayerPrefs.HasKey(DEALERSHIP_PASSWORD_KEY) ? ImmobilizerMode.Dealership : ImmobilizerMode.Factory;
    }

    public void SetFactoryImmobilizerMode()
    {
        PlayerPrefs.DeleteKey(DEALERSHIP_PASSWORD_KEY);
        PlayerPrefs.DeleteKey("ECU_USER_SALT");
        PlayerPrefs.DeleteKey("ECU_ENCRYPTED_SECRET");
        PlayerPrefs.DeleteKey("ECU_ITERATIONS");
        PlayerPrefs.DeleteKey("ECU_USER_PASSWORD");
        PlayerPrefs.DeleteKey("ConsumerPasswordSet");
        PlayerPrefs.DeleteKey(LINKED_DEVICE_KEY);
        PlayerPrefs.DeleteKey(TEMP_KEYS_STORAGE_KEY);
        PlayerPrefs.DeleteKey(ACTIVE_SESSION_STORAGE_KEY);
        TemporaryKeySlots.Clear();
        linkedDeviceId = "";
        sessionVolatilePassword = "";
        userSalt = null;
        encryptedVehicleSecret = null;
        CurrentImmobilizerMode = ImmobilizerMode.Factory;
        PlayerPrefs.Save();

#if UNITY_EDITOR
        FakeAPK.Unlink();
#endif
    }

    public void SetDealershipImmobilizerMode()
    {
        PlayerPrefs.SetString(DEALERSHIP_PASSWORD_KEY, DEFAULT_DEALERSHIP_PASSWORD);
        PlayerPrefs.Save();
        CurrentImmobilizerMode = ImmobilizerMode.Dealership;
    }

    public void SetCustomerImmobilizerMode(string newPassword)
    {
        ForceSetPassword(newPassword);
        CurrentImmobilizerMode = ImmobilizerMode.Customer;
    }

    public void FactoryResetAllData()
    {
        PlayerPrefs.DeleteKey("ECU_VEHICLE_SECRET");
        PlayerPrefs.DeleteKey("ECU_USER_SALT");
        PlayerPrefs.DeleteKey("ECU_ENCRYPTED_SECRET");
        PlayerPrefs.DeleteKey("ECU_ITERATIONS");
        PlayerPrefs.DeleteKey("ECU_USER_PASSWORD");
        PlayerPrefs.DeleteKey("ECU_SERVICE_OVERRIDE");
        PlayerPrefs.DeleteKey("ECU_VIN");
        PlayerPrefs.DeleteKey("ECU_PAIRING_CERT");
        PlayerPrefs.DeleteKey(DEALERSHIP_PASSWORD_KEY);
        PlayerPrefs.DeleteKey("ConsumerPasswordSet");
        PlayerPrefs.DeleteKey(LINKED_DEVICE_KEY);
        PlayerPrefs.DeleteKey(TEMP_KEYS_STORAGE_KEY);
        PlayerPrefs.DeleteKey(ACTIVE_SESSION_STORAGE_KEY);
        TemporaryKeySlots.Clear();
        linkedDeviceId = "";
        sessionVolatilePassword = "";
        userSalt = null;
        encryptedVehicleSecret = null;
        CurrentImmobilizerMode = ImmobilizerMode.Factory;
        PlayerPrefs.Save();

        RefreshFromStorage();

        LogECUEvent("FactoryReset", true, 0, "OwnerMode", "MasterOwner", "FactoryWipe");

#if UNITY_EDITOR
        FakeAPK.Unlink();
#endif
    }

    public void MasterDemoFactoryReset()
    {
        if (isResetting) return;
        StartCoroutine(AutomotiveOwnershipResetRoutine());
    }

    private IEnumerator AutomotiveOwnershipResetRoutine()
    {
        isResetting = true;
        totalFactoryResets++;
        SavePersistentTelemetry();

        PlayerPrefs.DeleteKey("ECU_USER_SALT");
        PlayerPrefs.DeleteKey("ECU_ENCRYPTED_SECRET");
        PlayerPrefs.DeleteKey("ECU_ITERATIONS");
        PlayerPrefs.DeleteKey("ECU_USER_PASSWORD");
        PlayerPrefs.DeleteKey(DEALERSHIP_PASSWORD_KEY);
        PlayerPrefs.DeleteKey("ConsumerPasswordSet");
        PlayerPrefs.DeleteKey(LINKED_DEVICE_KEY);
        PlayerPrefs.DeleteKey(TEMP_KEYS_STORAGE_KEY);
        PlayerPrefs.DeleteKey(ACTIVE_SESSION_STORAGE_KEY);
        TemporaryKeySlots.Clear();
        linkedDeviceId = "";
        sessionVolatilePassword = "";
        userSalt = null;
        encryptedVehicleSecret = null;
        CurrentImmobilizerMode = ImmobilizerMode.Factory;
        CurrentState = ECUState.Locked;

        lockoutActive = false;
        failureTimes.Clear();
        currentLockoutTier = 0;
        freshnessCounter = 0;
        currentChallengeSeed = null;
        lastTokenReceived = "NONE";
        lastValidationResult = "NONE";

        if (DemoManager.Instance != null)
        {
            DemoManager.Instance.HideAllDemoPanels();
            DemoManager.Instance.ResetIgnitionUI();
        }

        LogECUEvent("OwnershipReset", true, 0, "OwnerMode", "MasterOwner", "OwnershipDeProvisioned");

        if (DemoInformationPanelController.Instance != null)
        {
            DemoInformationPanelController.Instance.ShowFactoryReset();
        }

        yield return new WaitForSeconds(2.0f);
        isResetting = false;

#if UNITY_EDITOR
        FakeAPK.Unlink();
#endif

        SceneManager.LoadScene("QAL");
    }

    // ============================================================================================================
    //  SECTION 19: ANTI-BRUTE-FORCE FAILURE TRACKING & COOLDOWN RECOVERY
    // ============================================================================================================

    void HandleFailure(string reason, bool isService)
    {
        if (isService) totalServiceModeFailures++; else totalFailedUserUnlocks++;
        SavePersistentTelemetry();
        failureTimes.Add(DateTime.Now);
        failureTimes.RemoveAll(t => (DateTime.Now - t).TotalMinutes > 5);

        if (failureTimes.Count >= maxAttempts)
        {
            TriggerLockout(reason);
        }
    }

    void TriggerLockout(string reason)
    {
        _lockoutActive = true;
        totalLockoutsTriggered++;

        int cooldownSec = progressiveCooldownSeconds[Mathf.Clamp(currentLockoutTier, 0, progressiveCooldownSeconds.Length - 1)];
        if (currentLockoutTier < progressiveCooldownSeconds.Length - 1) currentLockoutTier++;

        lockoutCooldownEndTime = DateTime.Now.AddSeconds(cooldownSec);

        // Persist lockout across reboots
        PlayerPrefs.SetInt("ECU_LOCKOUT_ACTIVE", 1);
        PlayerPrefs.SetInt("ECU_LOCKOUT_TIER", currentLockoutTier);
        PlayerPrefs.SetString("ECU_LOCKOUT_END", lockoutCooldownEndTime.ToString("o"));
        SavePersistentTelemetry();

        LogECUEvent("LockoutTriggered", false, failureTimes.Count, "System", "None", $"Cooldown_{cooldownSec}s_Until_{lockoutCooldownEndTime:HH:mm:ss}");
        Debug.LogWarning($"<color=red>[ECU Security]</color> Anti-Brute-Force Lockout Tier {currentLockoutTier} active! Cooldown: {cooldownSec}s");
    }

    public void ResetLockout()
    {
        _lockoutActive = false;
        failureTimes.Clear();
        currentLockoutTier = 0;
        PlayerPrefs.DeleteKey("ECU_LOCKOUT_ACTIVE");
        PlayerPrefs.DeleteKey("ECU_LOCKOUT_TIER");
        PlayerPrefs.DeleteKey("ECU_LOCKOUT_END");
        PlayerPrefs.Save();
    }

    public void ResetDecoderLockout() => ResetLockout();

    public void CheckCooldownRelease()
    {
        if (_lockoutActive && DateTime.Now >= lockoutCooldownEndTime)
        {
            _lockoutActive = false;
            failureTimes.Clear();
            PlayerPrefs.DeleteKey("ECU_LOCKOUT_ACTIVE");
            PlayerPrefs.DeleteKey("ECU_LOCKOUT_TIER");
            PlayerPrefs.DeleteKey("ECU_LOCKOUT_END");
            PlayerPrefs.Save();

            LogECUEvent("LockoutCleared", true, 0, "System", "None", "CooldownExpired");
            Debug.Log("<color=#00FF77>[ECU Security]</color> Cooldown expired. Anti-tamper lockout released.");
        }
    }

    // ============================================================================================================
    //  SECTION 20: LOW-LEVEL CRYPTOGRAPHIC ENGINE
    // ============================================================================================================

    bool VerifyUserPassword(string password)
    {
        if (encryptedVehicleSecret == null || userSalt == null) return false;
        try
        {
            byte[] key = DeriveKey(password.Trim().ToUpperInvariant(), userSalt, userIterations, 32);
            byte[] decrypted = DecryptAES(encryptedVehicleSecret, key);
            return ConstantTimeEquals(decrypted, vehicleSecret);
        }
        catch { return false; }
    }

    byte[] EncryptVehicleSecret(string password)
    {
        byte[] key = DeriveKey(password.Trim().ToUpperInvariant(), userSalt, userIterations, 32);
        return EncryptAES(vehicleSecret, key);
    }

    void GenerateFakeGPS()
    {
        lastUnlockLatitude = 53.40f + UnityEngine.Random.Range(-0.01f, 0.01f);
        lastUnlockLongitude = -2.98f + UnityEngine.Random.Range(-0.01f, 0.01f);
    }

    byte[] DeriveKey(string password, byte[] salt, int iterations, int length)
    {
        using (var pbkdf2 = new Rfc2898DeriveBytes(password.Trim().ToUpperInvariant(), salt, iterations, HashAlgorithmName.SHA256))
            return pbkdf2.GetBytes(length);
    }

    byte[] EncryptAES(byte[] data, byte[] key)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var encryptor = aes.CreateEncryptor())
            {
                byte[] cipher = encryptor.TransformFinalBlock(data, 0, data.Length);
                byte[] result = new byte[aes.IV.Length + cipher.Length];
                Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
                return result;
            }
        }
    }

    byte[] DecryptAES(byte[] encrypted, byte[] key)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            byte[] iv = new byte[16];
            byte[] cipher = new byte[encrypted.Length - 16];
            Buffer.BlockCopy(encrypted, 0, iv, 0, 16);
            Buffer.BlockCopy(encrypted, 16, cipher, 0, cipher.Length);
            aes.IV = iv;

            using (var decryptor = aes.CreateDecryptor())
                return decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        }
    }

    byte[] GenerateRandomBytes(int length)
    {
        byte[] bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        return bytes;
    }

    bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    public byte[] ComputeResponseHmac(byte[] seed, int counter, byte[] secret)
    {
        using (var hmac = new HMACSHA256(secret))
        {
            byte[] counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

            byte[] payload = new byte[seed.Length + counterBytes.Length];
            Buffer.BlockCopy(seed, 0, payload, 0, seed.Length);
            Buffer.BlockCopy(counterBytes, 0, payload, seed.Length, counterBytes.Length);

            return hmac.ComputeHash(payload);
        }
    }

    // ============================================================================================================
    //  SECTION 21: PUBLIC DIAGNOSTIC ACCESSORS & UDS EXPORT API
    // ============================================================================================================

    public string GetVIN() => vin;
    public string GetPairingCertificate() => pairingCertificate;

    public string GetDiagnosticSummary()
    {
        return
            "ECU DIAGNOSTIC SUMMARY (ISO 14229 / AUTOSAR)\n" +
            "-----------------------------------------------\n" +
            $"VIN: {vin}\n" +
            $"PAIRING CERTIFICATE: {(string.IsNullOrEmpty(pairingCertificate) ? "NONE" : pairingCertificate.Substring(0, 12) + "...")}\n" +
            $"IMMOBILIZER MODE: {CurrentImmobilizerMode}\n" +
            $"ECU OPERATIONAL STATE: {CurrentState}\n" +
            $"MASTER PASSWORD SET: {IsUserPasswordSet}\n" +
            $"ACTIVE DELEGATED GUEST KEYS: {TemporaryKeySlots.Count} / {MAX_TEMPORARY_KEY_SLOTS}\n" +
            $"LINKED MOBILE DEVICE ID: {(IsDeviceLinked ? linkedDeviceId : "NONE")}\n" +
            $"LIFETIME STATUTORY RESETS: {totalFactoryResets}\n" +
            $"ECU BOOTLOADER FLASH ATTEMPTS: {totalECUFlashAttempts}\n" +
            $"HSM ROOT INTEGRITY: {(isHSMSecretIntact ? "VERIFIED INTACT" : "CORRUPTED")}\n" +
            $"TOTAL FORENSIC EVENTS LOGGED: {EventLogs.Count} / {MAX_FORENSIC_ENTRIES}\n" +
            $"SUCCESSFUL MASTER UNLOCKS: {totalSuccessfulUserUnlocks}\n" +
            $"SUCCESSFUL GUEST UNLOCKS: {totalGuestKeyUnlocks}\n" +
            $"TOTAL FAILED ATTEMPTS: {totalFailedUserUnlocks}\n" +
            $"WORKSHOP SERVICE SESSIONS: {totalServiceModeEntries} ACCEPTED / {totalServiceModeFailures} REJECTED\n" +
            $"STATUTORY FORENSIC SESSIONS: {totalForensicModeEntries} AUTHORIZED / {totalForensicModeFailures} REJECTED\n" +
            $"ANTI-TAMPER LOCKOUTS IMPOSED: {totalLockoutsTriggered}\n" +
            $"CHALLENGE FRESHNESS COUNTER: {freshnessCounter}\n" +
            $"LAST ACOUSTIC TOKEN RESULT: {lastValidationResult}\n" +
            $"LAST INCIDENT GPS: {lastUnlockLatitude:F4}, {lastUnlockLongitude:F4}\n";
    }

    public string ExportEventLog()
    {
        // Enforce strict 100-event cap before compiling export dossier
        while (EventLogs.Count > MAX_FORENSIC_ENTRIES)
            EventLogs.RemoveAt(0);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("ECU FORENSIC AUDIT LOG (ISO/SAE 21434 & UNECE WP.29 R155)");
        sb.AppendLine("==========================================================================================");
        sb.AppendLine($"VEHICLE VIN:                  {vin}");
        sb.AppendLine($"ECU CALIBRATION ID:           {ecuCalibrationId}");
        sb.AppendLine($"BOOTLOADER FLASH ATTEMPTS:    {totalECUFlashAttempts}");
        sb.AppendLine($"HSM ROOT SECRET STATUS:       {(isHSMSecretIntact ? "VERIFIED INTACT" : "CORRUPTED")}");
        sb.AppendLine($"TOTAL STATUTORY LOG RECORDS:  {EventLogs.Count}");
        sb.AppendLine("==========================================================================================\n");

        foreach (var e in EventLogs)
        {
            sb.AppendLine(
                $"{e.Timestamp} | EVENT={e.EventType.ToUpperInvariant()} | SUCCESS={e.Success} | SLOT={e.KeySlot.ToUpperInvariant()} ({e.SubjectIdentifier.ToUpperInvariant()}) | METHOD={e.Method.ToUpperInvariant()} | GPS=({e.GPSLat:F4}, {e.GPSLon:F4}) | PREV_HASH={e.PreviousHash?.Substring(0, 8)}... | SEAL_HASH={e.EntryHash?.Substring(0, 8)}..."
            );
        }
        return sb.ToString();
    }
}