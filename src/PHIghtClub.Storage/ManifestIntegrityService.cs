using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PHIghtClub.Core;
using PHIghtClub.Core.Logging;

namespace PHIghtClub.Storage;

public interface IManifestIntegrityService
{
    ManifestIntegrity Sign(ExportManifest manifestWithoutIntegrity, IReadOnlyList<string> exportedObjectHashes, byte[] hmacKey, string keyId);
    bool Verify(ExportManifest manifest, IReadOnlyList<string> exportedObjectHashes, byte[] hmacKey);
}

public sealed class ManifestIntegrityService : IManifestIntegrityService
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = null
    };

    private readonly IAuditLogger _auditLogger;

    public ManifestIntegrityService(IAuditLogger? auditLogger = null)
    {
        _auditLogger = auditLogger ?? new NoOpAuditLogger();
    }

    public ManifestIntegrity Sign(ExportManifest manifestWithoutIntegrity, IReadOnlyList<string> exportedObjectHashes, byte[] hmacKey, string keyId)
    {
        var clone = CloneWithoutIntegrity(manifestWithoutIntegrity);
        var canonicalJson = JsonSerializer.Serialize(clone, CanonicalOptions);
        var manifestSha = Sha256Hex(Encoding.UTF8.GetBytes(canonicalJson));
        var objectListSha = Sha256Hex(Encoding.UTF8.GetBytes(string.Join("\n", exportedObjectHashes.OrderBy(x => x, StringComparer.Ordinal))));
        var signature = HmacHex(hmacKey, manifestSha + ":" + objectListSha);

        var integrity = new ManifestIntegrity
        {
            ManifestSha256 = manifestSha,
            ObjectListSha256 = objectListSha,
            HmacKeyId = keyId,
            Signature = signature
        };

        _auditLogger.LogManifestSigned(
            clone.JobId,
            keyId,
            signature,
            exportedObjectHashes.Count,
            DateTime.UtcNow);

        return integrity;
    }

    public bool Verify(ExportManifest manifest, IReadOnlyList<string> exportedObjectHashes, byte[] hmacKey)
    {
        if (manifest.ManifestIntegrity is null)
        {
            _auditLogger.LogManifestVerificationAttempt(manifest.JobId, false, "No integrity data present", DateTime.UtcNow);
            return false;
        }

        var expected = Sign(manifest, exportedObjectHashes, hmacKey, manifest.ManifestIntegrity.HmacKeyId);
        var verified = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected.Signature),
            Encoding.UTF8.GetBytes(manifest.ManifestIntegrity.Signature));

        var reason = verified ? null : "Signature mismatch";
        _auditLogger.LogManifestVerificationAttempt(manifest.JobId, verified, reason, DateTime.UtcNow);

        return verified;
    }

    private static ExportManifest CloneWithoutIntegrity(ExportManifest manifest)
    {
        var json = JsonSerializer.Serialize(manifest, CanonicalOptions);
        var clone = JsonSerializer.Deserialize<ExportManifest>(json, CanonicalOptions)
            ?? throw new InvalidOperationException("Failed to clone manifest.");
        clone.ManifestIntegrity = null;
        return clone;
    }

    private static string Sha256Hex(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string HmacHex(byte[] key, string value)
    {
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
}
