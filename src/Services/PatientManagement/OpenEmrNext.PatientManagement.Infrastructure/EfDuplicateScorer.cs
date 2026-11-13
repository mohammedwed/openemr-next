using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IDuplicateScorer"/>: runs the exact same weighted comparison
/// legacy OpenEMR's <c>DuplicatePatientService::dupScoreSql()</c> and <c>recalculateScore()</c> use
/// (six points each for email/DOB/name/sex exact matches, two for a surname soundex hit, one for any
/// shared phone number), against the real <c>patient_data</c> table directly -- not through the
/// <see cref="Patient"/> aggregate, which maps only the columns registration touches and was never
/// meant to hold a bulk-comparison formula. Two round trips (score, then write), matching the legacy
/// code's own shape exactly, not one atomic statement.
/// </summary>
public sealed class EfDuplicateScorer(PatientManagementDbContext db) : IDuplicateScorer
{
    // p1 is the patient being scored, p2 the candidate being compared against -- same aliasing as
    // legacy's dupScoreSql(). The fname+lname concatenation uses no separator between them
    // (CONCAT(fname, '', lname)), a known legacy quirk reproduced here deliberately: a different
    // concatenation would silently stop matching rows legacy's own scorer already matches.
    private const string ScoreSql = """
        SELECT COALESCE(MAX(
            6 * (TRIM(p1.email) != '' AND TRIM(p1.email) = TRIM(p2.email)) +
            6 * (p1.DOB IS NOT NULL AND p2.DOB IS NOT NULL AND p1.DOB = p2.DOB) +
            6 * (LOWER(CONCAT(TRIM(p1.fname), '', TRIM(p1.lname))) = LOWER(CONCAT(TRIM(p2.fname), '', TRIM(p2.lname)))) +
            6 * (TRIM(p1.sex) != '' AND TRIM(p1.sex) = TRIM(p2.sex)) +
            2 * (SOUNDEX(p1.lname) = SOUNDEX(p2.lname)) +
            1 * (
                (TRIM(p1.phone_home) != '' AND (
                    REPLACE(REPLACE(p1.phone_home, '-', ''), ' ', '') IN (
                        REPLACE(REPLACE(p2.phone_home, '-', ''), ' ', ''),
                        REPLACE(REPLACE(p2.phone_biz , '-', ''), ' ', ''),
                        REPLACE(REPLACE(p2.phone_cell, '-', ''), ' ', ''))))
                OR (TRIM(p1.phone_biz) != '' AND (
                    REPLACE(REPLACE(p1.phone_biz , '-', ''), ' ', '') IN (
                        REPLACE(REPLACE(p2.phone_biz , '-', ''), ' ', ''),
                        REPLACE(REPLACE(p2.phone_cell, '-', ''), ' ', ''))))
                OR (TRIM(p1.phone_cell) != '' AND (
                    REPLACE(REPLACE(p1.phone_cell, '-', ''), ' ', '') =
                    REPLACE(REPLACE(p2.phone_cell, '-', ''), ' ', '')))
            )
        ), 0)
        FROM patient_data AS p1, patient_data AS p2
        WHERE p1.pid = {0} AND p2.pid != p1.pid AND p2.dupscore != {1}
        """;

    public async Task<int> RescoreAsync(long pid, CancellationToken ct)
    {
        var scores = await db.Database
            .SqlQueryRaw<int>(ScoreSql, pid, DuplicateScore.Unique)
            .ToListAsync(ct);
        var score = scores.SingleOrDefault();

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE patient_data SET dupscore = {score} WHERE pid = {pid}", ct);

        return score;
    }
}
