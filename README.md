# Quackworks Acoustic Lock (QAL)
### Out-of-Band Acoustic Authentication for Physical-Presence Verification

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![Simulation](https://img.shields.io/badge/Simulation-Unity%20%7C%20C%23-blue.svg)]()
[![Documentation](https://img.shields.io/badge/Docs-Technical%20Dossier-green.svg)](docs/)
[![YouTube](https://img.shields.io/badge/YouTube-@Quackworkstudio-red.svg?logo=youtube)](https://www.youtube.com/@Quackworkstudio)
[![Support on Ko-fi](https://img.shields.io/badge/Support-Ko--fi-F16061?style=flat&logo=ko-fi&logoColor=white)](https://ko-fi.com/quackworkstudios)

Quackworks Acoustic Lock (QAL) is an experimental, software-defined acoustic authentication protocol designed to enforce strict physical-presence verification across an acoustic air-gap. 

By transmitting a near-ultrasonic, cryptographically bound token between a mobile device and existing microphone hardware, the protocol verifies local presence without requiring specialized secondary radio hardware (such as UWB, BLE beacons, or RFID transceivers). Because sound pressure waves are physically confined by glass, weatherstripping, and interior walls, the physical room or cabin boundary acts as the security boundary, neutralizing remote relays and digital spoofing.

Rather than letting this project sit locked on a hard drive behind patent office bureaucracy and corporate red tape, I have open-sourced the entire codebase, technical dossiers, and attempted patent documentation under the Apache 2.0 License for anyone to study, adapt, or build upon.

---

## What Is in This Repository

* `/src/` or Root: Complete Unity project codebase (27 modular C# scripts) running a full Software-in-the-Loop (SIL) simulation.
* `/docs/QAL_Executive_Briefing.pdf`: 1-page commercial and technical overview briefing.
* `/docs/QAL_Technical_Paper.pdf`: Core architectural white paper and threat model breakdown.
* `/docs/QAL_Patent_Specification_and_Claims.pdf`: Full attempted patent filing text (UK Application No. GB2619997.6, Claims 1 to 25).
* `/docs/QAL_Patent_Diagrams.pdf`: Complete patent figures and state-transition schematics (Figures 1 through 6).
* `/docs/QAL_Physical_Layer_Demodulation_Benchmark.pdf`: Empirical validation report covering 50-burst evaluation batches and over-the-air bench testing.
* `/docs/QAL_Demo_Walkthrough_Dossier.pdf`: Comprehensive 360-page engineering implementation dossier and protocol walkthrough.

> ### Note on Patent Status & Legacy Markings in the Documents
> An initial UK Patent Application (No. GB2619997.6) was lodged to establish an official priority timestamp. However, **no filing fees were paid, no search fees were paid, the application was never digitally linked, and it will not be pursued or prosecuted further**. The filing is being allowed to lapse permanently in favor of public release.
> 
> The PDF dossiers, benchmark reports, and technical papers included in `/docs` are uploaded in their original, complete, and unedited form for archival and technical completeness. Any legacy markings inside these files referring to "Patent Pending", "Commercial Licensing", "MNDA Required", or password-protected demo portals are completely superseded by this repository's **Apache 2.0 License**. 
> 
> All external references to `quackworkstudios/qal` remain valid, password restrictions on demonstration portals have been removed, and the entire project is released freely as public defensive prior art.

---

## Video Demonstrations

The complete 12-part technical video walkthrough of the Software-in-the-Loop simulation running inside Unity is publicly available on YouTube:

▶️ **[Watch the Demonstrations on YouTube (@Quackworkstudio)](https://www.youtube.com/@Quackworkstudio)**

*(All password-protected portal restrictions referenced in earlier documentation have been removed. The videos are now completely open to the public).*

---

## Core Protocol & Architecture

* **Physical Acoustic Air-Gap:** Radio frequencies penetrate domestic walls and vehicle glass, making them vulnerable to wireless amplifier bridging. Near-ultrasonic acoustic waves (16 kHz to 22 kHz) undergo severe transmission loss through structural glass and seals, keeping authentication physically contained.
* **Zero Added Hardware BOM:** Designed to reuse standard pre-existing audio equipment (microphones, head-units, DSP processors) operating at standard 44.1 kHz or 48.0 kHz sampling rates.
* **Acoustic Modulation:** Continuous-Phase Frequency-Shift Keying (CP-FSK / CP-BFSK) to eliminate phase discontinuities and audible transient clicks.
* **Steganographic Layering:** The near-ultrasonic payload is layered beneath an audible chime (< 14 kHz) separated by an unmodulated spectral guard band (14 kHz to 16 kHz) shaped by a 12th-order Butterworth low-pass filter.
* **Demodulation:** Non-coherent In-Phase and Quadrature (I/Q) energy detection across discrete frequency bins, ensuring robust bit recovery regardless of phase cancellation.
* **Security & Integrity:** Dynamic single-use rolling nonces, HMAC-SHA256 credentials, CRC32 error checks, and state-gated dormant fallbacks to prevent parallel brute-force attacks.

---

## Beyond Automotive: Alternative Applications & Adaptations

> **Implementation Note:** The reference code and Unity simulation in this repository are currently configured around automotive vehicle states, ECU interlocks, and starter relay gating. Adapting this protocol to the use cases below will require modifications to the transport buffers, state machines, and host interface logic. However, the underlying continuous-phase modulation, packet framing, and cryptographic verification pipelines are completely transferable.

While originally prototyped as an automotive immobilizer, the core acoustic engine can be adapted to any system equipped with a microphone and a speaker:

### 1. RAT-Proof Workstation Login & Local 2FA
Remote Access Trojans (RATs), credential-stuffing bots, keyloggers, and compromised remote desktop tools (RDP/VNC) allow remote adversaries to inject passwords and software prompts from across the globe.
* By requiring a local acoustic handshake between an authorized smartphone app and the computer's built-in microphone, the operating system (e.g. a custom PAM module on Linux or a Windows credential provider) can verify that a legitimate human is physically sitting in front of the desk.
* An attacker on the other side of the planet cannot inject localized acoustic pressure waves into the room's physical microphone, providing true physical presence verification without needing dedicated USB hardware security keys.

### 2. Smart Apartments, Elevators, and Lost Fob Replacement
Property managers and landlords spend significant money replacing and reprogramming lost plastic RFID key fobs and cards.
* Building intercoms, turnstiles, and elevator call boxes almost always have pre-existing microphones and speakers installed.
* Residents can simply tap an app to chirp an ephemeral, near-ultrasonic token to buzz open the lobby door or select their floor in the elevator.
* Because building entry already involves a natural physical pause, user friction is virtually zero compared to automotive driving scenarios.

### 3. Server Rooms and High-Security Facility Access
High-security areas (data centers, server vaults, hospital medicine rooms) often require secondary physical factors. Acoustic authentication provides an out-of-band second line of defense that cannot be sniffed, boosted, or cloned through walls by external radio frequency scanners.

### 4. Shared Public Kiosks & Point-of-Sale Terminals
Can be used to authorize transactions or unlock sensitive terminal sessions on shared hardware without requiring physical contact, NFC chip pairing friction, or optical QR code alignment.

---

## Running the Simulation

1. Clone this repository.
2. Open the project in the **Unity Editor** (tested on Unity 2022 LTS).
3. Open the primary simulation scene and press **Play**.
4. The simulation models the mobile transmitter, acoustic attenuation, multi-microphone capture, and receiver state-machine transitions in real time.

*(Note: The simulation is designed to run inside the Unity Editor Play Mode to support real-time simulated device hooks and virtualized runtime inspectors).*

---

## The Story Behind the Project

Let's be completely honest: I didn't sit down and teach myself advanced DSP mathematics from a textbook. 

The idea started while sitting in my kitchen. I thought: *sound pressure waves get physically trapped by glass and walls, whereas radio waves pass right through them. Where could that be useful?* Relay car theft immediately jumped out: criminals stealing £60k cars in under 60 seconds with cheap radio repeaters because key fobs leak RF through front doors.

To bring the idea to life, I used AI to help me wrestle with the underlying math, frequency modulation, and C# scripts far more quickly than I could do so on my own. What followed was a brutal amount of trial, error, and painful debugging to get all the pieces (pulse shaping, I/Q quadrature detection, buffer management, and vehicle state-machine loops) actually working in a real simulation loop. Along the way, I cross-referenced the work across around six different AI models (who love finding flaws in other models and trying to outdo them). What resulted was a very carefully documented project alongside a full Unity Software-in-the-Loop (SIL) simulation running end-to-end.

Once it was running, I filed a 25-claim UK patent application (GB2619997.6) and tried to navigate the corporate world. What followed was months of administrative bureaucracy: waiting months for physical postal codes from the patent office, dealing with portal issues, and hitting silent corporate gatekeepers. 

Over time, I also had an honest engineering reality check: while the acoustic physics are solid, automotive adoption has too much user friction ("too faffy" to pull out a phone compared to passive fobs) and too many emergency failsafe headaches. 

The concept is probably much better suited for **apartment intercoms, elevators, or RAT-proof computer logins** where people already pause to authenticate.

Rather than spending any money on filing fees, searches, or legal retainers, I deliberately never completed or paid for the patent application, and am letting it lapse while open-sourcing the entire project, codebase, and documentation as **defensive prior art**. 

Do whatever you want with it! If you appreciate the effort of this project or find it helpful, please consider supporting me with a small donation.

---

## Support the Creator

This project was built entirely independently with personal funds and on modest hardware. If this simulation code, the DSP algorithms, or the documentation helped your research, saved you weeks of development, or sparked an idea:

☕ **[Buy Me a Coffee on Ko-fi](https://ko-fi.com/quackworkstudios)**

---

## License

Released under the **[Apache License, Version 2.0](LICENSE)**. 

You are free to use, modify, distribute, and integrate this software into open-source, educational, or commercial projects. The Apache 2.0 License includes an explicit grant of patent rights and a defensive patent retaliation clause to protect the developer community.
