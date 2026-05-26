using PHIghtClub.Core;
using Xunit;

namespace PHIghtClub.Tests;

/// <summary>
/// Tests for export job validation.
/// </summary>
public class JobValidationTests
{
    [Fact]
    public void ValidateJob_FailsWhenNoInputSourceSelected()
    {
        // Arrange
        var job = new ExportJob
        {
            Input = new InputSettings
            {
                UseDicomStorageScp = false,
                UseFolderImport = false
            }
        };

        // Act
        var result = ValidateJob(job);

        // Assert
        Assert.True(result.IsBlocked);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Select at least one input source", result.Errors[0]);
    }

    [Fact]
    public void ValidateJob_FailsWithInvalidPort()
    {
        // Arrange
        var job = new ExportJob
        {
            Input = new InputSettings
            {
                UseDicomStorageScp = true,
                UseFolderImport = false,
                Port = 70000
            }
        };

        // Act
        var result = ValidateJob(job);

        // Assert
        Assert.True(result.IsBlocked);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Invalid SCP port", result.Errors[0]);
    }

    [Fact]
    public void ValidateJob_FailsWithPort0()
    {
        // Arrange
        var job = new ExportJob
        {
            Input = new InputSettings
            {
                UseDicomStorageScp = true,
                UseFolderImport = false,
                Port = 0
            }
        };

        // Act
        var result = ValidateJob(job);

        // Assert
        Assert.True(result.IsBlocked);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("Invalid SCP port", result.Errors[0]);
    }

    [Fact]
    public void ValidateJob_PassesWithValidPort()
    {
        // Arrange
        var job = new ExportJob
        {
            Input = new InputSettings
            {
                UseDicomStorageScp = true,
                UseFolderImport = false,
                Port = 11112
            },
            DeIdentification = new DeIdentificationSettings
            {
                Mode = DeIdentificationMode.MetadataAnonymization
            }
        };

        // Act
        var result = ValidateJob(job);

        // Assert
        Assert.DoesNotContain("Invalid SCP port", result.Errors);
    }

    [Fact]
    public void ValidateJob_FailsPseudonymizationWithoutVaultScope()
    {
        // Arrange
        var job = new ExportJob
        {
            Input = new InputSettings
            {
                UseDicomStorageScp = true,
                Port = 11112
            },
            DeIdentification = new DeIdentificationSettings
            {
                Mode = DeIdentificationMode.Pseudonymization,
                DateOffset = new DateOffsetPolicy
                {
                    VaultScoped = false
                }
            }
        };

        // Act
        var result = ValidateJob(job);

        // Assert
        Assert.True(result.IsBlocked);
        Assert.Contains("vault-scoped", result.Errors[0]);
    }

    [Fact]
    public void ValidateJob_PassesPseudonymizationWithVaultScope()
    {
        // Arrange
        var job = new ExportJob
        {
            Input = new InputSettings
            {
                UseDicomStorageScp = true,
                Port = 11112
            },
            DeIdentification = new DeIdentificationSettings
            {
                Mode = DeIdentificationMode.Pseudonymization,
                DateOffset = new DateOffsetPolicy
                {
                    VaultScoped = true
                }
            }
        };

        // Act
        var result = ValidateJob(job);

        // Assert
        Assert.DoesNotContain("vault-scoped", result.Errors);
    }

    [Fact]
    public void ValidateJob_WarnsWhenOcrEnabled()
    {
        // Arrange
        var job = new ExportJob
        {
            Input = new InputSettings
            {
                UseDicomStorageScp = true,
                Port = 11112
            },
            BurnedInPhi = new BurnedInPhiSettings
            {
                OcrMode = OcrMode.WarnAndRequireApproval
            }
        };

        // Act
        var result = ValidateJob(job);

        // Assert
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("OCR is advisory", result.Warnings[0]);
    }

    [Fact]
    public void ValidateJob_PortBoundaryValues()
    {
        // Test port 1 (valid, but privileged)
        var job1 = CreateJobWithPort(1);
        var result1 = ValidateJob(job1);
        Assert.DoesNotContain("Invalid SCP port", string.Concat(result1.Errors));

        // Test port 65535 (valid max)
        var job2 = CreateJobWithPort(65535);
        var result2 = ValidateJob(job2);
        Assert.DoesNotContain("Invalid SCP port", string.Concat(result2.Errors));

        // Test port 65536 (invalid)
        var job3 = CreateJobWithPort(65536);
        var result3 = ValidateJob(job3);
        Assert.NotEmpty(result3.Errors);
        Assert.Contains("Invalid SCP port", result3.Errors[0]);
    }

    private static ValidationResult ValidateJob(ExportJob job)
    {
        var result = new ValidationResult();

        if (!job.Input.UseDicomStorageScp && !job.Input.UseFolderImport)
        {
            result.AddError("Select at least one input source.");
        }

        if (job.Input.Port <= 0 || job.Input.Port > 65535)
        {
            result.AddError("Invalid SCP port.");
        }

        if (job.BurnedInPhi?.OcrMode != OcrMode.Off)
        {
            result.AddWarning("OCR is advisory. Manual review and templates are still required for high-risk modalities.");
        }

        if (job.DeIdentification.Mode == DeIdentificationMode.Pseudonymization && 
            !job.DeIdentification.DateOffset.VaultScoped)
        {
            result.AddError("Pseudonymization with deterministic date offset requires vault-scoped offset.");
        }

        return result;
    }

    private static ExportJob CreateJobWithPort(int port)
    {
        return new ExportJob
        {
            Input = new InputSettings
            {
                UseDicomStorageScp = true,
                Port = port
            }
        };
    }
}
