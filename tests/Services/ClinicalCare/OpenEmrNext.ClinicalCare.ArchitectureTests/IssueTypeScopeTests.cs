using System.Text.RegularExpressions;

namespace OpenEmrNext.ClinicalCare.ArchitectureTests;

/// <summary>
/// ADR 0070 #2 as a build-time rule. <c>lists</c> holds every issue type (problems, allergies, medications, surgeries, ...) and a database grant cannot restrict <i>rows</i>, so the only thing
/// keeping a problem endpoint from reading or changing an allergy, a medication or a surgery is that <b>every statement that touches <c>lists</c> also names <c>type</c></b>. Until now that was
/// pinned by behavioural tests that try to reach another type's row; this adds the structural check, so a new statement that forgets the filter fails here, even before a test is written for it.
/// </summary>
/// <remarks>
/// It reads the infrastructure project's source and examines every SQL string (a raw <c>"""</c> block or a one-line interpolated string) that reads, joins, updates or inserts into <c>lists</c>
/// (<c>lists_touch</c> and the other <c>lists_*</c> tables are different tables and are not matched). It is deliberately a text check: it cannot prove the filter is <i>correct</i>, only that it
/// is present, which is the mistake that matters and the one a reviewer misses.
/// </remarks>
public class IssueTypeScopeTests
{
    private static readonly Regex TouchesLists = new(@"\b(?:FROM|JOIN|UPDATE|INTO)\s+lists\b(?!_)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex NamesType = new(@"\btype\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string InfrastructureDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Services", "ClinicalCare", "OpenEmrNext.ClinicalCare.Infrastructure");
            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException("Could not find the ClinicalCare Infrastructure source above " + AppContext.BaseDirectory);
    }

    /// <summary>Every SQL string in the project's source: raw <c>"""</c> blocks, and one-line interpolated strings.</summary>
    private static IEnumerable<(string File, string Sql)> SqlStrings()
    {
        foreach (var file in Directory.EnumerateFiles(InfrastructureDirectory(), "*.cs", SearchOption.TopDirectoryOnly))
        {
            var text = File.ReadAllText(file);
            var parts = text.Split("\"\"\"");
            for (var i = 1; i < parts.Length; i += 2)   // odd segments are inside a raw string literal
                yield return (Path.GetFileName(file), parts[i]);

            foreach (Match m in Regex.Matches(text, "\\$\"([^\"\\r\\n]*)\""))
                yield return (Path.GetFileName(file), m.Groups[1].Value);
        }
    }

    [Fact]
    public void Every_statement_that_touches_lists_also_names_the_issue_type()
    {
        var touching = SqlStrings().Where(s => TouchesLists.IsMatch(s.Sql)).ToList();

        var unscoped = touching.Where(s => !NamesType.IsMatch(s.Sql)).Select(s => $"{s.File}: {Squash(s.Sql)}").ToList();

        Assert.True(touching.Count >= 12, $"Found only {touching.Count} statements on lists: the scan is not reading what it should.");
        Assert.True(unscoped.Count == 0, "These statements touch lists without naming type (ADR 0070 #2):\n" + string.Join("\n", unscoped));
    }

    [Fact]
    public void The_check_would_notice_a_statement_that_forgets_the_filter()
    {
        // The rule's own test: prove the two regexes tell a scoped statement from an unscoped one, so a green result above means something.
        const string scoped = "SELECT id FROM lists WHERE uuid = {u} AND type = {t}";
        const string unscoped = "SELECT id FROM lists WHERE uuid = {u}";
        const string otherTable = "INSERT IGNORE INTO lists_touch (pid, type) VALUES (1, 'x')";

        Assert.True(TouchesLists.IsMatch(scoped) && NamesType.IsMatch(scoped));
        Assert.True(TouchesLists.IsMatch(unscoped) && !NamesType.IsMatch(unscoped));
        Assert.False(TouchesLists.IsMatch(otherTable));
        Assert.False(TouchesLists.IsMatch("SELECT * FROM lists_medication"));
        Assert.True(TouchesLists.IsMatch("UPDATE lists\n SET title = {x}\n WHERE uuid = {u}"));
        Assert.True(TouchesLists.IsMatch("JOIN lists l ON l.id = ie.list_id"));
    }

    private static string Squash(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();
}
