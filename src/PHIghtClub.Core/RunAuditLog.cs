using System;

namespace PHIghtClub.Core;

/// <summary>
/// Represents a single object processed during an export/validation run.
/// </summary>
public sealed class RunAuditLogEntry
{
    public DateTime Timestamp { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // import, validate, de-id, uid-remap, date-offset, pixel-mask, quarantine, export, etc.
    public string Status { get; set; } = string.Empty; // OK, skipped, blocked, quarantined, failed
    public string SourcePath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string QuarantinePath { get; set; } = string.Empty;
    public bool PatientIdChanged { get; set; }
    public bool PatientNameChanged { get; set; }
    public bool StudyUidChanged { get; set; }
    public bool SeriesUidChanged { get; set; }
    public bool SopUidChanged { get; set; }
    public int DateOffsetDays { get; set; }
    public bool PixelDataModified { get; set; }
    public string PixelAction { get; set; } = string.Empty; // None, BlackMask, Pixelate, Blur
    public int MaskRegionCount { get; set; }
    public string TransferSyntaxOriginal { get; set; } = string.Empty;
    public string TransferSyntaxOutput { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Writes audit log entries to a CSV file.
/// </summary>
public sealed class RunAuditLogger
{
    private readonly string _auditLogPath;
    private readonly object _lock = new object();
    private bool _headerWritten;

    public RunAuditLogger(string outputFolder, string jobId)
    {
        Directory.CreateDirectory(outputFolder);
        _auditLogPath = Path.Combine(outputFolder, $"{jobId}.audit.csv");
    }

    public string AuditLogPath => _auditLogPath;

    public void LogEntry(RunAuditLogEntry entry)
    {
        lock (_lock)
        {
            try
            {
                if (!_headerWritten)
                {
                    WriteHeader();
                    _headerWritten = true;
                }

                var line = EscapeCsvLine(new[]
                {
                    entry.Timestamp.ToString("O"),
                    entry.JobId,
                    entry.Action,
                    entry.Status,
                    entry.SourcePath,
                    entry.OutputPath,
                    entry.QuarantinePath,
                    entry.PatientIdChanged.ToString(),
                    entry.PatientNameChanged.ToString(),
                    entry.StudyUidChanged.ToString(),
                    entry.SeriesUidChanged.ToString(),
                    entry.SopUidChanged.ToString(),
                    entry.DateOffsetDays.ToString(),
                    entry.PixelDataModified.ToString(),
                    entry.PixelAction,
                    entry.MaskRegionCount.ToString(),
                    entry.TransferSyntaxOriginal,
                    entry.TransferSyntaxOutput,
                    entry.ErrorMessage
                });

                File.AppendAllText(_auditLogPath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // Log to console as fallback if audit logging fails
                Console.Error.WriteLine($"Failed to write audit log: {ex.Message}");
                throw;
            }
        }
    }

    private void WriteHeader()
    {
        var header = EscapeCsvLine(new[]
        {
            "Timestamp",
            "JobId",
            "Action",
            "Status",
            "SourcePath",
            "OutputPath",
            "QuarantinePath",
            "PatientIdChanged",
            "PatientNameChanged",
            "StudyUidChanged",
            "SeriesUidChanged",
            "SopUidChanged",
            "DateOffsetDays",
            "PixelDataModified",
            "PixelAction",
            "MaskRegionCount",
            "TransferSyntaxOriginal",
            "TransferSyntaxOutput",
            "ErrorMessage"
        });

        File.WriteAllText(_auditLogPath, header + Environment.NewLine);
    }

    private static string EscapeCsvLine(string[] fields)
    {
        return string.Join(",", fields.Select(f =>
        {
            if (string.IsNullOrEmpty(f))
                return string.Empty;

            // Escape quotes and wrap in quotes if necessary
            if (f.Contains(",") || f.Contains("\"") || f.Contains("\n"))
            {
                return "\"" + f.Replace("\"", "\"\"") + "\"";
            }

            return f;
        }));
    }
}
