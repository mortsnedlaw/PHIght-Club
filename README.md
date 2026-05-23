# PHIght Club

**Windows-native DICOM de-identification and export pipeline for preparing imaging datasets for AI training, validation, and vendor handoff.**

> You do not talk about PHI.

PHIght Club is intended to be a single Windows desktop application that can receive, inspect, de-identify, pseudonymize and export DICOM studies while preserving image integrity.

---

## Svenska

**PHIght Club** är tänkt som en fristående Windows-applikation för säker export av DICOM-data till exempelvis AI-träning, validering, leverantörsanalys eller forskningsyta.

Appen ska kunna:

- ta emot DICOM via lokal **DICOM Storage SCP**,
- importera DICOM från mapp,
- anonymisera eller pseudonymisera metadata,
- remappa PatientID, PatientName och DICOM UID:ar,
- hantera deterministisk datumoffset vid pseudonymisering,
- upptäcka misstänkt inbränd patientdata med OCR,
- låta användaren välja **GPU only**, **GPU/CPU auto** eller **CPU only** för OCR,
- stödja manuell maskning, pixelering eller blur av bildytor,
- bevara bildintegritet och transfer syntax när pixeldata inte ska ändras,
- aldrig tyst konvertera lossless till lossy,
- sätta felaktiga eller avvisade inkommande DICOM-objekt i quarantine,
- skapa manipulationsdetekterbara exportmanifest med SHA-256 och HMAC-SHA256.

### Viktiga principer

- **Fail closed** – stoppa hellre export än släpp igenom osäker data.
- **Rör inte PixelData om det inte behövs**.
- **Ingen tyst lossless → lossy-konvertering**.
- **Anonymisering och pseudonymisering är olika saker**.
- **OCR är ett stöd/hint-system, inte en garanti**.
- **Manifest ska vara tamper-evident**.
- **Quarantine ska finnas för missformade eller policy-avvisade objekt**.

### Status för v1.0.0

Detta paket är en **v1.0.0 source release / release candidate** av projektets arkitektur, GUI-skal, domänmodell och säkerhetskritiska designval.

Det är **inte färdigvaliderat för riktig patientdata** förrän DICOM-implementation, OCR-implementation, pixelpipeline och testning är genomförda och godkända i den miljö där verktyget ska användas.

---

## English

PHIght Club is a safety-first desktop tool for receiving, inspecting, de-identifying and exporting DICOM studies for AI training, validation, research preparation and vendor handoff.

### v1.0.0 scope in this release package

This repository release package contains:

1. Windows WPF shell with export wizard layout.
2. Domain models for jobs, profiles, OCR mode, image safety and manifests.
3. Deterministic date offset service for pseudonymization.
4. Manifest integrity service using SHA-256 and HMAC-SHA256.
5. Interfaces for DICOM SCP/SCU, OCR engines, pixel scrubbers and export targets.
6. Placeholder implementations that keep UI, DICOM, OCR and pixel logic separated.
7. Release notes, security hardening notes and draft DICOM conformance statement.
8. Swedish and English README content.

### Design principles

- Fail closed.
- Do not touch PixelData unless a pixel scrub step is explicitly enabled.
- Never silently convert lossless to lossy.
- Separate anonymization and pseudonymization.
- OCR is advisory, not a guarantee.
- Quarantine rejected or malformed inbound DICOM objects.
- Make export manifests tamper-evident.

### OCR acceleration modes

The UI and domain model support three explicit OCR modes:

```text
GPU only      - Use GPU backend only. Block if no compatible GPU backend is available.
GPU/CPU auto  - Try GPU first, fall back to CPU if needed.
CPU only      - Run OCR/pixel analysis without GPU initialization.
```

### Image safety

PHIght Club is designed around strict image integrity:

```text
Metadata-only export:
- PixelData should not be decoded.
- PixelData should not be modified.
- Original transfer syntax should be preserved.

Pixel-modifying export:
- PixelData may only be modified after explicit scrub/mask/pixelate action.
- Lossless input must not silently become lossy output.
- Unsafe re-encoding must block export in Strict mode.
```

### OCR warning

OCR detection is advisory. No OCR findings does **not** guarantee that an image is free from burned-in PHI. OCR must be combined with manual review, modality/vendor templates and export policy.

## Build

Install a supported .NET SDK on Windows, then run:

```powershell
./build.ps1
```

For a single-file Windows publish:

```powershell
./publish-win-x64.ps1
```

The published app will be placed under:

```text
artifacts/publish/win-x64/
```

## Repository layout

```text
src/
  PHIghtClub.App/                WPF desktop shell
  PHIghtClub.Core/               Domain models and shared contracts
  PHIghtClub.Dicom/              DICOM SCP/SCU interfaces and future fo-dicom implementation
  PHIghtClub.DeIdentification/   Pseudonymization and date offset logic
  PHIghtClub.Export/             Export contracts and manifest workflow
  PHIghtClub.Ocr/                OCR engine contracts and backend selection
  PHIghtClub.Pixel/              Pixel mask/pixelate contracts
  PHIghtClub.Storage/            Manifest integrity and vault abstractions
docs/
  release-v1.0.0.md
  security-hardening.md
  dicom-conformance-statement-draft.md
  validation-plan-v1.0.md
samples/
  profiles/
  templates/
```

## Production safety note

Do not use PHIght Club on real patient data until the DICOM SCP/SCU implementation, de-identification behavior, OCR behavior, pixel pipeline and manifest validation have been independently tested and approved for the intended environment.
