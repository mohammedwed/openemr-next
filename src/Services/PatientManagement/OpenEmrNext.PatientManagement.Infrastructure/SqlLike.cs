namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// Builds a "contains" pattern for a SQL <c>LIKE</c> from text a caller typed. The text is still bound as a parameter (so it can
/// never change the statement), but inside a <c>LIKE</c> a bound <c>%</c> or <c>_</c> is a wildcard, so a caller who sends <c>%</c>
/// would match every row. The wildcards are escaped so the text is matched as typed; the statement must say
/// <c>ESCAPE '!'</c> (an explicit character, so the result does not depend on the server's backslash setting).
/// </summary>
internal static class SqlLike
{
    public const char EscapeCharacter = '!';

    public static string Contains(string text) =>
        "%" + text.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_") + "%";
}
