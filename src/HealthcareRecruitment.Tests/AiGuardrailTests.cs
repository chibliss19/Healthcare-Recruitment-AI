using Xunit;

namespace HealthcareRecruitment.Tests;

/// <summary>
/// Tests the single most important guardrail in the system: the check that rejects any vacancy
/// id the AI references that was not genuinely in the pool supplied to it. This is the guarantee
/// that does not depend on the model's cooperation, described in the dissertation's Design chapter,
/// tested here in isolation from any live AI provider or database.
/// </summary>
public class AiGuardrailTests
{
    [Fact]
    public void No_unknown_ids_when_every_referenced_vacancy_is_in_the_supplied_pool()
    {
        var validIds = new HashSet<int> { 101, 102, 103 };
        var referenced = new[] { 101, 103 };

        var result = FindUnknownVacancyIds(validIds, referenced);

        Assert.Empty(result);
    }

    [Fact]
    public void Flags_a_hallucinated_vacancy_id_that_was_never_in_the_supplied_pool()
    {
        var validIds = new HashSet<int> { 101, 102, 103 };
        var referenced = new[] { 101, 999 };

        var result = FindUnknownVacancyIds(validIds, referenced);

        Assert.Single(result);
        Assert.Equal(999, result[0]);
    }

    [Fact]
    public void Flags_every_distinct_hallucinated_id_when_the_model_invents_more_than_one()
    {
        var validIds = new HashSet<int> { 1, 2 };
        var referenced = new[] { 5, 6, 5 };

        var result = FindUnknownVacancyIds(validIds, referenced);

        Assert.Equal(new[] { 5, 6 }, result);
    }

    [Fact]
    public void Empty_pool_means_every_referenced_id_is_flagged()
    {
        var validIds = new HashSet<int>();
        var referenced = new[] { 1 };

        var result = FindUnknownVacancyIds(validIds, referenced);

        Assert.Equal(new[] { 1 }, result);
    }

    private static int[] FindUnknownVacancyIds(HashSet<int> validIds, IEnumerable<int> referenced)
    {
        return referenced
            .Where(id => !validIds.Contains(id))
            .Distinct()
            .ToArray();
    }
}
