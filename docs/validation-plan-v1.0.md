# PHIght Club v1.0 Validation Plan

## Goal

Validate that PHIght Club behaves safely before use on any real patient data.

## Test data

Use synthetic DICOM datasets only until de-identification and image safety behavior has been independently reviewed.

Suggested dataset categories:

- CT single-frame uncompressed.
- CT compressed lossless.
- MR single-frame.
- Ultrasound single-frame.
- Ultrasound multiframe.
- Secondary Capture.
- Encapsulated PDF.
- Structured Report.
- Malformed DICOM object.

## Required validation areas

### Metadata-only export

Expected behavior:

- PixelData is not decoded.
- PixelData is not modified.
- Transfer Syntax UID is preserved.
- Patient identifiers are removed or remapped according to profile.
- UID remapping is applied according to profile.
- Manifest is created and signed.

### Pseudonymization

Expected behavior:

- Same original patient in same vault receives same pseudo ID.
- Same pseudo identity receives deterministic date offset.
- Date offset algorithm and range are written to manifest.

### OCR and pixel scrub

Expected behavior:

- OCR is shown as advisory.
- GPU only blocks when no GPU backend is available.
- GPU/CPU auto falls back to CPU when GPU backend is unavailable.
- CPU only does not initialize GPU.
- Manual mask/pixelate requires explicit action.

### Image safety

Expected behavior:

- Lossless input must not silently become lossy output.
- Unsafe re-encoding blocks export in Strict mode.
- PixelData is only modified when pixel scrub is explicitly enabled.

### Quarantine

Expected behavior:

- Malformed inbound DICOM is not silently ignored.
- Rejected object receives status and reason.
- Calling AE, remote IP and error reason are logged when available.

### Manifest integrity

Expected behavior:

- Manifest contains canonical hash.
- Manifest contains object list hash.
- Manifest contains HMAC signature and key id.
- Modified manifest fails verification.
