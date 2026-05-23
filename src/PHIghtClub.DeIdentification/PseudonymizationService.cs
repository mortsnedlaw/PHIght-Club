using System.Security.Cryptography;
using System.Text;

namespace PHIghtClub.DeIdentification;

public interface IPseudonymizationService
{
    string CreatePseudoPatientId(string originalPatientId, byte[] vaultSecret);
}

public sealed class HmacPseudonymizationService : IPseudonymizationService
{
    public string CreatePseudoPatientId(string originalPatientId, byte[] vaultSecret)
    {
        if (string.IsNullOrWhiteSpace(originalPatientId))
        {
            throw new ArgumentException("Original PatientID is required.", nameof(originalPatientId));
        }

        using var hmac = new HMACSHA256(vaultSecret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(originalPatientId.Trim()));
        return "PSEUDO-" + Convert.ToHexString(hash)[..16];
    }
}
