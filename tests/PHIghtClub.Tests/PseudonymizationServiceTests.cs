using PHIghtClub.DeIdentification;
using Xunit;

namespace PHIghtClub.Tests;

/// <summary>
/// Tests for HMAC-based pseudonymization service.
/// </summary>
public class PseudonymizationServiceTests
{
    [Fact]
    public void CreatePseudoPatientId_ReturnsDeterministicId()
    {
        // Arrange
        var service = new HmacPseudonymizationService();
        var patientId = "P123456";
        var secret = System.Text.Encoding.UTF8.GetBytes("vault-secret-32-bytes-minimum-test");

        // Act
        var pseudo1 = service.CreatePseudoPatientId(patientId, secret);
        var pseudo2 = service.CreatePseudoPatientId(patientId, secret);

        // Assert
        Assert.Equal(pseudo1, pseudo2);
    }

    [Fact]
    public void CreatePseudoPatientId_DifferentPatientsProduceDifferentIds()
    {
        // Arrange
        var service = new HmacPseudonymizationService();
        var patient1 = "P111111";
        var patient2 = "P222222";
        var secret = System.Text.Encoding.UTF8.GetBytes("vault-secret-32-bytes-minimum-test");

        // Act
        var pseudo1 = service.CreatePseudoPatientId(patient1, secret);
        var pseudo2 = service.CreatePseudoPatientId(patient2, secret);

        // Assert
        Assert.NotEqual(pseudo1, pseudo2);
    }

    [Fact]
    public void CreatePseudoPatientId_DifferentSecretsProduceDifferentIds()
    {
        // Arrange
        var service = new HmacPseudonymizationService();
        var patientId = "P123456";
        var secret1 = System.Text.Encoding.UTF8.GetBytes("secret-1-32-bytes-minimum-test-");
        var secret2 = System.Text.Encoding.UTF8.GetBytes("secret-2-32-bytes-minimum-test-");

        // Act
        var pseudo1 = service.CreatePseudoPatientId(patientId, secret1);
        var pseudo2 = service.CreatePseudoPatientId(patientId, secret2);

        // Assert
        Assert.NotEqual(pseudo1, pseudo2);
    }

    [Fact]
    public void CreatePseudoPatientId_TrimmsWhitespace()
    {
        // Arrange
        var service = new HmacPseudonymizationService();
        var patient1 = "P123456";
        var patient2 = "  P123456  ";
        var secret = System.Text.Encoding.UTF8.GetBytes("vault-secret-32-bytes-minimum-test");

        // Act
        var pseudo1 = service.CreatePseudoPatientId(patient1, secret);
        var pseudo2 = service.CreatePseudoPatientId(patient2, secret);

        // Assert
        Assert.Equal(pseudo1, pseudo2);
    }

    [Fact]
    public void CreatePseudoPatientId_ThrowsOnNullId()
    {
        // Arrange
        var service = new HmacPseudonymizationService();
        var secret = System.Text.Encoding.UTF8.GetBytes("vault-secret-32-bytes-minimum-test");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => service.CreatePseudoPatientId(null!, secret));
        Assert.Contains("Original PatientID is required", ex.Message);
    }

    [Fact]
    public void CreatePseudoPatientId_ThrowsOnEmptyId()
    {
        // Arrange
        var service = new HmacPseudonymizationService();
        var secret = System.Text.Encoding.UTF8.GetBytes("vault-secret-32-bytes-minimum-test");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => service.CreatePseudoPatientId(string.Empty, secret));
        Assert.Contains("Original PatientID is required", ex.Message);
    }

    [Fact]
    public void CreatePseudoPatientId_ThrowsOnWhitespaceOnlyId()
    {
        // Arrange
        var service = new HmacPseudonymizationService();
        var secret = System.Text.Encoding.UTF8.GetBytes("vault-secret-32-bytes-minimum-test");

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(
            () => service.CreatePseudoPatientId("   ", secret));
        Assert.Contains("Original PatientID is required", ex.Message);
    }

    [Fact]
    public void CreatePseudoPatientId_StartsWithPrefix()
    {
        // Arrange
        var service = new HmacPseudonymizationService();
        var patientId = "P123456";
        var secret = System.Text.Encoding.UTF8.GetBytes("vault-secret-32-bytes-minimum-test");

        // Act
        var pseudo = service.CreatePseudoPatientId(patientId, secret);

        // Assert
        Assert.StartsWith("PSEUDO-", pseudo);
    }

    [Fact]
    public void CreatePseudoPatientId_ProducesValidLength()
    {
        // Arrange
        var service = new HmacPseudonymizationService();
        var patientId = "P123456";
        var secret = System.Text.Encoding.UTF8.GetBytes("vault-secret-32-bytes-minimum-test");

        // Act
        var pseudo = service.CreatePseudoPatientId(patientId, secret);

        // Assert
        // "PSEUDO-" (7 chars) + 16 hex chars = 23 chars total
        Assert.Equal(23, pseudo.Length);
    }
}
