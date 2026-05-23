# PHIght Club v1.0.0 Release Notes

## Release type

Source release candidate / architecture baseline.

## Summary

PHIght Club v1.0.0 establishes the first complete project structure for a Windows-native DICOM de-identification and export tool. The release focuses on safe architecture before feature depth: fail-closed behavior, image integrity, explicit OCR acceleration choice, deterministic pseudonymization support and tamper-evident manifests.

## Swedish summary

PHIght Club v1.0.0 är en första releasekandidat av källkod, appstruktur och säkerhetsdesign. Syftet är att skapa en stabil grund innan verklig DICOM-, OCR- och pixelpipeline kopplas in.

## Included

- WPF desktop app shell.
- Export wizard UI.
- DICOM SCP/SCU interface layer.
- De-identification and pseudonymization domain model.
- Deterministic date offset algorithm for pseudonymization.
- Manifest integrity with SHA-256 and HMAC-SHA256.
- OCR mode and acceleration mode selection.
- Pixel scrub interface.
- Strict image safety policy.
- Quarantine design for malformed or rejected inbound DICOM.
- Swedish and English README.

## Not yet production-ready

The following are intentionally still placeholders and must be implemented and validated before clinical or research use on real patient data:

- Real fo-dicom Storage SCP.
- Real fo-dicom C-ECHO/C-STORE SCU.
- Real DICOM metadata de-identification rules mapped to DICOM PS3.15 Annex E.
- Real OCR inference backend.
- Real pixel decoding, mask/pixelate/blur and safe re-encoding.
- Full validation against representative DICOM datasets.

## Release checklist

Before tagging this as a production-ready binary release:

- [ ] Build on Windows with supported .NET SDK.
- [ ] Run unit tests.
- [ ] Validate DICOM folder import on synthetic test data.
- [ ] Validate Storage SCP with synthetic DICOM sender.
- [ ] Validate C-ECHO and C-STORE against a test receiver.
- [ ] Validate metadata-only export preserves PixelData and transfer syntax.
- [ ] Validate blocked export when unsafe pixel re-encoding would be required.
- [ ] Validate OCR warning language in UI and manifest.
- [ ] Validate manifest HMAC verification.
- [ ] Review DICOM PS3.15 Annex E mapping.
- [ ] Review DICOM Conformance Statement.
