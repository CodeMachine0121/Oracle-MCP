using System.Text.RegularExpressions;

namespace Oracle;

/// <summary>
/// Provides validation and utility helpers to ensure Oracle SQL statements are read-only
/// and to safely wrap or sanitize SQL before execution.
/// </summary>
public static class OracleSqlGuard
{
    /// <summary>
    /// Regular expression matching potentially dangerous SQL keywords that indicate mutating or DDL statements.
    /// </summary>
    private static readonly Regex DangerousSqlRegex = new(
        @"\b(insert|update|delete|merge|drop|alter|truncate|create|grant|revoke|begin|declare|commit|rollback|execute|exec|call)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Regular expression that detects "FOR UPDATE" clauses in SELECT statements.
    /// </summary>
    private static readonly Regex ForUpdateRegex = new(
        @"\bfor\s+update\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Determines whether the provided SQL is read-only.
    /// </summary>
    /// <param name="sql">The SQL text to validate.</param>
    /// <param name="reason">When the method returns false, contains a brief reason why the SQL was rejected; otherwise null.</param>
    /// <returns>True if the SQL is considered read-only and safe; otherwise false.</returns>
    public static bool IsReadonlySql(string sql, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(sql))
        {
            reason = "SQL is empty.";
            return false;
        }

        if (sql.Contains(';', StringComparison.Ordinal))
        {
            reason = "Multiple statements are not allowed.";
            return false;
        }

        string leading = StripLeadingComments(sql).TrimStart();
        if (leading.Length == 0)
        {
            reason = "SQL is empty after trimming comments.";
            return false;
        }

        bool startsReadOnly =
            leading.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
            leading.StartsWith("with", StringComparison.OrdinalIgnoreCase);

        if (!startsReadOnly)
        {
            reason = "SQL must start with SELECT or WITH.";
            return false;
        }

        string sanitized = RemoveSingleQuotedLiterals(leading);
        if (DangerousSqlRegex.IsMatch(sanitized))
        {
            reason = "Detected potentially non-read-only keyword.";
            return false;
        }

        if (ForUpdateRegex.IsMatch(sanitized))
        {
            reason = "SELECT ... FOR UPDATE is not allowed.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Wraps the provided SQL in an outer query that limits rows using ROWNUM and a bind parameter.
    /// </summary>
    /// <param name="sql">The original SQL to wrap.</param>
    /// <returns>The wrapped SQL that limits the number of returned rows.</returns>
    public static string WrapWithRowNumLimit(string sql)
    {
        return $"select * from ({sql}) where rownum <= :mcp_max_rows";
    }

    /// <summary>
    /// Clamps the provided max-rows value to the specified upper bound, ensuring a minimum of 1.
    /// </summary>
    /// <param name="value">Requested max rows.</param>
    /// <param name="max">Maximum allowed rows.</param>
    /// <returns>An integer between 1 and <paramref name="max"/> inclusive.</returns>
    public static int ClampMaxRows(int value, int max)
    {
        if (value <= 0) return 1;

        return value > max
            ? max
            : value;
    }

    /// <summary>
    /// Replaces all characters inside single-quoted string literals with spaces and returns the modified SQL.
    /// If an unterminated string literal is detected, returns an empty string.
    /// </summary>
    /// <param name="sql">The SQL to sanitize.</param>
    /// <returns>The SQL with single-quoted literals removed (replaced by spaces), or an empty string for unterminated literals.</returns>
    public static string RemoveSingleQuotedLiterals(string sql)
    {
        char[] chars = sql.ToCharArray();
        bool inString = false;

        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] != '\'')
            {
                if (inString) chars[i] = ' ';
                continue;
            }

            if (!inString)
            {
                inString = true;
                chars[i] = ' ';
                continue;
            }

            if (i + 1 < chars.Length && chars[i + 1] == '\'')
            {
                chars[i] = ' ';
                chars[i + 1] = ' ';
                i++;
                continue;
            }

            inString = false;
            chars[i] = ' ';
        }

        if (inString)
        {
            return string.Empty;
        }

        return new string(chars);
    }

    /// <summary>
    /// Removes leading SQL comments (single-line -- and block /* */) and returns the remaining SQL starting with the first non-comment token.
    /// </summary>
    /// <param name="sql">The SQL to strip leading comments from.</param>
    /// <returns>The SQL with leading comments removed.</returns>
    private static string StripLeadingComments(string sql)
    {
        string s = sql;
        while (true)
        {
            string trimmed = s.TrimStart();
            if (trimmed.StartsWith("--", StringComparison.Ordinal))
            {
                int newline = trimmed.IndexOf('\n');
                s = newline >= 0
                    ? trimmed[(newline + 1)..]
                    : string.Empty;
                continue;
            }

            if (trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                int end = trimmed.IndexOf("*/", StringComparison.Ordinal);
                s = end >= 0
                    ? trimmed[(end + 2)..]
                    : string.Empty;
                continue;
            }

            return trimmed;
        }
    }
}