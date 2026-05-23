# PHIght Club DICOM Conformance Statement - Draft for v1.0.0

## Product

PHIght Club

## Version

1.0.0 source release candidate

## Intended role

PHIght Club is designed to act as:

- Storage SCP for receiving DICOM instances into local staging.
- Verification SCU for C-ECHO destination tests.
- Storage SCU for exporting de-identified DICOM instances.

## Current implementation status

This v1.0.0 source release defines the DICOM contracts and UI behavior, but real fo-dicom SCP/SCU behavior is still pending implementation and validation.

Do not claim production DICOM conformance until these sections are completed and tested.

## Storage SCP - intended behavior

The final implementation should document:

- Configurable AE Title.
- Configurable port.
- Configurable bind address.
- Accepted SOP Classes.
- Accepted Transfer Syntaxes.
- Association policy.
- Maximum PDU.
- Rejection and quarantine behavior.
- Handling of malformed DICOM objects.

## Verification SCU - intended behavior

The final implementation should document:

- Destination AE Title.
- Destination host/IP.
- Destination port.
- Timeout behavior.
- Error handling.

## Storage SCU - intended behavior

The final implementation should document:

- Sent SOP Classes.
- Proposed Transfer Syntaxes.
- Retry behavior.
- Error handling.
- Behavior when export is blocked by image-safety policy.

## Limitations

- PHIght Club is not a PACS.
- PHIght Club does not provide Query/Retrieve in v1.0.0.
- PHIght Club does not provide DICOMweb in v1.0.0.
- OCR detection is advisory and does not guarantee absence of burned-in PHI.
- Non-pixel objects such as SR, KO, PR and PDF require policy handling and manual review until fully implemented.
