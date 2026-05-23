namespace PHIghtClub.Storage;

public interface IMappingVault
{
    string VaultId { get; }
    byte[] GetOrCreateVaultSecret();
    Task<string> GetOrCreatePseudoPatientIdAsync(string originalPatientId, CancellationToken cancellationToken);
}

public sealed class InMemoryMappingVault : IMappingVault
{
    private readonly Dictionary<string, string> _patientMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly byte[] _secret = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);

    public string VaultId { get; } = "in-memory-dev-vault";

    public byte[] GetOrCreateVaultSecret() => _secret;

    public Task<string> GetOrCreatePseudoPatientIdAsync(string originalPatientId, CancellationToken cancellationToken)
    {
        if (!_patientMap.TryGetValue(originalPatientId, out var pseudo))
        {
            pseudo = "PSEUDO-" + (_patientMap.Count + 1).ToString("D8");
            _patientMap[originalPatientId] = pseudo;
        }

        return Task.FromResult(pseudo);
    }
}
