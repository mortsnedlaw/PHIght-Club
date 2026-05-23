using System.Security.Cryptography;
using System.Text;
using PHIghtClub.Core;

namespace PHIghtClub.DeIdentification;

public interface IDateOffsetService
{
    int GetOffsetDays(string stablePseudoSubjectId, byte[] vaultSecret, DateOffsetPolicy policy);
    DateOnly Offset(DateOnly originalDate, string stablePseudoSubjectId, byte[] vaultSecret, DateOffsetPolicy policy);
}

public sealed class DeterministicDateOffsetService : IDateOffsetService
{
    public int GetOffsetDays(string stablePseudoSubjectId, byte[] vaultSecret, DateOffsetPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(stablePseudoSubjectId))
        {
            throw new ArgumentException("Stable pseudo subject id is required.", nameof(stablePseudoSubjectId));
        }

        if (vaultSecret.Length < 32)
        {
            throw new ArgumentException("Vault secret should be at least 256 bits.", nameof(vaultSecret));
        }

        if (policy.MinDays > policy.MaxDays)
        {
            throw new ArgumentException("Invalid date offset range.", nameof(policy));
        }

        using var hmac = new HMACSHA256(vaultSecret);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stablePseudoSubjectId));
        var unsigned = BitConverter.ToUInt32(hash, 0);
        var range = policy.MaxDays - policy.MinDays + 1;
        return policy.MinDays + (int)(unsigned % range);
    }

    public DateOnly Offset(DateOnly originalDate, string stablePseudoSubjectId, byte[] vaultSecret, DateOffsetPolicy policy)
    {
        var days = GetOffsetDays(stablePseudoSubjectId, vaultSecret, policy);
        return originalDate.AddDays(days);
    }
}
