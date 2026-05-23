namespace PHIghtClub.Core;

public sealed class ValidationResult
{
    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];

    public IReadOnlyList<string> Warnings => _warnings;
    public IReadOnlyList<string> Errors => _errors;
    public bool IsBlocked => _errors.Count > 0;

    public void AddWarning(string message) => _warnings.Add(message);
    public void AddError(string message) => _errors.Add(message);
}
