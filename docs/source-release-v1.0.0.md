# PHIght Club v1.0.0 Source Release Scope

## Goal

A safe first PHIght Club source release that opens as a Windows app, exposes the final export wizard shape, defines the domain model and provides the safety-critical contracts before real DICOM/OCR/pixel implementations are connected.

## Includes

- WPF application shell.
- Wizard-style UI: Input, De-ID, OCR/Pixel, Image Safety, Export.
- Domain model for ExportJob.
- AI Training Strict default profile sample.
- Deterministic date offset service.
- Manifest integrity service.
- JSON manifest model.
- Dry-run action that writes a signed representative manifest.

## Does not include production behavior yet

- Real DICOM parsing.
- Real Storage SCP listener.
- Real C-STORE export.
- Pixel decoding/re-encoding.
- OCR inference.

## Next implementation milestone

Implement real folder import using fo-dicom and metadata-only de-identification while preserving original transfer syntax and PixelData.
