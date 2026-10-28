using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Core.Ingestion;

/// <summary>
/// Lookup over the member-state list. A country can have several membership periods
/// (Iceland appears twice), so membership is always asked for a specific date.
/// </summary>
public sealed class MemberDirectory
{
    private readonly Dictionary<string, List<MemberState>> _byCode = new(StringComparer.Ordinal);

    public MemberDirectory(IEnumerable<MemberState> states)
    {
        foreach (var state in states)
        {
            if (!_byCode.TryGetValue(state.CountryCode, out var periods))
            {
                periods = [];
                _byCode[state.CountryCode] = periods;
            }
            periods.Add(state);
        }
    }

    public bool Exists(string code) => _byCode.ContainsKey(code);

    /// <summary>Returns the one shared string instance for a code, so millions of rows do not each hold their own copy.</summary>
    public bool TryGetCanonicalCode(string code, out string canonical)
    {
        if (_byCode.TryGetValue(code, out var periods))
        {
            canonical = periods[0].CountryCode;
            return true;
        }
        canonical = string.Empty;
        return false;
    }

    public bool IsMemberOn(string code, DateOnly date) =>
        _byCode.TryGetValue(code, out var periods) && periods.Any(p => p.IsMemberOn(date));
}
