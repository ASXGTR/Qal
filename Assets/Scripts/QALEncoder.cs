using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

public class QALEncoder : MonoBehaviour
{
    // ============================================================================================================
    //  QAL ENCODER - CRYPTOGRAPHIC TOKEN SYNTHESIZER (PATENT CLAIMS 1, 3, 5, 13, 15, 17)
    //  UPDATED WITH ANTI-LOOP TRAILING CUSHION FOR MOBILE PHONE REPEAT PLAYERS
    // ============================================================================================================

    public static QALEncoder Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        EnsureAudioSource();
    }

    void OnEnable()
    {
        if (Instance == null)
            Instance = this;

        EnsureAudioSource();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D flat audio
        }
    }

    [Header("Audio Output")]
    public AudioSource audioSource;
    public AudioClip defaultMaskClip;

    [Header("Active Memory Clips")]
    public AudioClip activeMasterKeyClip;
    public AudioClip activeGuestKeyClip;

    // Acoustic Modulation Configuration (Patent Section 6.2)
    const int SAMPLE_RATE = 48000;
    const float BIT_DURATION = 0.010f; // 10ms per bit (100 baud, Claim 5)
    const float FREQ_0 = 16000f;       // Binary '0' carrier (16.0 kHz, Claim 4)
    const float FREQ_1 = 17000f;       // Binary '1' carrier (17.0 kHz, Claim 4)

    const string PREAMBLE = "10101010101010101010101010101010"; // 32-bit clock sync
    const string SOF = "1110001110001110";                     // 16-bit Start-of-Frame
    const string POSTAMBLE = "10101010101010101010101010101010"; // 32-bit protective carrier cushion (Section 6.3)

    // Amplitude Scaling Constants (Claim 15 / Section 6.2)
    const float AUDIBLE_PEAK = 0.65f;
    const float ULTRASONIC_PEAK = 0.25f;
    const float COMPOSITE_MAX = 0.90f;
    const float BURST_FADE_SECONDS = 0.005f; // 5ms raised-cosine fade in/out

    // Anti-Looping Trailing Silence Cushion (Allows auto-looping mobile players to cleanly separate bursts)
    const float TRAILING_SILENCE_SECONDS = 8.0f;

    // ============================================================================================================
    //  LIVE TRANSMISSION
    // ============================================================================================================

    public void PlayAudio(string masterPassword)
    {
        // Do NOT register this as a permanent master password in FakeECU!
        // Simply use the password to derive the cryptographic signing key for the transmission.
        byte[] signingKey = GetCryptographicSigningKey(masterPassword);
        int expiryEpoch = (int)DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds();

        string bitstream = BuildCryptographicTokenBitstream(0x01, signingKey, expiryEpoch);

        float[] ultrasonic = GenerateFSK(bitstream);
        float[] masked = MixWithMask(ultrasonic);

        activeMasterKeyClip = CreateClip(masked, "LiveMasterTokenClip");
        PlayClipDirectly(activeMasterKeyClip);

        NotifySuccess("Transmitting FRESH Replay-Proof Token (16-17 kHz CP-FSK).");
    }

    public void PlayGuestAudio(string guestPassword)
    {
        byte[] signingKey = GetCryptographicSigningKey(guestPassword);
        int expiryEpoch = (int)DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds();

        string bitstream = BuildCryptographicTokenBitstream(0x02, signingKey, expiryEpoch);

        float[] ultrasonic = GenerateFSK(bitstream);
        float[] masked = MixWithMask(ultrasonic);

        activeGuestKeyClip = CreateClip(masked, "LiveGuestClip");
        PlayClipDirectly(activeGuestKeyClip);

        NotifySuccess("Transmitting FRESH Replay-Proof Guest Token (16-17 kHz CP-FSK).");
    }

    // ============================================================================================================
    //  WAV EXPORT FOR PHYSICAL TESTING
    // ============================================================================================================

    public void GenerateAudio(string masterPassword)
    {
        byte[] signingKey = GetCryptographicSigningKey(masterPassword);
        int expiryEpoch = (int)DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();

        string bitstream = BuildCryptographicTokenBitstream(0x01, signingKey, expiryEpoch);

        float[] ultrasonic = GenerateFSK(bitstream);
        float[] masked = MixWithMask(ultrasonic);

        activeMasterKeyClip = CreateClip(masked, "TestMasterTokenClip");
        string filename = "QAL_TestKey_Master_30Days";
        ExportToWav(masked, filename);
        OpenExportFolder();

        NotifySuccess($"Physical Test Key Exported!\nFile: {filename}.wav (Includes 8s Anti-Loop Cushion)");
    }

    public void GenerateGuestAudio(string guestPassword, string guestLabel, int durationHours)
    {
        string safeLabel = SanitizeFilename(guestLabel);
        byte[] signingKey = GetCryptographicSigningKey(guestPassword);
        int expiryEpoch = (int)DateTimeOffset.UtcNow.AddHours(durationHours).ToUnixTimeSeconds();

        string bitstream = BuildCryptographicTokenBitstream(0x02, signingKey, expiryEpoch);

        float[] ultrasonic = GenerateFSK(bitstream);
        float[] masked = MixWithMask(ultrasonic);

        activeGuestKeyClip = CreateClip(masked, $"GuestClip_{safeLabel}");
        string filename = $"QAL_TestKey_{safeLabel}_{durationHours}h";
        ExportToWav(masked, filename);
        OpenExportFolder();

        NotifySuccess($"Guest Digital Key Generated!\nFile: {filename}.wav (Valid {durationHours}h)");
    }

    // ============================================================================================================
    //  CRYPTOGRAPHIC KEY DERIVATION
    // ============================================================================================================
    private byte[] GetCryptographicSigningKey(string credential)
    {
        string cleanCred = SanitizePasswordPublic(credential);

        if (FakeECU.Instance != null && FakeECU.Instance.GetHSMRootSecret() != null)
        {
            using (var hmac = new HMACSHA256(FakeECU.Instance.GetHSMRootSecret()))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(cleanCred));
            }
        }

        using (var sha = SHA256.Create())
        {
            return sha.ComputeHash(Encoding.UTF8.GetBytes("QAL_STATIC_SALT_" + cleanCred));
        }
    }

    // ============================================================================================================
    //  PACKET FRAMING (CLAIM 13: NONCE + EPOCH + 256-BIT HMAC + CRC32 + 32-BIT POSTAMBLE)
    // ============================================================================================================
    string BuildCryptographicTokenBitstream(byte slotType, byte[] signingKey256, int expiryEpoch)
    {
        byte[] nonce = GenerateRandomBytes(8);
        byte[] epochBytes = BitConverter.GetBytes(expiryEpoch);
        if (BitConverter.IsLittleEndian) Array.Reverse(epochBytes);

        byte[] signingData = new byte[1 + nonce.Length + epochBytes.Length];
        signingData[0] = slotType;
        Buffer.BlockCopy(nonce, 0, signingData, 1, nonce.Length);
        Buffer.BlockCopy(epochBytes, 0, signingData, 1 + nonce.Length, epochBytes.Length);

        byte[] hmac;
        using (var hmacSha = new HMACSHA256(signingKey256))
        {
            hmac = hmacSha.ComputeHash(signingData);
        }

        byte[] payload = new byte[signingData.Length + hmac.Length];
        Buffer.BlockCopy(signingData, 0, payload, 0, signingData.Length);
        Buffer.BlockCopy(hmac, 0, payload, signingData.Length, hmac.Length);

        uint crc = CRC32(payload);
        byte[] crcBytes = BitConverter.GetBytes(crc);
        if (BitConverter.IsLittleEndian) Array.Reverse(crcBytes);

        Debug.Log($"<color=cyan>[ENCODER]</color> MINTED TOKEN CRC: <b>{BitConverter.ToString(crcBytes)}</b> | Nonce: {BitConverter.ToString(nonce, 0, 4)}... (Payload: {payload.Length} bytes)");

        byte[] fullFrame = new byte[payload.Length + crcBytes.Length];
        Buffer.BlockCopy(payload, 0, fullFrame, 0, payload.Length);
        Buffer.BlockCopy(crcBytes, 0, fullFrame, payload.Length, crcBytes.Length);

        StringBuilder bitstream = new StringBuilder(PREAMBLE + SOF);
        foreach (byte b in fullFrame)
            bitstream.Append(Convert.ToString(b, 2).PadLeft(8, '0'));

        bitstream.Append(POSTAMBLE);

        return bitstream.ToString();
    }

    // ============================================================================================================
    //  CP-FSK MODULATOR WITH 64-BIT PRECISION & ZERO-DRIFT PHASE WRAPPING
    // ============================================================================================================
    float[] GenerateFSK(string bitstream)
    {
        int samplesPerBit = (int)(SAMPLE_RATE * BIT_DURATION);
        int totalSamples = bitstream.Length * samplesPerBit;

        float[] ultrasonic = new float[totalSamples];
        double phase = 0.0;
        const double TWO_PI = Math.PI * 2.0;

        for (int b = 0; b < bitstream.Length; b++)
        {
            double freq = (bitstream[b] == '1') ? FREQ_1 : FREQ_0;
            double omega = TWO_PI * freq / SAMPLE_RATE;

            for (int i = 0; i < samplesPerBit; i++)
            {
                ultrasonic[b * samplesPerBit + i] = (float)Math.Sin(phase);
                phase += omega;
                if (phase >= TWO_PI) phase -= TWO_PI;
            }
        }

        ApplyBurstFade(ultrasonic, SAMPLE_RATE, BURST_FADE_SECONDS);
        return ultrasonic;
    }

    void ApplyBurstFade(float[] data, int sampleRate, float fadeSeconds)
    {
        int fadeSamples = Mathf.Min((int)(sampleRate * fadeSeconds), data.Length / 2);
        if (fadeSamples <= 0) return;

        for (int i = 0; i < fadeSamples; i++)
        {
            float t = i / (float)fadeSamples;
            float gain = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t);
            data[i] *= gain;
            data[data.Length - 1 - i] *= gain;
        }
    }

    // ============================================================================================================
    //  12TH-ORDER BUTTERWORTH LOWPASS FILTER (SIX SOS BIQUADS) - SECTION 6.2
    // ============================================================================================================
    float[] LowPassButterworth12(float[] data, float cutoffHz, int sampleRate)
    {
        float[] sectionQ = new float[] { 3.8306f, 1.3066f, 0.8213f, 0.6305f, 0.5412f, 0.5043f };
        float[] output = data;
        foreach (float q in sectionQ)
            output = BiquadLowPass(output, cutoffHz, q, sampleRate);
        return output;
    }

    float[] BiquadLowPass(float[] data, float cutoffHz, float q, int sampleRate)
    {
        float w0 = 2f * Mathf.PI * cutoffHz / sampleRate;
        float cosw0 = Mathf.Cos(w0);
        float sinw0 = Mathf.Sin(w0);
        float alpha = sinw0 / (2f * q);

        float b0 = (1f - cosw0) / 2f;
        float b1 = 1f - cosw0;
        float b2 = (1f - cosw0) / 2f;
        float a0 = 1f + alpha;
        float a1 = -2f * cosw0;
        float a2 = 1f - alpha;

        b0 /= a0; b1 /= a0; b2 /= a0;
        a1 /= a0; a2 /= a0;

        float[] output = new float[data.Length];
        float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float x0 = data[i];
            float y0 = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            output[i] = y0;
            x2 = x1; x1 = x0;
            y2 = y1; y1 = y0;
        }
        return output;
    }

    // ============================================================================================================
    //  STEGANOGRAPHIC MASKING MIXER WITH 8-SECOND ANTI-LOOP TRAILING CUSHION
    // ============================================================================================================
    float[] MixWithMask(float[] ultrasonic)
    {
        // Add 8 full seconds of clean dead air after the token to prevent phone repeat-loop collisions!
        int paddingSamples = (int)(SAMPLE_RATE * TRAILING_SILENCE_SECONDS);
        int maxSamples = ultrasonic.Length + paddingSamples;

        float[] final = new float[maxSamples];
        float[] maskData = new float[maxSamples];
        int clipSamples = 0;

        if (defaultMaskClip != null)
        {
            clipSamples = defaultMaskClip.samples;
            float[] temp = new float[clipSamples];
            defaultMaskClip.GetData(temp, 0);

            // Loop the chime ONLY during the active ultrasonic token period (stops during the trailing silence)
            for (int i = 0; i < maxSamples; i++)
            {
                maskData[i] = (i < ultrasonic.Length && clipSamples > 0) ? temp[i % clipSamples] : 0f;
            }

            maskData = LowPassButterworth12(maskData, 14000f, SAMPLE_RATE);
            NormalizeToPeak(maskData, AUDIBLE_PEAK);
        }

        float[] scaledUltrasonic = new float[maxSamples];
        for (int i = 0; i < maxSamples; i++)
        {
            float u = (i < ultrasonic.Length) ? ultrasonic[i] : 0f;
            scaledUltrasonic[i] = u * ULTRASONIC_PEAK;
        }

        for (int i = 0; i < maxSamples; i++)
            final[i] = maskData[i] + scaledUltrasonic[i];

        // Ensure composite peak strictly adheres to Claim 15 (<= 0.90)
        float max = 0f;
        for (int i = 0; i < final.Length; i++)
            max = Mathf.Max(max, Mathf.Abs(final[i]));

        if (max > COMPOSITE_MAX)
        {
            float scale = COMPOSITE_MAX / max;
            for (int i = 0; i < final.Length; i++)
                final[i] *= scale;
        }

        return final;
    }

    void NormalizeToPeak(float[] data, float targetPeak)
    {
        if (data == null || data.Length == 0) return;

        float max = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float a = Mathf.Abs(data[i]);
            if (a > max) max = a;
        }

        if (max <= 0f) return;

        float scale = targetPeak / max;
        for (int i = 0; i < data.Length; i++)
            data[i] *= scale;
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

    byte[] GenerateRandomBytes(int length)
    {
        byte[] bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return bytes;
    }

    public string SanitizePasswordPublic(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-";
        raw = raw.ToUpperInvariant();
        StringBuilder clean = new StringBuilder();
        foreach (char c in raw)
            if (allowed.IndexOf(c) >= 0) clean.Append(c);
        return clean.ToString();
    }

    string SanitizeFilename(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Key";
        StringBuilder clean = new StringBuilder();
        foreach (char c in raw)
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-') clean.Append(c);
        return clean.Length > 0 ? clean.ToString() : "Key";
    }

    private void PlayClipDirectly(AudioClip clip)
    {
        if (clip == null) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.ConfigureForFSK();

        EnsureAudioSource();

        if (audioSource != null)
        {
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    AudioClip CreateClip(float[] samples, string clipName)
    {
        AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SAMPLE_RATE, false);
        clip.SetData(samples, 0);
        return clip;
    }

    string ExportToWav(float[] samples, string filename)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "QAL_AudioKeys");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string filepath = Path.Combine(folder, filename + ".wav");

            using (FileStream stream = new FileStream(filepath, FileMode.Create))
            {
                short[] intData = new short[samples.Length];
                byte[] bytesData = new byte[samples.Length * 2];
                const float rescale = 32767f;
                for (int i = 0; i < samples.Length; i++)
                {
                    intData[i] = (short)(Mathf.Clamp(samples[i], -1f, 1f) * rescale);
                    byte[] byteArr = BitConverter.GetBytes(intData[i]);
                    byteArr.CopyTo(bytesData, i * 2);
                }
                WriteWavHeader(stream, SAMPLE_RATE, 1, bytesData.Length);
                stream.Write(bytesData, 0, bytesData.Length);
            }
            return filepath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WAV EXPORT ERROR] {ex.Message}");
            return null;
        }
    }

    void WriteWavHeader(FileStream stream, int sampleRate, int channels, int dataLength)
    {
        byte[] riff = Encoding.UTF8.GetBytes("RIFF");
        stream.Write(riff, 0, 4);
        stream.Write(BitConverter.GetBytes(dataLength + 36), 0, 4);
        stream.Write(Encoding.UTF8.GetBytes("WAVEfmt "), 0, 8);
        stream.Write(BitConverter.GetBytes(16), 0, 4);
        stream.Write(BitConverter.GetBytes((ushort)1), 0, 2);
        stream.Write(BitConverter.GetBytes((ushort)channels), 0, 2);
        stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);
        stream.Write(BitConverter.GetBytes(sampleRate * channels * 2), 0, 4);
        stream.Write(BitConverter.GetBytes((ushort)(channels * 2)), 0, 2);
        stream.Write(BitConverter.GetBytes((ushort)16), 0, 2);
        stream.Write(Encoding.UTF8.GetBytes("data"), 0, 4);
        stream.Write(BitConverter.GetBytes(dataLength), 0, 4);
    }

    public void OpenExportFolder()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "QAL_AudioKeys");
        if (Directory.Exists(folder)) Application.OpenURL("file://" + folder);
    }

    private void NotifySuccess(string message)
    {
        if (DemoManager.Instance != null) DemoManager.Instance.ShowSuccessPopup(message);
        else Debug.Log($"[QAL Encoder] {message}");
    }
}