# QAL Errata: known mistakes in the documents

This project was built by an amateur with a lot of AI assistance, then documented at length. The documents are published exactly as written, so they contain errors, inconsistencies, and a few claims that go further than the evidence. This file lists every one I know of, so you don't have to discover them yourself.

**Rule of thumb:** where a document disagrees with this file, trust this file. Where two documents disagree, the Demo Walkthrough (Appendix A source code in particular) is the most reliable, because it contains the code that actually runs.

If you find something not listed here, please open an issue. Being told I'm wrong is useful.

---

## A. Technical errors

### A1. The low-pass filter does not give ">80 dB at 16 kHz"
- **Where:** Patent text [0067] and claim 3; Technical Paper (glossary, §3.2).
- **Says:** the 12th-order Butterworth filter (14 kHz cutoff) attenuates by more than 80 dB at 16 kHz.
- **Actually:** a 12th-order Butterworth at 14 kHz cutoff, 48 kHz sampling, gives roughly **-30 dB at 16 kHz** (about -15 dB at 15 kHz, -46 dB at 17 kHz, -64 dB at 18 kHz). It passes 80 dB somewhere around 18.5-19 kHz.
- **Correct source:** the Demo Walkthrough (QALEncoder section) says "approximately 30 dB at 16.0 kHz and over 80 dB across the upper stopband", which is right.
- **Consequence:** the 14-16 kHz "guard band" is quiet, not silent. Any chime harmonics reaching the 16 kHz edge are only about 30 dB down. The spec's advice to keep chime tones below 4 kHz is what keeps this from mattering much in practice.

### A2. The 1.5 s latency window and the ~4.4 s token length conflict
- **Where:** Patent text [0089], [0124], claims 18-19, 25; Technical Paper; benchmark report.
- **Problem:** a full token frame (32-bit preamble + 16-bit start-of-frame + 392 payload bits, plus a 32-bit postamble in the code) at 100 baud is roughly **4.4-4.7 seconds** of audio. A round-trip limit of 1.5 s measured to "receipt of the response token" can never be met if "receipt" means the end of the token.
- **Likely intent:** the clock stops when the response *starts* (preamble detected), not when it ends. The documents never say which, and it matters.
- **Status:** unresolved. If you implement this, define the measurement point explicitly. In the simulation the check compares timestamps inside a single program, so it has not been tested against real acoustic round trips.

### A3. The anti-relay latency argument is weak
- **Where:** Patent text [0126]; Technical Paper §2.1 (Vector 4) and TARA table.
- **Says:** remote streaming relays fail because network delay "typically exceeds 300 ms to 1000 ms" and voice codecs strip frequencies above ~14 kHz.
- **Actually:**
  - 300-1000 ms is *below* the 1.5 s limit, so latency alone does not reject a relay.
  - The codec argument applies to telephone/VoIP voice codecs. An attacker using a wideband audio codec or a raw data link is not covered.
  - A real-time relay of a one-way chirp has never been tested against this design.
- **Treat as:** an unproven defence. The more honest framing is that the user-intent gate (unlock the phone and tap Transmit) and cabin attenuation raise the cost of an attack, not that relay is excluded.

### A4. Pulse shaping: the spec describes an earlier design than the code
- **Where:** Patent text [0059] and claim 5 (Hann window on every bit).
- **Actually:** the implementation uses constant amplitude across the burst with a single 5 ms raised-cosine fade at the start and end. The Technical Paper explains that per-bit Hann windowing creates a 100 Hz amplitude-modulation sideband that leaks into the guard band, which is why it was dropped.
- **Authoritative:** the code (`ApplyBurstFade()` in `QALEncoder.cs`).

### A5. Demodulator timing is described inconsistently
- **Where:** the benchmark report says "7.0 ms integration + 1.5 ms echo guard"; the Technical Paper and patent text describe integrating over the full 10 ms (480-sample) symbol window.
- **Status:** check `UltrasonicDecoder.cs` (Walkthrough Appendix A.24) for what actually runs.

---

## B. Freshness, replay and crypto

### B1. Two freshness modes, described as if they were one
The documents describe two different anti-replay mechanisms and often don't say which one they mean:

| Mode | How it works | Where it exists |
|---|---|---|
| **Unidirectional** | Token carries an 8-byte random nonce and an expiry time. The car remembers used nonces (`consumedNonces`) and enforces a 60-second validity window. | Implemented in the simulation, and what the phone-to-laptop tests used. |
| **Bidirectional challenge-response** | Car emits a 16-byte challenge seed; phone answers with an HMAC bound to the seed and an incrementing freshness counter, within the latency limit. | Implemented in simulation only (not exercised over the air). |

The Technical Paper describes the HSM monotonic counter as if it were in the unidirectional frame; it isn't. The frame contains a nonce and an epoch, not a counter.

### B2. Replay protection in the demo is only in memory
The consumed-nonce registry is a simulated in-memory store. Restart the simulation and an exported token file works again. A real design needs tamper-resistant non-volatile storage, and the "64-bit hardware monotonic counter in the HSM" is a production design intent, not something demonstrated.

### B3. The audit log is "tamper-evident" only against casual edits
Documents call the SHA-256 hash-chained log "immutable". A bare hash chain can be recomputed by anyone who can rewrite the whole log. A real design needs a keyed MAC or signature over the chain head (stored in the HSM), and a ring buffer capped at 100 entries means old entries are overwritten by design.

### B4. The phone holds the vehicle secret
Verification is a symmetric HMAC, so the phone's vault contains the secret the car checks against. Compromising the phone's vault compromises the vehicle's secret. Documented designs using asymmetric keys (per-device certificates) would avoid this; the prototype doesn't.

### B5. Simulation crypto is scaffolding
The demo uses hardcoded credentials (for example `PASSWORD`, `DEALER-LIV01`, `WARRANT-99`), static salts, `PlayerPrefs` storage, and unmasked console logging so evaluators can inspect values. The Walkthrough says so. None of it is a model for real key handling. The prototype also stores the expiry timestamp as a signed 32-bit integer (Y2038 limit), which the Walkthrough flags.

---

## C. Evidence and claims

### C1. The "Demodulation Benchmark" is a software loopback
It feeds the encoder straight into the decoder inside Unity (50 bursts, 0 errors in 19,600 bits). That shows the modulator and demodulator agree with each other. It says nothing about real acoustic channels. Despite wording like "bench verification" and "empirical", it involved no speaker, microphone or air.

### C2. The real over-the-air tests are in the Walkthrough (Appendix B), and are small
- Three topologies: Honor 90 Smart phone speaker to the laptop's built-in Realtek mic array; same phone to a Fifine K669B USB mic; laptop speaker to its own mic.
- 10-20 cm, one room at about 48-52 dBA, 50 consecutive bursts per topology, no errors observed, pre-synthesized token files.
- **"No errors in 50" is not "never fails".** Zero failures in 50 trials is still consistent with a true failure rate up to roughly 6%.
- No car cabin, no other handsets or cases, no engine or road noise, no attack testing (replay, relay, jamming, or an outside microphone).

### C3. The OS audio enhancements had to be disabled
Laptop noise suppression and vendor audio suites treat 15-18 kHz as hiss and can cut the carrier by 20-40 dB (Walkthrough Appendix B.4). On the test laptop, both the HP audio enhancements and the Windows audio enhancements had to be turned off before it worked. The documents argue that car microphones feed raw samples to the DSP and avoid this problem; that is reasonable but has not been measured.

### C4. Cabin containment numbers are targets
The "30-42 dB transmission loss through laminated glass and seals" figure is a target, labelled as such in the Technical Paper's evidentiary tiers (Tier 3: planned). It has not been measured, and the claim that tokens "cannot" be captured outside the car should be read as "are expected to be hard to".

### C5. "Zero added BOM" depends on the hardware
It assumes existing cabin microphones pass 16-17 kHz and phone speakers produce it cleanly. The Technical Paper itself says this is "subject to vehicle-specific acoustic bandwidth validation". Some people, especially younger ones, and some animals can hear near 16 kHz.

### C6. Standards language is aspirational
"Aligned with" ISO/SAE 21434, ISO 26262, UNECE R155/R156, EVITA, and CCC Digital Key means "designed with these in mind". Nothing has been assessed, certified, or reviewed against them. The Technical Paper also expands "TARA" as "Threat Assessment and Remediation Analysis"; in ISO/SAE 21434 it is **Threat Analysis and Risk Assessment** (the Walkthrough has it right).

---

## D. Smaller inconsistencies

- **DTC 0x06.** The benchmark labels it "Nonce Replay", the Technical Paper's table lists it as "HMAC invalid", and the code reports both failures with 0x06. The table has no dedicated replay code.
- **Frame field list.** The Technical Paper lists the frame as Preamble + SOF + Nonce + Epoch + HMAC + CRC32 (440 bits). The benchmark and code also include a 1-byte slot ID inside the 392-bit payload, and the code adds a 32-bit postamble.
- **Demo shortcuts.** The manual-override button is always visible in the simulation. In the design it is meant to stay unpowered until the listening window expires. The Walkthrough flags this.
- **Hidden service gateway.** The "three empty PIN entries unlock the technician prompt" trigger (claim 12) is security through obscurity. Now that it is public, treat it as a demo feature only and replace it with properly authenticated service access in any real design.

---

## E. Legal and status markings

- Pages marked "Confidential", "Strictly Confidential (NDA)", "Patent Pending", "MNDA Required", "Commercial Licensing", or describing the code as a "trade secret" are **void**. Everything in this repository is released under Apache 2.0.
- UK application GB2619997.6 was never pursued (no search or examination fees paid) and is lapsing. Nothing here is patent pending.
- The "Executive Briefing & Commercial Patent Assignment Proposal" is a historical pitch document with no commercial terms in force.
- I'm not a lawyer and nothing here is legal advice.

---

## F. Design caveats (not mistakes, but worth stating)

- The stolen-key-plus-PIN fallback is a weaker route around the acoustic factor, by design.
- Sound is not a perfect air-gap. A mic near the phone and a speaker in the car, linked in real time, is a plausible attack that has not been tested.
- Jamming can deny convenience but is intended to fail to the PIN fallback, not to unlock.
- No security review of any kind has been done.
