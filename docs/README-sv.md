# PHIght Club – svensk översikt

PHIght Club är en Windows-native exportstation för DICOM-data.

Målet är att kunna ta emot, granska, avidentifiera, pseudonymisera och exportera DICOM-studier till exempelvis AI-träning, validering, leverantörsöverlämning eller forskningsyta.

## Grundidé

```text
Inkommande DICOM
  ↓
Staging / quarantine
  ↓
Metadata-anonymisering eller pseudonymisering
  ↓
OCR-varning för inbränd PHI
  ↓
Manuell maskning/pixelering vid behov
  ↓
Bildintegritetskontroll
  ↓
Export + manifest
```

## Viktigaste säkerhetsprinciperna

- Stoppa hellre export än exportera osäkert.
- Rör inte PixelData om det inte behövs.
- Konvertera aldrig lossless till lossy i tysthet.
- Särhåll anonymisering och pseudonymisering.
- OCR är endast rådgivande.
- Skapa manifest som går att kontrollera i efterhand.

## Status

v1.0.0 är en releasekandidat för källkod och arkitektur. Verklig användning på patientdata kräver fortsatt implementation, testning och godkännande.
