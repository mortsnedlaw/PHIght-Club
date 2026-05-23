# Changelog

## 1.0.0 - Source release candidate

Initial safety-first source release package for PHIght Club.

### Added

- Windows WPF shell with export wizard layout.
- Explicit OCR acceleration selection: GPU only, GPU/CPU auto, CPU only.
- Domain models for DICOM input, de-identification, OCR, image safety and export settings.
- Deterministic HMAC-SHA256 date offset service for pseudonymization.
- Manifest model and manifest integrity service using SHA-256 + HMAC-SHA256.
- DICOM SCP/SCU contracts and placeholder services.
- OCR contracts and placeholder OCR backend selector.
- Pixel scrub contracts and no-op placeholder implementation.
- Quarantine/rejected inbound design notes.
- Swedish and English README.
- Draft DICOM Conformance Statement.
- v1.0 validation plan.

### Safety notes

- Real DICOM SCP/SCU, OCR inference and pixel re-encoding are intentionally still placeholders in this source package.
- Do not use on real patient data until implementation and validation are completed.
