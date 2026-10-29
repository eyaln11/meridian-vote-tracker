namespace MeridianVoteTracker.Core.Entities;

/// <summary>One membership period of a country. A country that left and came back has two rows.</summary>
public class MemberState
{
    public int Id { get; set; }
    public required string CountryCode { get; set; }
    public DateOnly MemberFrom { get; set; }
    public DateOnly? MemberTo { get; set; }

    public bool IsMemberOn(DateOnly date) =>
        MemberFrom <= date && (MemberTo is null || date <= MemberTo);
}
