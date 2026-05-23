# PHIght Club v1.0 Release Checklist

## Before publishing source release

- [ ] README includes Swedish and English description.
- [ ] VERSION is set to 1.0.0.
- [ ] CHANGELOG is updated.
- [ ] Release notes are present.
- [ ] Validation plan is present.
- [ ] DICOM Conformance Statement draft is present.

## Before publishing binary release for real use

- [ ] Build on Windows.
- [ ] Unit tests pass.
- [ ] fo-dicom SCP/SCU implementation validated.
- [ ] Metadata-only de-id validated against synthetic data.
- [ ] PixelData preservation validated.
- [ ] OCR warning behavior validated.
- [ ] Quarantine behavior validated.
- [ ] Manifest HMAC verification validated.
- [ ] DICOM PS3.15 Annex E mapping reviewed.
- [ ] Risk review completed.
- [ ] No real patient data used before approval.
