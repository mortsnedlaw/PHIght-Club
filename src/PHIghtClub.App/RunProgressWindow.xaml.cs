using System;
using System.Windows;
using System.Collections.Generic;

namespace PHIghtClub.App;

public partial class RunProgressWindow : Window
{
    private int _processedCount;
    private int _totalCount;
    private int _failedCount;
    private int _quarantinedCount;
    private int _exportedCount;
    private readonly Queue<string> _statusLog = new();
    private const int MaxLogLines = 100;

    public RunProgressWindow()
    {
        InitializeComponent();
    }

    public void SetJobId(string jobId)
    {
        JobIdText.Text = jobId;
    }

    public void SetTotalCount(int count)
    {
        _totalCount = count;
        UpdateCounters();
    }

    public void UpdateProgress(string currentAction, string currentFile)
    {
        CurrentActionText.Text = currentAction;
        CurrentFileText.Text = currentFile;
    }

    public void LogStatus(string message, RunProgressStatus status = RunProgressStatus.Info)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var statusPrefix = status switch
        {
            RunProgressStatus.Success => "[✓]",
            RunProgressStatus.Failed => "[✗]",
            RunProgressStatus.Warning => "[!]",
            RunProgressStatus.Quarantined => "[Q]",
            _ => "[i]"
        };

        var logLine = $"{timestamp} {statusPrefix} {message}";
        _statusLog.Enqueue(logLine);

        if (_statusLog.Count > MaxLogLines)
        {
            _statusLog.Dequeue();
        }

        StatusLogText.Text = string.Join(Environment.NewLine, _statusLog);
        StatusLogScrollViewer?.ScrollToEnd();
    }

    public void IncrementProcessed()
    {
        _processedCount++;
        UpdateCounters();
    }

    public void IncrementFailed()
    {
        _failedCount++;
        UpdateCounters();
    }

    public void IncrementQuarantined()
    {
        _quarantinedCount++;
        UpdateCounters();
    }

    public void IncrementExported()
    {
        _exportedCount++;
        UpdateCounters();
    }

    private void UpdateCounters()
    {
        ProcessedCountText.Text = _processedCount.ToString();
        TotalCountText.Text = _totalCount.ToString();
        FailedCountText.Text = _failedCount.ToString();
        QuarantinedCountText.Text = _quarantinedCount.ToString();
        ExportedCountText.Text = _exportedCount.ToString();

        if (_totalCount > 0)
        {
            ProgressBar.Value = (_processedCount / (double)_totalCount) * 100;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public enum RunProgressStatus
{
    Info,
    Success,
    Failed,
    Warning,
    Quarantined
}
