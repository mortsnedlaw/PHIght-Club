using PHIghtClub.Core;
using PHIghtClub.DeIdentification;
using Xunit;

namespace PHIghtClub.Tests;

public class DateOffsetTests
{
    [Fact]
    public void SamePatientSameVaultGetsSameOffset()
    {
        var service = new DeterministicDateOffsetService();
        var secret = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var policy = DateOffsetPolicy.Default();

        var a = service.GetOffsetDays("PSEUDO-000001", secret, policy);
        var b = service.GetOffsetDays("PSEUDO-000001", secret, policy);

        Assert.Equal(a, b);
    }
}
