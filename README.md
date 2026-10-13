# Quackworks Acoustic Lock (QAL)

### An experimental near-ultrasonic "proof you are in the room" token, built as a car-immobilizer simulation

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Simulation](https://img.shields.io/badge/Simulation-Unity%20%7C%20C%23-blue.svg)]()
[![YouTube](https://img.shields.io/badge/YouTube-@Quackworkstudio-red.svg?logo=youtube)](https://www.youtube.com/@Quackworkstudio)
[![Support on Ko-fi](https://img.shields.io/badge/Support-Ko--fi-F16061?style=flat&logo=ko-fi&logoColor=white)](https://ko-fi.com/quackworkstudios)

**Status: hobbyist research prototype. Not security-audited, not production-ready, not a product.**

---
# Quackworks Acoustic Lock (QAL)

### An experimental near-ultrasonic "proof you are in the room" token, built as a car-immobilizer simulation

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Simulation](https://img.shields.io/badge/Simulation-Unity%20%7C%20C%23-blue.svg)]()
[![YouTube](https://img.shields.io/badge/YouTube-@Quackworkstudio-red.svg?logo=youtube)](https://www.youtube.com/@Quackworkstudio)
[![Support on Ko-fi](https://img.shields.io/badge/Support-Ko--fi-F16061?style=flat&logo=ko-fi&logoColor=white)](https://ko-fi.com/quackworkstudios)

**Status: hobbyist research prototype. Not security-audited, not production-ready, not a product.**

**Known mistakes in the documents are listed in [`ERRATA.md`](ERRATA.md). Please read it before relying on any figure or claim in the PDFs.**

---

## What this is, in plain terms

Modern keyless cars can be stolen with a relay attack: thieves bridge the key fob's radio signal from inside your house to the car on your driveway. Radio goes through walls and glass. Sound mostly doesn't, especially at high frequencies.

QAL is my attempt at a second gate that sits *behind* the normal keyless system. After the car wakes up, it still won't release the starter or high-voltage contactors until it hears a short, cryptographically signed, near-ultrasonic (16-17 kHz) audio burst from your unlocked phone, captured by a microphone already inside the cabin.

The idea is that the physical cabin becomes part of the security boundary, and that a thief holding both your key *and* your phone still needs your phone's biometric/PIN unlock to make it speak.

This repository contains everything I produced while working on it: a full Unity Software-in-the-Loop (SIL) simulation (27 C# scripts), and several hundred pages of documentation. I'm releasing all of it under Apache 2.0, as public prior art and in case any of it is useful to someone else.

## Who made this (and how)

I'm a total amateur. I'm not a DSP engineer, a cryptographer, or an automotive professional. I had an idea in my kitchen, and then spent months pushing it well beyond my comfort zone.

I used AI models heavily to help with the maths, the signal processing, and the C#, and cross-checked the work between around six different models. A lot of trial, error, and debugging went into getting the modulator, demodulator, and state machine to work end to end.

**What that means for you as a reader:** this has not been reviewed by professionals. The documents are long and detailed, but length is not correctness. Please read it critically, and please tell me what I got wrong (see [Known issues in the documents](#known-issues-in-the-documents)).

---

## Start here

If you only open one thing, open **`QAL Demo Walkthrough Document.pdf`**. It is the most important file in this repository, because it shows the whole system working and gives you everything needed to reproduce it:

- **The simulation, shot by shot.** Every scene and phase of the demo, with screenshots, explaining what each screen does and which script is behind it.
- **The full source code** of all 27 C# scripts, printed in Appendix A (so you can read the code without installing anything). The parts most relevant to the audio are `QALEncoder.cs` (modulator, Appendix A.25), `UltrasonicDecoder.cs` (demodulator, A.24), `AudioManager.cs` (A.1), and `FakeECU.cs` (the state machine and crypto checks, A.15).
- **The real-device test setup** (Appendix B), including exactly which phone, laptop and microphones I used, the distances, and the noise level. That is the part I'd most like others to repeat and extend.
- **The threat model, error codes, clock integrity, claims-to-code mapping and evidentiary status** (Appendices C to F).

The shorter Technical Paper is a good overview, but the Walkthrough is the primary document, and where the two disagree, the Walkthrough is the better reference. The Patent text and Technical Paper contain several errors that the Walkthrough gets right, so read [`ERRATA.md`](ERRATA.md) alongside them.

The testing is the other half. The point of releasing this is that you can run it yourself, see it work (or see where it fails on *your* hardware), and change it. See [Try it, test it, adjust it](#try-it-test-it-adjust-it) below.

---

## What has actually been tested

| Level | What it is | Status |
|---|---|---|
| **Software loopback** | Encoder output fed straight into the decoder inside Unity. This is what the "50-burst, 0.00% BER" benchmark PDF measures. | Done, documented. It shows the modulator and demodulator agree with each other. It says nothing about real-world noise. |
| **Real devices, over the air** | Three topologies at 48 kHz, using pre-synthesized token audio: (1) Honor 90 Smart phone speaker to the laptop's built-in Realtek mic array, (2) the same phone to an external Fifine K669B USB condenser mic, (3) the laptop's own speakers to its own mic. Laptop: entry-level HP, Intel Pentium Gold. Phone held 10-20 cm from the mic, in an ordinary room at roughly 48-52 dBA background noise. OS audio enhancements (echo cancellation, noise suppression, high-frequency filtering) were bypassed; on my laptop that meant disabling both the HP audio enhancements and the Windows audio enhancements, and it did not work otherwise. | **No errors observed in 50-burst consecutive batches per topology**, with CRC-32 lock and HMAC validation passing. Documented in Appendix B of the Walkthrough. See the caveats below. |
| **Cabin transmission loss** | Measuring how much the 16-17 kHz signal is attenuated by real vehicle glass and seals. | **Not done.** The paper's 30-42 dB figure is a *target*, not a measurement. |
| **In a real car** | Real vehicle microphones, engine/road noise, window positions. | **Not done.** |
| **Security review** | Independent review of protocol, crypto, or implementation. | **Not done.** |

**How to read the over-the-air result.** It is real feasibility evidence on cheap consumer hardware, and I'm proud of it. It is also a small test:

- "No errors in 50 bursts" is not "never fails". Statistically, zero failures in 50 trials is still consistent with a failure rate of up to roughly 6%.
- It was done at 10-20 cm, in one room, with one phone model and one laptop. It does not cover a car cabin, other handsets, pockets, or engine and road noise.
- OS audio processing had to be switched off for it to work (HP's and Windows' enhancements both blocked it; Appendix B.4 explains why). Any laptop or phone with such enhancements left on may suppress the 16-17 kHz carrier, which is a real practical limitation for uses where you can't control the user's settings. The documents argue that car microphones feed raw samples to the audio DSP and don't have this problem. That is plausible, but it is argued, not measured.
- The tests used pre-synthesized token files. As far as I can tell, the live challenge-response mode (where the car emits a challenge and the phone answers) was exercised in simulation, not over the air.
- It measured whether legitimate tokens decode. It was not an attack test: there are no measured numbers yet for replay, relay, jamming, or how far away an outside microphone could still decode.

If you want to turn this into more useful data (success rate vs. distance, noise, phone model, window up/down, a real cabin, and false-accept tests), that would be the single most valuable contribution to this project.

---

## How it works (short version)

- **Modulation:** continuous-phase binary FSK. 16 kHz = bit 0, 17 kHz = bit 1, 10 ms per bit (100 baud), 48 kHz sampling. A short fade-in/out avoids speaker clicks.
- **Optional masking chime:** the data burst can be mixed beneath an audible chime low-passed at 14 kHz (12th-order Butterworth, six biquad sections), leaving a 14-16 kHz guard band. That filter is only about 30 dB down at 16 kHz (some documents wrongly say more than 80 dB; see `ERRATA.md`), so the guard band is quiet rather than silent.
- **Demodulation:** non-coherent I/Q energy detection at each carrier. Compare energy at 17 kHz vs 16 kHz per symbol, so phase shifts from reflections don't matter.
- **Frame:** preamble, start-of-frame marker, slot ID, 8-byte random nonce, timestamp/epoch, HMAC-SHA256 signature, CRC-32.
- **Car logic:** Locked -> Stage 1 (mic listening for 30-45 s after a fob/door event) -> Stage 2 (propulsion released) if a valid token is heard. If nothing valid arrives, a manual PIN fallback wakes up, with progressive lockout after failures.
- **Safety invariant:** the system only gates *starting*. Once moving, it never cuts power because of an acoustic fault.
- **Extras in the simulation:** guest keys with expiry, owner recovery key, hash-chained event log, simulated diagnostic (UDS-style) service mode.

The Technical Paper walks through all of this and maps each patent claim to the C# file that implements it.

---

## Threat model and known limitations

I'd rather you read these here than discover them yourself.

**What it is meant to help with**
- Relay attacks on keyless entry (the RF bridge only gets the thief as far as Stage 1).
- A stolen key fob on its own.
- A stolen key fob *plus* a stolen phone (token needs the phone to be unlocked and a deliberate tap).
- CAN-bus / OBD key-programming tricks that only fool the normal immobilizer.

**What it does not prove, or does not handle**
- **Sound is not a perfect air-gap.** A determined attacker could place a microphone near your phone and a speaker in the car and relay the audio in real time. Single-use nonces and the owner's deliberate tap make this harder, but a one-way chirp cannot do true distance bounding. Nobody has tested how practical this is.
- **Containment is unmeasured.** Glass and door seals attenuating 16-17 kHz is physically plausible, but the specific numbers in the documents are targets.
- **Replay protection is only as good as the verifier's memory.** The simulation tracks used nonces in a simulated store. A real design needs this in tamper-resistant non-volatile storage.
- **The phone holds a symmetric secret.** The car verifies with HMAC, so compromise of the phone's vault compromises the vehicle secret.
- **The PIN fallback is a weaker door.** If the fob and the owner's PIN are both known, the acoustic factor can be bypassed after the timeout.
- **Hardware assumptions are unverified.** Many car microphones and phone speakers roll off at high frequencies. Whether real cabin mics pass 16-17 kHz cleanly is unknown. Some people (especially younger ones) and some pets can hear near 16 kHz.
- **Jamming** can deny convenience, but the design is meant to fail to the PIN fallback, not to unlock.
- **Crypto and safety claims are design intent.** PBKDF2/AES vaulting, HSM binding, ISO 26262 and ISO/SAE 21434 alignment, and UNECE R155/R156 mentions describe what a production design would aim for. Nothing here has been certified or formally assessed.

## Known issues in the documents

The PDFs are uploaded exactly as written, so they contain mistakes, inconsistencies, and a few claims that go further than the evidence. **Every one I know of is listed in [`ERRATA.md`](ERRATA.md).** Where a document and `ERRATA.md` disagree, trust `ERRATA.md`.

The ones most worth knowing about up front:

- **The filter figure is wrong in several places.** The patent text and Technical Paper say ">80 dB at 16 kHz". The real figure for that filter is about 30 dB (the Walkthrough has it right).
- **The 1.5 s latency check conflicts with a roughly 4.4 s token**, and the anti-relay argument built on it is weak. Treat relay resistance as unproven.
- **The "Demodulation Benchmark" PDF is a software loopback**, not an over-the-air test. The real over-the-air batches are in Appendix B of the Walkthrough, and they are small.
- **Two different freshness (anti-replay) mechanisms are described as if they were one**, and the replay registry in the demo is only in memory.
- **The "Confidential", "Patent Pending", "NDA" and "trade secret" markings are void.** Everything is Apache 2.0.

---

## What is in this repository

| Path | What it is |
|---|---|
| `Assets/`, `Packages/`, `ProjectSettings/` | The Unity project (27 modular C# scripts) running the full SIL simulation. |
| `QAL Technical Paper.pdf` | 36-page architecture and threat specification: state machine, DSP pipeline, crypto vault, TARA table, claims-to-code mapping. A good overview; read it alongside the Walkthrough. |
| **`QAL Demo Walkthrough Document.pdf`** | **The most important document. Start here.** The 360-page implementation walkthrough: the simulation shot by shot with screenshots, the full source of all 27 scripts (Appendix A), the over-the-air test setup (Appendix B), threat model and TARA (C), DTCs and clock integrity (D), claims traceability (E), evidentiary status (F), and prior-art notes (G). |
| `Physical-Layer Demodulation Benchmark Document.pdf` | The 50-burst **software loopback** report. |
| `Patent Combined Document.pdf`, `Patent Diagrams Document.pdf` | The text and figures of the UK application (GB2619997.6, claims 1-25). |
| `Executive Briefing & Commercial Patent Assignment Proposal.pdf` | A commercial pitch document from when I was trying to license the idea. Historical only; no commercial terms apply. |
| `QAL Simulation Passwords, Access Codes & Editor Tools.md` | Demo credentials and editor helpers for the *simulation only*. They protect nothing real. |
| `ERRATA.md` | Every known mistake, inconsistency and overstated claim in the documents. |
| `LICENSE` | Apache 2.0. |

> Earlier versions of this README pointed at a `/docs/` folder with different filenames. The files above are where they actually live.

---

## Try it, test it, adjust it

Everything you need is in the Walkthrough. The full source of all 27 scripts is in Appendix A, the shot-by-shot screenshots show what each screen should look like, and Appendix B describes my test setup. If you're comfortable reading C# (or pasting it into an AI to explain it), you can have it running quickly.

**Running it:** clone the repo, open it in Unity 2022 LTS, open the main scene (`QAL.unity` is the start of the flow), and press Play.

**Token files:** the demo exports the `.wav` token automatically. The code saves it to a `QAL_AudioKeys` folder on your desktop (on my Windows 10 setup it appears there automatically). If you can't find it, search for `QAL_TestKey*.wav`. Exported tokens are single-use per session, so regenerate between bursts.

**One thing that will trip you up: turn off audio enhancements.** For the over-the-air tests to work on my laptop, I had to disable *both* the HP audio software enhancements *and* the Windows audio enhancements. With them on, the near-ultrasonic signal didn't get through. If your tests mysteriously fail, check this first, and look for any vendor audio software (Realtek, HP, Dolby and similar) as well as the OS settings.

**What I'd most like to see:** try it on your own hardware, and tell me what happened, whether it worked or not. A GitHub issue with a table like this is plenty:

| Phone / speaker | Mic / device | Distance | Noise | Bursts | Decoded OK | Notes |
|---|---|---|---|---|---|---|
| | | | | | | |

Good experiments: different phones and cases, phone in a pocket, background music, other microphones, greater distances, higher carrier frequencies or faster baud rates until it breaks, and attacks such as replay or relaying the audio. The modem parameters (`QALEncoder.cs`, `UltrasonicDecoder.cs`) are meant to be changed.

If you have a car and a safe way to try it, even better. Please do not connect experimental code to anything that controls a real vehicle.

> **Demo video of the over-the-air test:** *[link to be added]*

---

## Ideas for other uses (untested)

The car is the only scenario this project was designed and simulated for. These are just ideas for others who want to play with the modem:

- **Apartment intercoms, lobby doors, elevator call boxes.** These already have microphones and speakers, and people already pause there. Same threat shape as the car: a remote relay attacker with no physical presence.
- **A presence signal for computer login.** The idea is that a phone-emitted acoustic token could be one extra factor showing someone is physically at the machine. **Important caveat:** if the computer itself is already compromised (for example by a Remote Access Trojan), the malware may be able to read the microphone stream, feed fake audio to the verifier, or skip the check entirely. It would only help if verification runs somewhere the malware can't reach. I have not analyzed this properly, so please don't treat it as protection against a compromised host.
- **Server-room or facility doors, kiosks, point-of-sale terminals.** Plausible as a secondary factor, completely unexplored.

Adapting any of these means changing the transport buffers, state machine, and host interface. The modulation, framing, and verification code is the reusable part.

---

## Related work

QAL is not the first use of sound for proximity or authentication, and I make no claim that it is. Things you should look at alongside it:

- Choi, Seo & Lee (2018), *Sound-Proximity: 2-Factor Authentication against Relay Attack on Passive Keyless Entry and Start System*, Journal of Advanced Transportation.
- Karapanos et al. (2015), *Sound-Proof: Usable Two-Factor Authentication Based on Ambient Sound*, USENIX Security.
- US 8,712,365 B2: crypto-acoustic credentials for digitally controlled locks (a phone emits encrypted tones picked up by a microphone at a lock).
- US 9,098,865 B2 (ultrasonic near-field payments), US 7,966,497 B2 (acoustic two-factor authentication), US 2019/0025402 A1 (in-cabin phone positioning by sound), and the Volvo relay-attack family, US 10,759,383 B2 and continuations.
- ggwave: an open-source data-over-sound library.

What QAL adds, as far as I know, is a specific combination: the car-immobilizer state machine and interlock gating, a user-intent (unlock-and-tap) gate, near-ultrasonic CP-FSK burst design, optional masking beneath an audible chime, and the dormant-fallback logic. I may well be wrong about novelty. That's what the prior-art label is for.

---

## Patent status

I filed a UK application (GB2619997.6) to get a priority date and then **chose not to continue it**: no search or examination fees were paid and it is being allowed to lapse. Nothing is "patent pending". The files in this repository containing "Patent Pending", "MNDA" or "Commercial Licensing" language are historical and superseded by Apache 2.0.

The Apache 2.0 license includes a patent grant from contributors to users. I'm not a lawyer, and nothing here is legal advice.

---

## Contributing

Things that would help most, in rough order:

1. **Repeat and extend the over-the-air tests** (see above). **Real measurements:** success rate vs. distance, noise, phone and mic models, window up/down, a real car cabin.
2. **A standalone modem library** (encode to WAV, decode from WAV, test vectors) outside Unity, in any language.
3. **Attack attempts:** replay, real-time relay, jamming. Tell me what breaks.
4. **Corrections to the documents.** Issues and pull requests are welcome, and being told I'm wrong is useful.
5. **Ports** to embedded C or other platforms.

---

## The story

The idea started in my kitchen: sound gets trapped by glass and walls, radio doesn't, so where could that be useful? Relay car theft was the obvious answer.

After the simulation worked, I filed a patent application and tried to take it down the commercial route, and spent months in postal codes, portals and silent gatekeepers. I also had an honest reality check: for cars, pulling out your phone is more hassle than people will accept from a keyless system, and the emergency-fallback problems are real.

So rather than let months of work sit on a hard drive, I'm giving it away. If any of it is useful, or if it just sparks a better idea, take it. If you want to say thanks, there's a Ko-fi link below, but it's entirely optional.

☕ **[Support on Ko-fi](https://ko-fi.com/quackworkstudios)**

---

## License

Released under the **[Apache License, Version 2.0](LICENSE)**. You're free to use, modify, distribute, and integrate this into open-source, educational, or commercial projects.


## What this is, in plain terms

Modern keyless cars can be stolen with a relay attack: thieves bridge the key fob's radio signal from inside your house to the car on your driveway. Radio goes through walls and glass. Sound mostly doesn't, especially at high frequencies.

QAL is my attempt at a second gate that sits *behind* the normal keyless system. After the car wakes up, it still won't release the starter or high-voltage contactors until it hears a short, cryptographically signed, near-ultrasonic (16-17 kHz) audio burst from your unlocked phone, captured by a microphone already inside the cabin.

The idea is that the physical cabin becomes part of the security boundary, and that a thief holding both your key *and* your phone still needs your phone's biometric/PIN unlock to make it speak.

This repository contains everything I produced while working on it: a full Unity Software-in-the-Loop (SIL) simulation (27 C# scripts), and several hundred pages of documentation. I'm releasing all of it under Apache 2.0, as public prior art and in case any of it is useful to someone else.

## Who made this (and how)

I'm a total amateur. I'm not a DSP engineer, a cryptographer, or an automotive professional. I had an idea in my kitchen, and then spent months pushing it well beyond my comfort zone.

I used AI models heavily to help with the maths, the signal processing, and the C#, and cross-checked the work between around six different models. A lot of trial, error, and debugging went into getting the modulator, demodulator, and state machine to work end to end.

**What that means for you as a reader:** this has not been reviewed by professionals. The documents are long and detailed, but length is not correctness. Please read it critically, and please tell me what I got wrong (see [Known issues in the documents](#known-issues-in-the-documents)).

---

## Start here

If you only open one thing, open **`QAL Demo Walkthrough Document.pdf`**. It is the most important file in this repository, because it shows the whole system working and gives you everything needed to reproduce it:

- **The simulation, shot by shot.** Every scene and phase of the demo, with screenshots, explaining what each screen does and which script is behind it.
- **The full source code** of all 27 C# scripts, printed in Appendix A (so you can read the code without installing anything). The parts most relevant to the audio are `QALEncoder.cs` (modulator, Appendix A.25), `UltrasonicDecoder.cs` (demodulator, A.24), `AudioManager.cs` (A.1), and `FakeECU.cs` (the state machine and crypto checks, A.15).
- **The real-device test setup** (Appendix B), including exactly which phone, laptop and microphones I used, the distances, and the noise level. That is the part I'd most like others to repeat and extend.
- **The threat model, error codes, clock integrity, claims-to-code mapping and evidentiary status** (Appendices C to F).

The shorter Technical Paper is a good overview, but the Walkthrough is the primary document, and where the two disagree, the Walkthrough is the better reference.

The testing is the other half. The point of releasing this is that you can run it yourself, see it work (or see where it fails on *your* hardware), and change it. See [Try it, test it, adjust it](#try-it-test-it-adjust-it) below.

---

## What has actually been tested

| Level | What it is | Status |
|---|---|---|
| **Software loopback** | Encoder output fed straight into the decoder inside Unity. This is what the "50-burst, 0.00% BER" benchmark PDF measures. | Done, documented. It shows the modulator and demodulator agree with each other. It says nothing about real-world noise. |
| **Real devices, over the air** | Three topologies at 48 kHz, using pre-synthesized token audio: (1) Honor 90 Smart phone speaker to the laptop's built-in Realtek mic array, (2) the same phone to an external Fifine K669B USB condenser mic, (3) the laptop's own speakers to its own mic. Laptop: entry-level HP, Intel Pentium Gold. Phone held 10-20 cm from the mic, in an ordinary room at roughly 48-52 dBA background noise. OS audio enhancements (echo cancellation, noise suppression, high-frequency filtering) were bypassed; on my laptop that meant disabling both the HP audio enhancements and the Windows audio enhancements, and it did not work otherwise. | **No errors observed in 50-burst consecutive batches per topology**, with CRC-32 lock and HMAC validation passing. Documented in Appendix B of the Walkthrough. See the caveats below. |
| **Cabin transmission loss** | Measuring how much the 16-17 kHz signal is attenuated by real vehicle glass and seals. | **Not done.** The paper's 30-42 dB figure is a *target*, not a measurement. |
| **In a real car** | Real vehicle microphones, engine/road noise, window positions. | **Not done.** |
| **Security review** | Independent review of protocol, crypto, or implementation. | **Not done.** |

**How to read the over-the-air result.** It is real feasibility evidence on cheap consumer hardware, and I'm proud of it. It is also a small test:

- "No errors in 50 bursts" is not "never fails". Statistically, zero failures in 50 trials is still consistent with a failure rate of up to roughly 6%.
- It was done at 10-20 cm, in one room, with one phone model and one laptop. It does not cover a car cabin, other handsets, pockets, or engine and road noise.
- OS audio processing had to be switched off for it to work (HP's and Windows' enhancements both blocked it). Real microphone pipelines in cars, and in any laptop with enhancements enabled, may filter or process the 16-17 kHz band the same way, and that is untested. This is a real practical limitation, not just a test detail.
- The tests used pre-synthesized token files. As far as I can tell, the live challenge-response mode (where the car emits a challenge and the phone answers) was exercised in simulation, not over the air.
- It measured whether legitimate tokens decode. It was not an attack test: there are no measured numbers yet for replay, relay, jamming, or how far away an outside microphone could still decode.

If you want to turn this into more useful data (success rate vs. distance, noise, phone model, window up/down, a real cabin, and false-accept tests), that would be the single most valuable contribution to this project.

---

## How it works (short version)

- **Modulation:** continuous-phase binary FSK. 16 kHz = bit 0, 17 kHz = bit 1, 10 ms per bit (100 baud), 48 kHz sampling. A short fade-in/out avoids speaker clicks.
- **Optional masking chime:** the data burst can be mixed beneath an audible chime low-passed at 14 kHz (12th-order Butterworth, six biquad sections), leaving a 14-16 kHz guard band.
- **Demodulation:** non-coherent I/Q energy detection at each carrier. Compare energy at 17 kHz vs 16 kHz per symbol, so phase shifts from reflections don't matter.
- **Frame:** preamble, start-of-frame marker, slot ID, 8-byte random nonce, timestamp/epoch, HMAC-SHA256 signature, CRC-32.
- **Car logic:** Locked -> Stage 1 (mic listening for 30-45 s after a fob/door event) -> Stage 2 (propulsion released) if a valid token is heard. If nothing valid arrives, a manual PIN fallback wakes up, with progressive lockout after failures.
- **Safety invariant:** the system only gates *starting*. Once moving, it never cuts power because of an acoustic fault.
- **Extras in the simulation:** guest keys with expiry, owner recovery key, hash-chained event log, simulated diagnostic (UDS-style) service mode.

The Technical Paper walks through all of this and maps each patent claim to the C# file that implements it.

---

## Threat model and known limitations

I'd rather you read these here than discover them yourself.

**What it is meant to help with**
- Relay attacks on keyless entry (the RF bridge only gets the thief as far as Stage 1).
- A stolen key fob on its own.
- A stolen key fob *plus* a stolen phone (token needs the phone to be unlocked and a deliberate tap).
- CAN-bus / OBD key-programming tricks that only fool the normal immobilizer.

**What it does not prove, or does not handle**
- **Sound is not a perfect air-gap.** A determined attacker could place a microphone near your phone and a speaker in the car and relay the audio in real time. Single-use nonces and the owner's deliberate tap make this harder, but a one-way chirp cannot do true distance bounding. Nobody has tested how practical this is.
- **Containment is unmeasured.** Glass and door seals attenuating 16-17 kHz is physically plausible, but the specific numbers in the documents are targets.
- **Replay protection is only as good as the verifier's memory.** The simulation tracks used nonces in a simulated store. A real design needs this in tamper-resistant non-volatile storage.
- **The phone holds a symmetric secret.** The car verifies with HMAC, so compromise of the phone's vault compromises the vehicle secret.
- **The PIN fallback is a weaker door.** If the fob and the owner's PIN are both known, the acoustic factor can be bypassed after the timeout.
- **Hardware assumptions are unverified.** Many car microphones and phone speakers roll off at high frequencies. Whether real cabin mics pass 16-17 kHz cleanly is unknown. Some people (especially younger ones) and some pets can hear near 16 kHz.
- **Jamming** can deny convenience, but the design is meant to fail to the PIN fallback, not to unlock.
- **Crypto and safety claims are design intent.** PBKDF2/AES vaulting, HSM binding, ISO 26262 and ISO/SAE 21434 alignment, and UNECE R155/R156 mentions describe what a production design would aim for. Nothing here has been certified or formally assessed.

## Known issues in the documents

The PDFs are uploaded as originally written, so they contain mistakes I'm aware of (and surely others I'm not). Where a document and this README disagree, **this README is more accurate**.

- Page headers say "Confidential", "Patent Pending", "MNDA" and call the code a trade secret. All of that is **void**. Everything is Apache 2.0.
- The Technical Paper expands "TARA" as "Threat Assessment and Remediation Analysis". In ISO/SAE 21434 it means **Threat Analysis and Risk Assessment** (the Walkthrough has it right).
- The relay-latency argument (round trip under 1.5 s) is weak: typical network delays are *below* 1.5 s, so latency alone does not stop a relay. Treat that defense as unproven. In the simulation code the latency check compares timestamps inside one program, so it has not been exercised against a real relay.
- There are two freshness modes, and the documents do not always say which one they mean. The *unidirectional* mode (what the phone-to-laptop tests used) sends a nonce and an expiry and relies on a consumed-nonce registry plus a 60-second window. The *bidirectional challenge-response* mode (the car emits a challenge seed, answered with a counter-bound HMAC) is the intended production design, but is only exercised in simulation. The Technical Paper mixes the two descriptions, so read the Walkthrough's Appendix C and D alongside it.
- The benchmark report lists DTC 0x06 as "Nonce Replay", while the Technical Paper's DTC table defines 0x06 as "HMAC invalid". In the code both failures report 0x06. The demodulator timing is also described differently in two documents (7 ms integration + guard vs. the full 10 ms symbol window).
- The simulation source contains hardcoded demo credentials and static salts, plus unmasked console logging, as test scaffolding (the Walkthrough says so). None of it is a model for production key handling.
- The hash-chained audit log is described as "immutable". A bare SHA-256 chain can be recomputed by someone with write access to the storage; a production design would need a keyed MAC or signature over the chain head.
- The "Demodulation Benchmark" PDF is a **software loopback** (encoder straight into decoder inside Unity), not an over-the-air test. The over-the-air batches are documented separately in Appendix B of the Walkthrough.

---

## What is in this repository

| Path | What it is |
|---|---|
| `Assets/`, `Packages/`, `ProjectSettings/` | The Unity project (27 modular C# scripts) running the full SIL simulation. |
| `QAL Technical Paper.pdf` | 36-page architecture and threat specification: state machine, DSP pipeline, crypto vault, TARA table, claims-to-code mapping. A good overview; read it alongside the Walkthrough. |
| **`QAL Demo Walkthrough Document.pdf`** | **The most important document. Start here.** The 360-page implementation walkthrough: the simulation shot by shot with screenshots, the full source of all 27 scripts (Appendix A), the over-the-air test setup (Appendix B), threat model and TARA (C), DTCs and clock integrity (D), claims traceability (E), evidentiary status (F), and prior-art notes (G). |
| `Physical-Layer Demodulation Benchmark Document.pdf` | The 50-burst **software loopback** report. |
| `Patent Combined Document.pdf`, `Patent Diagrams Document.pdf` | The text and figures of the UK application (GB2619997.6, claims 1-25). |
| `Executive Briefing & Commercial Patent Assignment Proposal.pdf` | A commercial pitch document from when I was trying to license the idea. Historical only; no commercial terms apply. |
| `QAL Simulation Passwords, Access Codes & Editor Tools.md` | Demo credentials and editor helpers for the *simulation only*. They protect nothing real. |
| `LICENSE` | Apache 2.0. |

> Earlier versions of this README pointed at a `/docs/` folder with different filenames. The files above are where they actually live.

---

## Try it, test it, adjust it

Everything you need is in the Walkthrough. The full source of all 27 scripts is in Appendix A, the shot-by-shot screenshots show what each screen should look like, and Appendix B describes my test setup. If you're comfortable reading C# (or pasting it into an AI to explain it), you can have it running quickly.

**Running it:** clone the repo, open it in Unity 2022 LTS, open the main scene (`QAL.unity` is the start of the flow), and press Play.

**Token files:** the demo exports the `.wav` token automatically. On my Windows 10 setup it lands on the desktop (the Walkthrough shows a `QAL_AudioKeys` folder). If you can't find it, search for `QAL_TestKey*.wav`. Exported tokens are single-use per session, so regenerate between bursts.

**One thing that will trip you up: turn off audio enhancements.** For the over-the-air tests to work on my laptop, I had to disable *both* the HP audio software enhancements *and* the Windows audio enhancements. With them on, the near-ultrasonic signal didn't get through. If your tests mysteriously fail, check this first, and look for any vendor audio software (Realtek, HP, Dolby and similar) as well as the OS settings.

**What I'd most like to see:** try it on your own hardware, and tell me what happened, whether it worked or not. A GitHub issue with a table like this is plenty:

| Phone / speaker | Mic / device | Distance | Noise | Bursts | Decoded OK | Notes |
|---|---|---|---|---|---|---|
| | | | | | | |

Good experiments: different phones and cases, phone in a pocket, background music, other microphones, greater distances, higher carrier frequencies or faster baud rates until it breaks, and attacks such as replay or relaying the audio. The modem parameters (`QALEncoder.cs`, `UltrasonicDecoder.cs`) are meant to be changed.

If you have a car and a safe way to try it, even better. Please do not connect experimental code to anything that controls a real vehicle.

> **Demo video of the over-the-air test:** *[link to be added]*

---

## Ideas for other uses (untested)

The car is the only scenario this project was designed and simulated for. These are just ideas for others who want to play with the modem:

- **Apartment intercoms, lobby doors, elevator call boxes.** These already have microphones and speakers, and people already pause there. Same threat shape as the car: a remote relay attacker with no physical presence.
- **A presence signal for computer login.** The idea is that a phone-emitted acoustic token could be one extra factor showing someone is physically at the machine. **Important caveat:** if the computer itself is already compromised (for example by a Remote Access Trojan), the malware may be able to read the microphone stream, feed fake audio to the verifier, or skip the check entirely. It would only help if verification runs somewhere the malware can't reach. I have not analyzed this properly, so please don't treat it as protection against a compromised host.
- **Server-room or facility doors, kiosks, point-of-sale terminals.** Plausible as a secondary factor, completely unexplored.

Adapting any of these means changing the transport buffers, state machine, and host interface. The modulation, framing, and verification code is the reusable part.

---

## Related work

QAL is not the first use of sound for proximity or authentication, and I make no claim that it is. Things you should look at alongside it:

- Choi, Seo & Lee (2018), *Sound-Proximity: 2-Factor Authentication against Relay Attack on Passive Keyless Entry and Start System*, Journal of Advanced Transportation.
- Karapanos et al. (2015), *Sound-Proof: Usable Two-Factor Authentication Based on Ambient Sound*, USENIX Security.
- US 8,712,365 B2: crypto-acoustic credentials for digitally controlled locks (a phone emits encrypted tones picked up by a microphone at a lock).
- US 9,098,865 B2 (ultrasonic near-field payments), US 7,966,497 B2 (acoustic two-factor authentication), US 2019/0025402 A1 (in-cabin phone positioning by sound), and the Volvo relay-attack family, US 10,759,383 B2 and continuations.
- ggwave: an open-source data-over-sound library.

What QAL adds, as far as I know, is a specific combination: the car-immobilizer state machine and interlock gating, a user-intent (unlock-and-tap) gate, near-ultrasonic CP-FSK burst design, optional masking beneath an audible chime, and the dormant-fallback logic. I may well be wrong about novelty. That's what the prior-art label is for.

---

## Patent status

I filed a UK application (GB2619997.6) to get a priority date and then **chose not to continue it**: no search or examination fees were paid and it is being allowed to lapse. Nothing is "patent pending". The files in this repository containing "Patent Pending", "MNDA" or "Commercial Licensing" language are historical and superseded by Apache 2.0.

The Apache 2.0 license includes a patent grant from contributors to users. I'm not a lawyer, and nothing here is legal advice.

---

## Contributing

Things that would help most, in rough order:

1. **Repeat and extend the over-the-air tests** (see above). **Real measurements:** success rate vs. distance, noise, phone and mic models, window up/down, a real car cabin.
2. **A standalone modem library** (encode to WAV, decode from WAV, test vectors) outside Unity, in any language.
3. **Attack attempts:** replay, real-time relay, jamming. Tell me what breaks.
4. **Corrections to the documents.** Issues and pull requests are welcome, and being told I'm wrong is useful.
5. **Ports** to embedded C or other platforms.

---

## The story

The idea started in my kitchen: sound gets trapped by glass and walls, radio doesn't, so where could that be useful? Relay car theft was the obvious answer.

After the simulation worked, I filed a patent application and tried to take it down the commercial route, and spent months in postal codes, portals and silent gatekeepers. I also had an honest reality check: for cars, pulling out your phone is more hassle than people will accept from a keyless system, and the emergency-fallback problems are real.

So rather than let months of work sit on a hard drive, I'm giving it away. If any of it is useful, or if it just sparks a better idea, take it. If you want to say thanks, there's a Ko-fi link below, but it's entirely optional.

☕ **[Support on Ko-fi](https://ko-fi.com/quackworkstudios)**

---

## License

Released under the **[Apache License, Version 2.0](LICENSE)**. You're free to use, modify, distribute, and integrate this into open-source, educational, or commercial projects.

