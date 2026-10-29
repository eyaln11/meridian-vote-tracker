namespace MeridianVoteTracker.Infrastructure.Imports;

/// <summary>Where uploaded files are kept. Set with the Storage:UploadDirectory setting (a volume in Docker).</summary>
public sealed class ImportStorageOptions
{
    public string UploadDirectory { get; set; } = "uploads";
}
