namespace MeridianVoteTracker.Core.Entities;

public class MemberState
{
    public int Id { get; set; }
    public required string CountryCode { get; set; }
    public required string CountryName { get; set; }
    public DateOnly MemberFrom { get; set; }
    public DateOnly? MemberTo { get; set; }

    public bool IsMemberOn(DateOnly date) =>
        MemberFrom <= date && (MemberTo is null || date <= MemberTo);
}