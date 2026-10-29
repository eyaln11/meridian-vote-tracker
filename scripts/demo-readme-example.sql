-- Learning aid, NOT part of the product: the README section 7.2 example, written by hand as rows
-- in the real tables, so you can SEE what the system stores after the two uploads.
-- Run it only against the copy database "meridian_demo", never against "meridian".
--
-- Cast of characters: user 1 = Dana (Analyst), user 2 = Omer (Analyst), user 3 = Lena (Admin).
-- Upload 1 = original.csv (Dana, 2 April), upload 2 = corrected.csv (Dana, 20 April).

-- ===== The two uploads (imports) =====
INSERT INTO imports (id, file_name, stored_path, size_bytes, status, stage, uploaded_by_user_id, uploaded_at, started_at, finished_at,
                     total_rows, rows_parsed, rows_applied, new_count, unchanged_count, duplicate_count, invalid_count,
                     contradiction_count, conflict_count, conflicts_opened, votes_created)
VALUES
 ('e1111111-1111-1111-1111-111111111111', 'original.csv',  '/data/uploads/e1111111.csv', 700, 'Completed', 'Done', 1,
  '2024-04-02 09:15:00+00', '2024-04-02 09:15:01+00', '2024-04-02 09:15:02+00', 8, 8, 8, 3, 0, 1, 2, 2, 0, 0, 2),
 ('e2222222-2222-2222-2222-222222222222', 'corrected.csv', '/data/uploads/e2222222.csv', 450, 'Completed', 'Done', 1,
  '2024-04-20 10:00:00+00', '2024-04-20 10:00:01+00', '2024-04-20 10:00:02+00', 5, 5, 5, 3, 1, 0, 0, 0, 1, 2, 3);

-- ===== The votes that exist after the uploads =====
-- 103 and 104 did not exist after upload 1 (all their rows were contradictory or invalid); upload 2 created them.
INSERT INTO votes (id, assembly_vote_id, session, title, vote_date, details_import_id, created_at) VALUES
 (1, 'MA/2024/101', 79, 'Maritime Safety', '2024-01-10', 'e1111111-1111-1111-1111-111111111111', '2024-04-02 09:15:02+00'),
 (2, 'MA/2024/102', 79, 'Food Security',   '2024-01-11', 'e1111111-1111-1111-1111-111111111111', '2024-04-02 09:15:02+00'),
 (3, 'MA/2024/103', 79, 'Digital Trade',   '2024-01-12', 'e2222222-2222-2222-2222-222222222222', '2024-04-20 10:00:02+00'),
 (4, 'MA/2024/104', 79, 'Climate Finance', '2024-01-15', 'e2222222-2222-2222-2222-222222222222', '2024-04-20 10:00:02+00'),
 (5, 'MA/2024/105', 79, 'Public Health',   '2024-02-01', 'e2222222-2222-2222-2222-222222222222', '2024-04-20 10:00:02+00');

-- ===== The approved ballots: who voted what, and exactly which file and line it came from =====
-- Brazil in vote 101 is 'A' now: Dana accepted the conflict (see the conflicts table), so its source is upload 2 line 2.
INSERT INTO country_votes (vote_id, country_code, ballot, source_import_id, source_line, approved_by_user_id, approved_at) VALUES
 (1, 'BRA', 'A', 'e2222222-2222-2222-2222-222222222222', 2, 1,    '2024-04-20 11:30:00+00'),
 (1, 'IND', 'Y', 'e1111111-1111-1111-1111-111111111111', 3, NULL, '2024-04-02 09:15:02+00'),
 (2, 'JPN', 'Y', 'e1111111-1111-1111-1111-111111111111', 4, NULL, '2024-04-02 09:15:02+00'),
 (3, 'FRA', 'N', 'e2222222-2222-2222-2222-222222222222', 4, NULL, '2024-04-20 10:00:02+00'),
 (4, 'FRA', 'X', 'e2222222-2222-2222-2222-222222222222', 5, NULL, '2024-04-20 10:00:02+00'),
 (5, 'BRA', 'Y', 'e2222222-2222-2222-2222-222222222222', 6, NULL, '2024-04-20 10:00:02+00');

-- ===== Rows that need an explanation (the import report). New and unchanged rows are not listed here. =====
INSERT INTO import_row_results (import_id, line_number, class, reason, assembly_vote_id, country_code, original_value) VALUES
 ('e1111111-1111-1111-1111-111111111111', 5, 'Duplicate',     'Identical to line 4', 'MA/2024/102', 'JPN', 'Y'),
 ('e1111111-1111-1111-1111-111111111111', 6, 'Contradiction', 'FRA appears with different ballots in this file: Y (line 6), N (line 7). No value was chosen.', 'MA/2024/103', 'FRA', 'Y'),
 ('e1111111-1111-1111-1111-111111111111', 7, 'Contradiction', 'FRA appears with different ballots in this file: Y (line 6), N (line 7). No value was chosen.', 'MA/2024/103', 'FRA', 'N'),
 ('e1111111-1111-1111-1111-111111111111', 8, 'Invalid',       'country_code ''FRN'' is not in the member-state list', 'MA/2024/104', 'FRN', 'MA/2024/104,79,2024-01-15,Climate Finance,FRN,France,X,2024-04-02T09:00:00Z'),
 ('e1111111-1111-1111-1111-111111111111', 9, 'Invalid',       'country_code ''NZL'' was not a member on 2024-01-15', 'MA/2024/104', 'NZL', 'MA/2024/104,79,2024-01-15,Climate Finance,NZL,New Zealand,Y,2024-04-02T09:00:00Z'),
 ('e2222222-2222-2222-2222-222222222222', 2, 'Conflict',      'Differs from approved value Y; conflict opened', 'MA/2024/101', 'BRA', 'A');

-- ===== "What happened in each vote" for each upload =====
INSERT INTO import_vote_summaries (import_id, assembly_vote_id, vote_created, new_count, unchanged_count, duplicate_count, contradiction_count, conflict_count) VALUES
 ('e1111111-1111-1111-1111-111111111111', 'MA/2024/101', true,  2, 0, 0, 0, 0),
 ('e1111111-1111-1111-1111-111111111111', 'MA/2024/102', true,  1, 0, 1, 0, 0),
 ('e1111111-1111-1111-1111-111111111111', 'MA/2024/103', false, 0, 0, 0, 2, 0),
 ('e2222222-2222-2222-2222-222222222222', 'MA/2024/101', false, 0, 1, 0, 0, 1),
 ('e2222222-2222-2222-2222-222222222222', 'MA/2024/103', true,  1, 0, 0, 0, 0),
 ('e2222222-2222-2222-2222-222222222222', 'MA/2024/104', true,  1, 0, 0, 0, 0),
 ('e2222222-2222-2222-2222-222222222222', 'MA/2024/105', true,  1, 0, 0, 0, 0);

-- ===== Conflicts =====
-- 1: Brazil's ballot in vote 101 (Y approved, A proposed). Dana accepted it.
-- 2: the title of vote 101. Nobody decided yet: it waits in the Review screen.
INSERT INTO conflicts (id, vote_id, kind, country_code, approved_value, proposed_value, approved_import_id, proposed_import_id,
                       proposed_line, opened_at, status, resolved_by_user_id, resolved_at, resolution_note) VALUES
 (1, 1, 'Ballot', 'BRA', 'Y', 'A', 'e1111111-1111-1111-1111-111111111111', 'e2222222-2222-2222-2222-222222222222', 2,
  '2024-04-20 10:00:02+00', 'Accepted', 1, '2024-04-20 11:30:00+00', 'The assembly corrected its own error'),
 (2, 1, 'Title',  NULL,  'Maritime Safety', 'Maritime Safety and Trade', 'e1111111-1111-1111-1111-111111111111', 'e2222222-2222-2222-2222-222222222222', 2,
  '2024-04-20 10:00:02+00', 'Open', NULL, NULL, NULL);

-- ===== The position of the institute on vote 101: Lena set weight 4, later changed it to 5 =====
INSERT INTO positions (vote_id, stance, weight, updated_by_user_id, updated_at) VALUES
 (1, 'Yes', 5, 3, '2024-05-02 14:00:00+00');

-- ===== The timeline of vote 101 (what the Votes screen shows) =====
INSERT INTO vote_events (vote_id, type, occurred_at, actor_user_id, import_id, conflict_id, country_code, old_value, new_value, note) VALUES
 (1, 'VoteCreated',       '2024-04-02 09:15:02+00', 1, 'e1111111-1111-1111-1111-111111111111', NULL, NULL,  NULL,    NULL,       'Created with 2 country votes from original.csv'),
 (1, 'ConflictOpened',    '2024-04-20 10:00:02+00', 1, 'e2222222-2222-2222-2222-222222222222', 1,    'BRA', 'Y',     'A',        'corrected.csv line 2'),
 (1, 'ConflictOpened',    '2024-04-20 10:00:02+00', 1, 'e2222222-2222-2222-2222-222222222222', 2,    NULL,  'Maritime Safety', 'Maritime Safety and Trade', 'corrected.csv title differs'),
 (1, 'ConflictAccepted',  '2024-04-20 11:30:00+00', 1, NULL,                                   1,    'BRA', 'Y',     'A',        'The assembly corrected its own error'),
 (1, 'PositionSet',       '2024-05-01 09:00:00+00', 3, NULL, NULL, NULL, NULL,      'Yes / weight 4', NULL),
 (1, 'PositionChanged',   '2024-05-02 14:00:00+00', 3, NULL, NULL, NULL, 'Yes / weight 4', 'Yes / weight 5', NULL);

INSERT INTO vote_events (vote_id, type, occurred_at, actor_user_id, import_id, note) VALUES
 (2, 'VoteCreated', '2024-04-02 09:15:02+00', 1, 'e1111111-1111-1111-1111-111111111111', 'Created with 1 country vote from original.csv'),
 (3, 'VoteCreated', '2024-04-20 10:00:02+00', 1, 'e2222222-2222-2222-2222-222222222222', 'Created with 1 country vote from corrected.csv'),
 (4, 'VoteCreated', '2024-04-20 10:00:02+00', 1, 'e2222222-2222-2222-2222-222222222222', 'Created with 1 country vote from corrected.csv'),
 (5, 'VoteCreated', '2024-04-20 10:00:02+00', 1, 'e2222222-2222-2222-2222-222222222222', 'Created with 1 country vote from corrected.csv');
