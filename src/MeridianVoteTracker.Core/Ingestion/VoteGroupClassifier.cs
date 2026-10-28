using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Ingestion;

/// <summary>
/// The heart of the import (README ING-05, ING-06, ING-08..12): decides what happens to every row of ONE vote,
/// given what the system already holds as approved data. It is a pure function: no database, no clock, no I/O,
/// so every rule can be unit-tested in milliseconds.
/// </summary>
public static class VoteGroupClassifier
{
    private static readonly IReadOnlySet<ConflictKey> NoKnownConflicts = new HashSet<ConflictKey>();

    public static VoteGroupResult Classify(
        VoteGroup group,
        ApprovedVote? approved,
        IReadOnlySet<ConflictKey>? knownConflicts = null)
    {
        knownConflicts ??= NoKnownConflicts;
        var results = new List<RowResult>(group.Rows.Count);

        // ING-05: every row of the vote must carry the same title and date. If they disagree we cannot know which is right,
        // so we choose nothing: every row of this vote is reported as a contradiction and nothing is stored.
        if (group.Variants.Count > 1)
        {
            var counts = group.Rows.GroupBy(r => r.VariantIndex).ToDictionary(g => g.Key, g => g.Count());
            var described = string.Join(" | ", group.Variants.Select((v, i) =>
                $"title '{v.Title}', date {v.Date:yyyy-MM-dd} ({counts.GetValueOrDefault(i)} rows)"));
            var reason = $"Vote details differ between rows of the same vote: {described}. No value was chosen.";

            results.AddRange(group.Rows.Select(r =>
                new RowResult(r.LineNumber, RowClass.Contradiction, reason, group.VoteId, r.CountryCode, r.Ballot.ToCode())));
            return Finish(group.VoteId, null, [], [], results);
        }

        var details = group.Variants[0];
        var newCountryVotes = new List<NewCountryVote>();
        var conflicts = new List<ConflictProposal>();

        foreach (var byCountry in group.Rows.GroupBy(r => r.CountryCode))
        {
            var rows = byCountry.OrderBy(r => r.LineNumber).ToList();

            // ING-06: the same country with different ballots in one file. We do not pick one.
            if (rows.Select(r => r.Ballot).Distinct().Count() > 1)
            {
                var listing = string.Join(", ", rows.Select(r => $"{r.Ballot.ToCode()} (line {r.LineNumber})"));
                var reason = $"{byCountry.Key} appears with different ballots in this file: {listing}. No value was chosen.";
                results.AddRange(rows.Select(r =>
                    new RowResult(r.LineNumber, RowClass.Contradiction, reason, group.VoteId, r.CountryCode, r.Ballot.ToCode())));
                continue;
            }

            var first = rows[0];

            // ING-06: an identical repeat is a duplicate. The first occurrence is the one that counts.
            foreach (var repeat in rows.Skip(1))
            {
                results.Add(new RowResult(repeat.LineNumber, RowClass.Duplicate,
                    $"Identical to line {first.LineNumber}", group.VoteId, repeat.CountryCode, repeat.Ballot.ToCode()));
            }

            if (approved is null || !approved.Ballots.TryGetValue(first.CountryCode, out var existing))
            {
                // ING-10: a country missing from the system is new data, also for a vote that already exists.
                newCountryVotes.Add(new NewCountryVote(first.CountryCode, first.Ballot, first.LineNumber));
                results.Add(new RowResult(first.LineNumber, RowClass.New, null, group.VoteId, first.CountryCode, first.Ballot.ToCode()));
            }
            else if (existing == first.Ballot)
            {
                results.Add(new RowResult(first.LineNumber, RowClass.Unchanged, null, group.VoteId, first.CountryCode, first.Ballot.ToCode()));
            }
            else
            {
                // ING-08: approved data is never overwritten. A different value opens a conflict for a human to decide.
                var key = new ConflictKey(ConflictKind.Ballot, first.CountryCode, first.Ballot.ToCode());
                var known = knownConflicts.Contains(key);
                if (!known)
                {
                    conflicts.Add(new ConflictProposal(ConflictKind.Ballot, first.CountryCode,
                        existing.ToCode(), first.Ballot.ToCode(), first.LineNumber));
                }
                var reason = known
                    ? $"Differs from approved value {existing.ToCode()}; a conflict for this value already exists"
                    : $"Differs from approved value {existing.ToCode()}; conflict opened";
                results.Add(new RowResult(first.LineNumber, RowClass.Conflict, reason, group.VoteId, first.CountryCode, first.Ballot.ToCode()));
            }
        }

        // CON-03: a change in the vote's own details (title, date) is also a conflict.
        if (approved is not null)
        {
            var firstLine = group.Rows.Min(r => r.LineNumber);
            AddDetailConflict(conflicts, knownConflicts, ConflictKind.Title, approved.Title, details.Title, firstLine);
            AddDetailConflict(conflicts, knownConflicts, ConflictKind.Date,
                approved.Date.ToString("yyyy-MM-dd"), details.Date.ToString("yyyy-MM-dd"), firstLine);
        }

        var newVote = approved is null && newCountryVotes.Count > 0
            ? new NewVoteDetails(group.VoteId, group.Session, details.Title, details.Date)
            : null;

        return Finish(group.VoteId, newVote, newCountryVotes, conflicts, results);
    }

    private static void AddDetailConflict(
        List<ConflictProposal> conflicts,
        IReadOnlySet<ConflictKey> known,
        ConflictKind kind,
        string approvedValue,
        string proposedValue,
        int line)
    {
        if (approvedValue == proposedValue || known.Contains(new ConflictKey(kind, null, proposedValue)))
        {
            return;
        }
        conflicts.Add(new ConflictProposal(kind, null, approvedValue, proposedValue, line));
    }

    private static VoteGroupResult Finish(
        string voteId,
        NewVoteDetails? newVote,
        IReadOnlyList<NewCountryVote> newCountryVotes,
        IReadOnlyList<ConflictProposal> conflicts,
        List<RowResult> results)
    {
        results.Sort((a, b) => a.LineNumber.CompareTo(b.LineNumber));
        return new VoteGroupResult(voteId, newVote, newCountryVotes, conflicts, results);
    }
}
