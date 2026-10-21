using System.Text.RegularExpressions;

namespace HealthcareRecruitment.Api.Services;

public static partial class VacancyNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>
    /// Builds the strict composite dedupe key from title + employer + location:
    /// lowercased, trimmed, internal whitespace collapsed.
    /// </summary>
    public static string BuildDedupeKey(string title, string employer, string location)
    {
        static string Norm(string s) => Whitespace().Replace(s.Trim().ToLowerInvariant(), " ");
        return $"{Norm(title)}|{Norm(employer)}|{Norm(location)}";
    }
}
