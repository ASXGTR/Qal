using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Text;
using System.IO;
using System.Collections;

public class UltrasonicDecoder : MonoBehaviour
{
    // ============================================================================================================
    //  ULTRASONIC DECODER - PATENT COMPLIANT (100 BAUD / 10ms / 16-17 kHz CPFSK)
    //  PREAMBLE-LOCKED CHANNEL EQUALIZATION & DRIFT-IMMUNE DEMODULATION
    // ============================================================================================================

    public static UltrasonicDecoder Instance;

    public Action<string> OnOwnerAudioPasswordDecoded;

    [Header("Offline Direct-File Testing")]
    public AudioClip offlineTestAudioClip;

    [Header("Live Microphone Settings")]
    public float stage1Timeout = 40.0f;

    [Header("UI Status Displays")]
    public TextMeshProUGUI decodedPasswordText;
    public Button ignitionButton;
    public Image microphoneImage;

    public Color defaultColor = Color.white;
    public Color activeColor = new Color(0f, 1f, 0f);

    // ============================================================================================================
    //  PATENT SPECIFICATION CONSTANTS (STRICT ADHERENCE TO CLAIMS 4, 5, 13)
    // ============================================================================================================
    int activeSampleRate = 48000;

    const float BIT_DURATION = 0.010f;             // Claim 5: Strict 10ms symbol period (100 baud)
    const float FREQ_0 = 16000f;                   // Claim 4: Binary '0' carrier (16.0 kHz)
    const float FREQ_1 = 17000f;                   // Claim 4: Binary '1' carrier (17.0 kHz)

    const float BANDPASS_LOW_HZ = 15500f;
    const float BANDPASS_HIGH_HZ = 18000f;
    const float POLLING_INTERVAL_SECONDS = 0.15f;

    const float SQUELCH_NOISE_FLOOR = 0.0003f;

    // Strict 24-bit composite sync: 8 bits Preamble ("10101010") + 16 bits SOF ("1110001110001110")
    const string SYNC_PATTERN = "101010101110001110001110";
    const int SOF_OFFSET_IN_SYNC = 8;
    const string RAW_SOF = "1110001110001110";

    bool microphoneActive = false;
    AudioClip micClip;
    string micDevice = "";

    // Orthogonal Tables (7.0ms integration window @ 48kHz = 336 samples)
    float[] sin0_center, cos0_center, sin1_center, cos1_center;
    int guardOffsetSamples = 0;
    int centerIntegrationSamples = 0;

    private int lastProcessedSamplePos = 0;
    private float lastDiagnosticLogTime = 0f;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        DetectMicrophone();
    }

    // ============================================================================================================
    //  OFFLINE TEST HARNESS
    // ============================================================================================================

    [ContextMenu("Test Assigned Offline Clip")]
    public void RunOfflineTestFromInspector()
    {
        if (offlineTestAudioClip != null)
        {
            DecodeAudioClip(offlineTestAudioClip);
        }
        else
        {
            Debug.LogError("[OFFLINE TEST] No audio clip assigned.");
        }
    }

    public void DecodeFromAudioFile(string path)
    {
        StartCoroutine(LoadAndDecodeRoutine(path));
    }

    private IEnumerator LoadAndDecodeRoutine(string path)
    {
        string uri = path;
        if (!uri.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            uri = "file://" + path.Replace("\\", "/");
        }

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[OFFLINE TEST] Failed to load audio file at {uri}: {www.error}");
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            clip.name = Path.GetFileName(path);
            DecodeAudioClip(clip);
        }
    }

    public bool DecodeAudioClip(AudioClip clip)
    {
        if (clip == null) return false;

        float[] multiChannel = new float[clip.samples * clip.channels];
        clip.GetData(multiChannel, 0);

        float[] monoSamples;
        if (clip.channels > 1)
        {
            monoSamples = new float[clip.samples];
            for (int i = 0; i < clip.samples; i++)
            {
                float sum = 0f;
                for (int c = 0; c < clip.channels; c++)
                    sum += multiChannel[i * clip.channels + c];
                monoSamples[i] = sum / clip.channels;
            }
        }
        else
        {
            monoSamples = multiChannel;
        }

        Debug.Log($"<color=#00E5FF>[OFFLINE TEST]</color> Injecting '{clip.name}' ({clip.samples} samples, {clip.frequency} Hz)...");
        return DecodeRawSamples(monoSamples, clip.frequency);
    }

    public bool DecodeRawSamples(float[] samples, int sampleRate)
    {
        activeSampleRate = sampleRate;
        PrecomputeTrigTables();

        int samplesPerBit = (int)(activeSampleRate * BIT_DURATION);
        int phaseStep = Mathf.Max(1, samplesPerBit / 24);

        float[] filteredBuffer = BandPass(samples, BANDPASS_LOW_HZ, BANDPASS_HIGH_HZ);
        float rawPeak = GetPeak(filteredBuffer);
        NormalizeBuffer(filteredBuffer, rawPeak);

        float[] candidates = new float[] { 1.00f, 1.25f, 1.45f, 1.10f, 0.85f };
        for (int c = 0; c < candidates.Length; c++)
        {
            for (int phase = 0; phase < samplesPerBit; phase += phaseStep)
            {
                string bits = DemodulateFSK_Calibrated(filteredBuffer, phase, candidates[c]);
                if (string.IsNullOrEmpty(bits)) continue;

                if (ScanAndDecodeAllFrames(bits, phase, true))
                    return true;
            }
        }

        Debug.LogError("<color=#FF0000>[OFFLINE TEST FAILED]</color> Scanned all phases, but valid CRC was not found.");
        return false;
    }

    // ============================================================================================================
    //  LIVE MICROPHONE ENGINE
    // ============================================================================================================

    void DetectMicrophone()
    {
        if (Microphone.devices.Length == 0) return;

        micDevice = Microphone.devices[0];
        foreach (string device in Microphone.devices)
        {
            if (device.ToLower().Contains("fifine") || device.ToLower().Contains("usb"))
            {
                micDevice = device;
                break;
            }
        }
        Debug.Log("[UltrasonicDecoder] Bound to microphone: " + micDevice);
    }

    public void OnIgnitionStage1Pressed()
    {
        if (microphoneActive) return;

        if (FakeECU.Instance != null && FakeECU.Instance.lockoutActive)
        {
            if (DemoManager.Instance != null)
                DemoManager.Instance.ShowErrorPopup($"Immobilizer is locked ({FakeECU.Instance.RemainingCooldownSeconds}s remaining).");
            return;
        }

        Microphone.GetDeviceCaps(micDevice, out int minFreq, out int maxFreq);
        int targetFreq = 48000;
        if (minFreq > 0 && maxFreq > 0)
        {
            if (targetFreq < minFreq || targetFreq > maxFreq)
                targetFreq = maxFreq;
        }

        int bufferLength = Mathf.CeilToInt(stage1Timeout) + 5;
        micClip = Microphone.Start(micDevice, true, bufferLength, targetFreq);
        microphoneActive = true;

        activeSampleRate = micClip.frequency;
        if (activeSampleRate <= 0) activeSampleRate = targetFreq;

        PrecomputeTrigTables();
        lastProcessedSamplePos = 0;
        lastDiagnosticLogTime = 0f;

        Debug.Log($"<color=#00E5FF>[DECODER]</color> Single-Burst Listening active at {activeSampleRate} Hz on {micDevice}. Play phone audio ONCE...");

        SetIgnitionActive(true);
        SetMicrophoneActive(true);

        if (FakeECU.Instance != null)
            FakeECU.Instance.EnterStage1Listening();

        StartCoroutine(DecoderLoop());
    }

    void PrecomputeTrigTables()
    {
        int samplesPerBit = (int)(activeSampleRate * BIT_DURATION);

        guardOffsetSamples = (int)(activeSampleRate * 0.0015f);          // 1.5ms echo guard
        centerIntegrationSamples = (int)(activeSampleRate * 0.0070f);    // 7.0ms pure integration

        sin0_center = new float[centerIntegrationSamples];
        cos0_center = new float[centerIntegrationSamples];
        sin1_center = new float[centerIntegrationSamples];
        cos1_center = new float[centerIntegrationSamples];

        for (int i = 0; i < centerIntegrationSamples; i++)
        {
            float t = (float)i / activeSampleRate;
            sin0_center[i] = Mathf.Sin(2f * Mathf.PI * FREQ_0 * t);
            cos0_center[i] = Mathf.Cos(2f * Mathf.PI * FREQ_0 * t);
            sin1_center[i] = Mathf.Sin(2f * Mathf.PI * FREQ_1 * t);
            cos1_center[i] = Mathf.Cos(2f * Mathf.PI * FREQ_1 * t);
        }
    }

    IEnumerator DecoderLoop()
    {
        float startTime = Time.time;
        int samplesPerBit = (int)(activeSampleRate * BIT_DURATION);
        int phaseStep = Mathf.Max(1, samplesPerBit / 24);

        int maxWindowSamples = (int)(activeSampleRate * 5.0f);

        while (microphoneActive)
        {
            yield return new WaitForSeconds(POLLING_INTERVAL_SECONDS);

            if (Time.time - startTime > stage1Timeout)
            {
                Microphone.End(micDevice);
                microphoneActive = false;

                Debug.LogWarning("<color=#FFA500>[DECODER TIMEOUT]</color> Listening window elapsed.");

                if (DemoManager.Instance != null)
                    DemoManager.Instance.ShowErrorPopup("Stage 1 listening timed out (DTC 0x01)");

                SetIgnitionActive(false);
                SetMicrophoneActive(false);

                if (FakeECU.Instance != null)
                {
                    FakeECU.Instance.LogECUEvent("Stage1Timeout", false, 1, "Acoustic_CPFSK", "DTC_0x01", "ListeningWindowElapsed");
                    FakeECU.Instance.CheckStage1Timeout();
                }

                yield break;
            }

            int currentMicPos = Microphone.GetPosition(micDevice);
            if (currentMicPos <= 0 || micClip == null) continue;

            int newSamplesAvailable = currentMicPos - lastProcessedSamplePos;
            if (newSamplesAvailable < 0) newSamplesAvailable += micClip.samples;
            if (newSamplesAvailable < (int)(activeSampleRate * 1.5f)) continue;

            int extractCount = Mathf.Min(currentMicPos, maxWindowSamples);
            int startOffset = Mathf.Max(0, currentMicPos - extractCount);

            float[] buffer = new float[extractCount];
            micClip.GetData(buffer, startOffset);

            buffer = BandPass(buffer, BANDPASS_LOW_HZ, BANDPASS_HIGH_HZ);

            float rawPeak = GetPeak(buffer);
            if (rawPeak < SQUELCH_NOISE_FLOOR) continue;

            NormalizeBuffer(buffer, rawPeak);

            float[] testRatios = new float[] { 1.00f, 1.25f, 1.45f, 1.10f, 0.90f };
            bool frameDecoded = false;

            for (int r = 0; r < testRatios.Length && !frameDecoded; r++)
            {
                float ratio = testRatios[r];

                for (int phase = 0; phase < samplesPerBit; phase += phaseStep)
                {
                    string bits = DemodulateFSK_Calibrated(buffer, phase, ratio);
                    if (string.IsNullOrEmpty(bits)) continue;

                    if (ScanAndDecodeAllFrames(bits, phase, false))
                    {
                        frameDecoded = true;
                        Microphone.End(micDevice);
                        microphoneActive = false;
                        yield break;
                    }
                }

                yield return null;
            }

            lastProcessedSamplePos = currentMicPos;
        }
    }

    // ============================================================================================================
    //  STABLE FIXED-RATIO FSK DEMODULATOR (NEVER FLIPS ON LONG HMAC RUNS)
    // ============================================================================================================
    string DemodulateFSK_Calibrated(float[] data, int startOffset, float ratio)
    {
        int samplesPerBit = (int)(activeSampleRate * BIT_DURATION);
        if (samplesPerBit <= 0 || (data.Length - startOffset) < samplesPerBit) return null;

        StringBuilder bits = new StringBuilder((data.Length - startOffset) / samplesPerBit);

        for (int i = startOffset; i <= data.Length - samplesPerBit; i += samplesPerBit)
        {
            float mag0_sin = 0f, mag0_cos = 0f;
            float mag1_sin = 0f, mag1_cos = 0f;

            int bitBase = i + guardOffsetSamples;
            if (bitBase + centerIntegrationSamples > data.Length) break;

            for (int j = 0; j < centerIntegrationSamples; j++)
            {
                float s = data[bitBase + j];
                mag0_sin += s * sin0_center[j];
                mag0_cos += s * cos0_center[j];
                mag1_sin += s * sin1_center[j];
                mag1_cos += s * cos1_center[j];
            }

            float mag0 = mag0_sin * mag0_sin + mag0_cos * mag0_cos;
            float mag1 = (mag1_sin * mag1_sin + mag1_cos * mag1_cos) * ratio;

            bits.Append(mag1 > mag0 ? '1' : '0');
        }

        return bits.ToString();
    }

    float GetPeak(float[] data)
    {
        float peak = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float a = Mathf.Abs(data[i]);
            if (a > peak) peak = a;
        }
        return peak;
    }

    void NormalizeBuffer(float[] data, float peak)
    {
        if (peak <= 0.0001f) return;
        float scale = 1.0f / peak;
        for (int i = 0; i < data.Length; i++)
            data[i] *= scale;
    }

    bool ScanAndDecodeAllFrames(string bitstream, int currentPhaseOffset, bool isOfflineTest)
    {
        int searchStart = 0;

        while (searchStart < bitstream.Length)
        {
            int syncIndex = bitstream.IndexOf(SYNC_PATTERN, searchStart, StringComparison.Ordinal);
            int sofIndex = -1;
            int payloadStart = -1;

            if (syncIndex >= 0)
            {
                sofIndex = syncIndex + SOF_OFFSET_IN_SYNC;
                payloadStart = sofIndex + 16;
                searchStart = syncIndex + 1;
            }
            else
            {
                if (isOfflineTest)
                {
                    sofIndex = bitstream.IndexOf(RAW_SOF, searchStart, StringComparison.Ordinal);
                    if (sofIndex < 0) break;
                    payloadStart = sofIndex + 16;
                    searchStart = sofIndex + 1;
                }
                else
                {
                    break;
                }
            }

            int availableBits = bitstream.Length - payloadStart;

            if (availableBits >= 392)
            {
                if (TryValidateSingleFrame(bitstream, payloadStart, currentPhaseOffset, sofIndex, isOfflineTest))
                {
                    return true;
                }
            }
        }

        return false;
    }

    bool TryValidateSingleFrame(string bitstream, int payloadStart, int winningPhase, int sofBitIndex, bool isOfflineTest)
    {
        byte[] frameBytes = new byte[49];
        for (int i = 0; i < 49; i++)
        {
            string byteBits = bitstream.Substring(payloadStart + (i * 8), 8);
            frameBytes[i] = Convert.ToByte(byteBits, 2);
        }

        byte[] payloadData = new byte[45];
        byte[] receivedCrcBytes = new byte[4];
        Buffer.BlockCopy(frameBytes, 0, payloadData, 0, 45);
        Buffer.BlockCopy(frameBytes, 45, receivedCrcBytes, 0, 4);

        uint receivedCrcVal = BitConverter.ToUInt32(receivedCrcBytes, 0);
        if (BitConverter.IsLittleEndian)
        {
            receivedCrcVal = SwapEndianness(receivedCrcVal);
        }

        // ========================================================================================================
        // MULTI-ALGORITHM CRC MATCH ENGINE
        // ========================================================================================================
        bool isValidMatch = false;
        string matchedAlgorithm = "";

        // Standard IEEE 802.3 over 45-byte payload (Big-Endian match)
        uint calcIEEE = CRC32(payloadData);
        byte[] computedCrcBytes = BitConverter.GetBytes(calcIEEE);
        if (BitConverter.IsLittleEndian) Array.Reverse(computedCrcBytes);

        if (AreByteArraysEqual(receivedCrcBytes, computedCrcBytes))
        {
            isValidMatch = true;
            matchedAlgorithm = "Standard IEEE-802.3 (Big-Endian)";
        }
        else if (calcIEEE == receivedCrcVal)
        {
            isValidMatch = true;
            matchedAlgorithm = "Standard IEEE-802.3 (Little-Endian)";
        }
        else if (ComputeCrc32_MPEG2(payloadData) == receivedCrcVal || SwapEndianness(ComputeCrc32_MPEG2(payloadData)) == receivedCrcVal)
        {
            isValidMatch = true;
            matchedAlgorithm = "CRC32 MPEG-2 / BZIP2";
        }
        else
        {
            byte[] nonceOnly = new byte[44];
            Buffer.BlockCopy(payloadData, 1, nonceOnly, 0, 44);
            if (CRC32(nonceOnly) == receivedCrcVal || SwapEndianness(CRC32(nonceOnly)) == receivedCrcVal)
            {
                isValidMatch = true;
                matchedAlgorithm = "CRC32 (Excluding Slot byte)";
            }
        }

        int bitDifferences = 0;
        for (int b = 0; b < 4; b++)
        {
            byte diff = (byte)(receivedCrcBytes[b] ^ computedCrcBytes[b]);
            while (diff > 0)
            {
                bitDifferences += diff & 1;
                diff >>= 1;
            }
        }

        if (!isValidMatch)
        {
            if (!isOfflineTest && (Time.time - lastDiagnosticLogTime > 1.5f))
            {
                lastDiagnosticLogTime = Time.time;
                byte slot = payloadData[0];
                string nonceHex = BitConverter.ToString(payloadData, 1, 4);

                Debug.Log($"<color=#FFA500>[AIR BURST DETECTED]</color> Slot: 0x{slot:X2} | Nonce: {nonceHex}... | Rx CRC: <b>{BitConverter.ToString(receivedCrcBytes)}</b> vs Calc: <b>{BitConverter.ToString(computedCrcBytes)}</b> ({bitDifferences} bit diffs)");

                RunComprehensiveDiagnostics(frameBytes, receivedCrcBytes);
            }
            return false;
        }

        // ========================================================================================================
        // 100% SUCCESSFUL ACOUSTIC DECODE
        // ========================================================================================================
        Debug.Log($"<color=#00FF77>[CRC MATCH LOCKED]</color> Verified via <b>{matchedAlgorithm}</b> | CRC: <b>{BitConverter.ToString(receivedCrcBytes)}</b>");

        byte slotType = payloadData[0];

        byte[] epochBytes = new byte[4];
        Buffer.BlockCopy(payloadData, 9, epochBytes, 0, 4);
        if (BitConverter.IsLittleEndian) Array.Reverse(epochBytes);
        int expiryEpoch = BitConverter.ToInt32(epochBytes, 0);

        byte[] receivedHmac = new byte[32];
        Buffer.BlockCopy(payloadData, 13, receivedHmac, 0, 32);

        string resultMsg = "";
        if (FakeECU.Instance != null)
        {
            if (FakeECU.Instance.ValidateCryptographicToken(slotType, payloadData, receivedHmac, expiryEpoch, out resultMsg))
            {
                if (decodedPasswordText != null)
                    decodedPasswordText.text = "CRYPTOGRAPHIC TOKEN VERIFIED (0x00)";

                SetIgnitionActive(true);
                OnOwnerAudioPasswordDecoded?.Invoke("HMAC_VALID");

                if (DemoManager.Instance != null)
                    DemoManager.Instance.ShowSuccessPopup(resultMsg);

                Debug.Log("<color=#00FF00>[DECODER SUCCESS]</color> <b>Stage 2 Authorized! Propulsion Interlocks Closed.</b>");
                return true;
            }
            else
            {
                Debug.LogWarning($"<color=#FFCC00>[CRC VERIFIED BUT ECU REJECTED]</color> Acoustic frame arrived cleanly, but FakeECU rejected signature: {resultMsg}");
                return true;
            }
        }

        return false;
    }

    void RunComprehensiveDiagnostics(byte[] fullFrame, byte[] rxCrcBytes)
    {
        uint rxCrc = BitConverter.ToUInt32(rxCrcBytes, 0);
        if (BitConverter.IsLittleEndian) rxCrc = SwapEndianness(rxCrc);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<color=#FF00FF><b>=== [ACOUSTIC PAYLOAD DIAGNOSTIC] ===</b></color>");
        sb.AppendLine($"<b>Full Frame ({fullFrame.Length}B):</b> {BitConverter.ToString(fullFrame)}");
        sb.AppendLine($"<b>Target Rx CRC:</b> {BitConverter.ToString(rxCrcBytes)} (Hex: 0x{rxCrc:X8})");

        for (int start = 0; start <= 2; start++)
        {
            int len = 45 - start;
            if (len <= 0 || start + len > fullFrame.Length) continue;

            byte[] slice = new byte[len];
            Buffer.BlockCopy(fullFrame, start, slice, 0, len);

            uint ieee = CRC32(slice);
            uint mpeg = ComputeCrc32_MPEG2(slice);

            sb.AppendLine($"Slice [Offset {start}, Len {len}] -> IEEE: 0x{ieee:X8} | MPEG2: 0x{mpeg:X8}");
        }

        Debug.Log(sb.ToString());
    }

    bool AreByteArraysEqual(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    uint SwapEndianness(uint val)
    {
        return ((val & 0x000000FF) << 24) |
               ((val & 0x0000FF00) << 8) |
               ((val & 0x00FF0000) >> 8) |
               ((val & 0xFF000000) >> 24);
    }

    uint CRC32(byte[] bytes)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in bytes)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                bool bit = (crc & 1) == 1;
                crc >>= 1;
                if (bit) crc ^= 0xEDB88320;
            }
        }
        return ~crc;
    }

    uint ComputeCrc32_MPEG2(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = 0; i < data.Length; i++)
        {
            crc ^= (uint)(data[i] << 24);
            for (int j = 0; j < 8; j++)
                crc = ((crc & 0x80000000) != 0) ? (crc << 1) ^ 0x04C11DB7 : (crc << 1);
        }
        return crc;
    }

    float[] BandPass(float[] data, float lowHz, float highHz)
    {
        float dt = 1f / activeSampleRate;
        float RC_hp = 1f / (lowHz * 2f * Mathf.PI);
        float alpha_hp = RC_hp / (RC_hp + dt);

        float RC_lp = 1f / (highHz * 2f * Mathf.PI);
        float alpha_lp = dt / (RC_lp + dt);

        float[] highPassed = new float[data.Length];
        float[] bandPassed = new float[data.Length];

        highPassed[0] = data[0];
        bandPassed[0] = data[0];

        for (int i = 1; i < data.Length; i++)
        {
            highPassed[i] = alpha_hp * (highPassed[i - 1] + data[i] - data[i - 1]);
            bandPassed[i] = bandPassed[i - 1] + alpha_lp * (highPassed[i] - bandPassed[i - 1]);
        }
        return bandPassed;
    }

    void SetIgnitionActive(bool active)
    {
        if (DemoManager.Instance != null) DemoManager.Instance.SetIgnitionActive(active);
        if (ignitionButton != null)
        {
            TextMeshProUGUI btnText = ignitionButton.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.color = active ? activeColor : defaultColor;
        }
    }

    void SetMicrophoneActive(bool active)
    {
        if (microphoneImage != null) microphoneImage.color = active ? activeColor : defaultColor;
        if (DemoManager.Instance != null && DemoManager.Instance.micStatusCircle != null)
            DemoManager.Instance.micStatusCircle.color = active ? activeColor : defaultColor;
    }
}