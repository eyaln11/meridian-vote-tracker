namespace MeridianVoteTracker.Core.Domain;

public enum Ballot
{
    Yes,
    No,
    Abstain,
    Absent
}

public static class BallotCodes
{
    public static bool TryParse(string? code, out Ballot ballot)
    {
        switch (code)
        {
            case "Y": ballot = Ballot.Yes; return true;
            case "N": ballot = Ballot.No; return true;
            case "A": ballot = Ballot.Abstain; return true;
            case "X": ballot = Ballot.Absent; return true;
            default: ballot = default; return false;
        }
    }

    public static Ballot Parse(string code) =>
        TryParse(code, out var ballot) ? ballot : throw new FormatException($"'{code}' is not a ballot code.");

    public static string ToCode(this Ballot ballot) => ballot switch
    {
        Ballot.Yes => "Y",
        Ballot.No => "N",
        Ballot.Abstain => "A",
        Ballot.Absent => "X",
        _ => throw new ArgumentOutOfRangeException(nameof(ballot))
    };
}
