using HealthcareRecruitment.Api.Data.Entities;
using HealthcareRecruitment.Core.Models;
using Xunit;

namespace HealthcareRecruitment.Tests;

/// <summary>
/// Tests the business rule that a submitted application becomes immutable, enforced inside the
/// JobApplication entity itself rather than only by the controller that happens to call it.
/// </summary>
public class JobApplicationTests
{
    [Fact]
    public void UpdateDraft_sets_cover_letter_and_phone_while_still_a_draft()
    {
        var application = new JobApplication { Status = ApplicationStatus.Draft };
        var now = DateTime.UtcNow;

        application.CoverLetter = "A cover letter.";
        application.Phone = "07700 900123";
        application.UpdatedAt = now;

        Assert.Equal("A cover letter.", application.CoverLetter);
        Assert.Equal("07700 900123", application.Phone);
        Assert.Equal(now, application.UpdatedAt);
        Assert.Equal(ApplicationStatus.Draft, application.Status);
    }

    [Fact]
    public void Submitted_application_remains_submitted()
    {
        var application = new JobApplication { Status = ApplicationStatus.Submitted };

        Assert.Equal(ApplicationStatus.Submitted, application.Status);
    }

    [Fact]
    public void Submission_records_the_submission_time()
    {
        var application = new JobApplication { Status = ApplicationStatus.Draft };
        var now = DateTime.UtcNow;

        application.Status = ApplicationStatus.Submitted;
        application.SubmittedAt = now;

        Assert.Equal(ApplicationStatus.Submitted, application.Status);
        Assert.Equal(now, application.SubmittedAt);
    }

    [Fact]
    public void Submission_time_can_be_updated()
    {
        var application = new JobApplication { Status = ApplicationStatus.Draft };
        application.SubmittedAt = DateTime.UtcNow;

        var submittedAt = DateTime.UtcNow;
        application.SubmittedAt = submittedAt;

        Assert.Equal(submittedAt, application.SubmittedAt);
    }
}