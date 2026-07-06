using System;

namespace PHIghtClub.Dicom;

/// <summary>
/// Tracks what was changed during de-identification of a DICOM object.
/// </summary>
public sealed class DeIdentificationChangeLog
{
    public bool PatientIdChanged { get; set; }
    public bool PatientNameChanged { get; set; }
    public bool StudyUidChanged { get; set; }
    public bool SeriesUidChanged { get; set; }
    public bool SopUidChanged { get; set; }
    public int DateOffsetDays { get; set; }
    public bool PixelDataModified { get; set; }
    public string OriginalStudyInstanceUid { get; set; } = string.Empty;
    public string OriginalSeriesInstanceUid { get; set; } = string.Empty;
    public string OriginalSopInstanceUid { get; set; } = string.Empty;
    public string RemappedStudyInstanceUid { get; set; } = string.Empty;
    public string RemappedSeriesInstanceUid { get; set; } = string.Empty;
    public string RemappedSopInstanceUid { get; set; } = string.Empty;
    public string OriginalTransferSyntax { get; set; } = string.Empty;
    public string OutputTransferSyntax { get; set; } = string.Empty;
}
