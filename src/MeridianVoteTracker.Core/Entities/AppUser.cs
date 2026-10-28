using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Entities;

public class AppUser
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string DisplayName { get; set; }
    public UserRole Role { get; set; }
}
