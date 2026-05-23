# Security and Safety Hardening Notes

## Swedish summary

PHIght Club ska behandlas som ett säkerhetskritiskt exportverktyg. Standardbeteendet ska vara konservativt: stoppa hellre export än riskera att patientdata eller förstörd bilddata lämnar verktyget.

## Date offset

Date offset must be deterministic for pseudonymized datasets when longitudinal analysis matters.

Proposed algorithm:

```text
offsetDays = HMAC_SHA256(vaultSecret, stablePseudoSubjectId) mapped to allowed range
```

## Manifest integrity

Each manifest should include:

- canonical JSON hash
- object list hash
- HMAC-SHA256 signature
- key id, but never the key material

## OCR warning

OCR is advisory.

A clean OCR result does not guarantee that no burned-in PHI exists.

## Non-pixel objects

SR, KO, PR, PDF and other text-bearing objects should default to manual review or block until specific de-identification handling exists.

## Private tags

Default whitelist should be empty or extremely conservative.

## Image integrity

Metadata-only export must not decode, modify or re-encode PixelData.

Pixel-modifying export must never silently introduce lossy compression.
