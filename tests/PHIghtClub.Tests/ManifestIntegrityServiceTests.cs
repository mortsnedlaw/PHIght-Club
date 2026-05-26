using PHIghtClub.Core;
using PHIghtClub.Storage;
using PHIghtClub.Core.Logging;
using Moq;
using Xunit;

namespace PHIghtClub.Tests;

/// <summary>
/// Tests for manifest integrity signing and verification.
/// </summary>
public class ManifestIntegrityServiceTests
{
    [Fact]
    public void Sign_ProducesConsistentSignature()
    {
        // Arrange
        var auditLogger = new Mock<IAuditLogger>();
        var service = new ManifestIntegrityService(auditLogger.Object);
        var manifest = CreateTestManifest();
        var objectHashes = new[] { "hash1", "hash2", "hash3" };
        var key = System.Text.Encoding.UTF8.GetBytes("test-key-32-bytes-minimum-test");

        // Act
        var integrity1 = service.Sign(manifest, objectHashes, key, "key-1");
        var integrity2 = service.Sign(manifest, objectHashes, key, "key-1");

        // Assert
        Assert.Equal(integrity1.Signature, integrity2.Signature);
        Assert.Equal(integrity1.ManifestSha256, integrity2.ManifestSha256);
        Assert.Equal(integrity1.ObjectListSha256, integrity2.ObjectListSha256);
    }

    [Fact]
    public void Sign_DifferentKeyProducesDifferentSignature()
    {
        // Arrange
        var auditLogger = new Mock<IAuditLogger>();
        var service = new ManifestIntegrityService(auditLogger.Object);
        var manifest = CreateTestManifest();
        var objectHashes = new[] { "hash1", "hash2" };
        var key1 = System.Text.Encoding.UTF8.GetBytes("key-1-32-bytes-minimum-test-key");
        var key2 = System.Text.Encoding.UTF8.GetBytes("key-2-32-bytes-minimum-test-key");

        // Act
        var integrity1 = service.Sign(manifest, objectHashes, key1, "key-1");
        var integrity2 = service.Sign(manifest, objectHashes, key2, "key-2");

        // Assert
        Assert.NotEqual(integrity1.Signature, integrity2.Signature);
    }

    [Fact]
    public void Verify_SucceedsForValidSignature()
    {
        // Arrange
        var auditLogger = new Mock<IAuditLogger>();
        var service = new ManifestIntegrityService(auditLogger.Object);
        var manifest = CreateTestManifest();
        var objectHashes = new[] { "hash1", "hash2", "hash3" };
        var key = System.Text.Encoding.UTF8.GetBytes("test-key-32-bytes-minimum-test");

        manifest.ManifestIntegrity = service.Sign(manifest, objectHashes, key, "key-1");

        // Act
        var verified = service.Verify(manifest, objectHashes, key);

        // Assert
        Assert.True(verified);
    }

    [Fact]
    public void Verify_FailsForTamperedManifest()
    {
        // Arrange
        var auditLogger = new Mock<IAuditLogger>();
        var service = new ManifestIntegrityService(auditLogger.Object);
        var manifest = CreateTestManifest();
        var objectHashes = new[] { "hash1", "hash2", "hash3" };
        var key = System.Text.Encoding.UTF8.GetBytes("test-key-32-bytes-minimum-test");

        manifest.ManifestIntegrity = service.Sign(manifest, objectHashes, key, "key-1");
        
        // Tamper with manifest - create a new one with different profile
        var tamperedManifest = new ExportManifest
        {
            JobId = manifest.JobId,
            Profile = "Tampered Profile",
            Status = manifest.Status
        };
        tamperedManifest.ManifestIntegrity = manifest.ManifestIntegrity;

        // Act
        var verified = service.Verify(tamperedManifest, objectHashes, key);

        // Assert
        Assert.False(verified);
    }

    [Fact]
    public void Verify_FailsForMissingIntegrity()
    {
        // Arrange
        var auditLogger = new Mock<IAuditLogger>();
        var service = new ManifestIntegrityService(auditLogger.Object);
        var manifest = CreateTestManifest();
        var objectHashes = new[] { "hash1", "hash2" };
        var key = System.Text.Encoding.UTF8.GetBytes("test-key-32-bytes-minimum-test");
        manifest.ManifestIntegrity = null;

        // Act
        var verified = service.Verify(manifest, objectHashes, key);

        // Assert
        Assert.False(verified);
    }

    [Fact]
    public void Verify_FailsForWrongKey()
    {
        // Arrange
        var auditLogger = new Mock<IAuditLogger>();
        var service = new ManifestIntegrityService(auditLogger.Object);
        var manifest = CreateTestManifest();
        var objectHashes = new[] { "hash1", "hash2" };
        var key1 = System.Text.Encoding.UTF8.GetBytes("key-1-32-bytes-minimum-test-key");
        var key2 = System.Text.Encoding.UTF8.GetBytes("key-2-32-bytes-minimum-test-key");

        manifest.ManifestIntegrity = service.Sign(manifest, objectHashes, key1, "key-1");

        // Act
        var verified = service.Verify(manifest, objectHashes, key2);

        // Assert
        Assert.False(verified);
    }

    [Fact]
    public void Sign_LogsAuditEvent()
    {
        // Arrange
        var auditLogger = new Mock<IAuditLogger>();
        var service = new ManifestIntegrityService(auditLogger.Object);
        var manifest = CreateTestManifest();
        var objectHashes = new[] { "hash1", "hash2" };
        var key = System.Text.Encoding.UTF8.GetBytes("test-key-32-bytes-minimum-test");

        // Act
        service.Sign(manifest, objectHashes, key, "test-key");

        // Assert
        auditLogger.Verify(
            x => x.LogManifestSigned(
                manifest.JobId,
                "test-key",
                It.IsAny<string>(),
                objectHashes.Length,
                It.IsAny<DateTime>()),
            Times.Once);
    }

    private static ExportManifest CreateTestManifest()
    {
        return new ExportManifest
        {
            JobId = Guid.NewGuid().ToString(),
            Profile = "Test Profile",
            Status = "DryRun"
        };
    }
}
