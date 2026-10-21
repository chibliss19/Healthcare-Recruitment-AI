namespace HealthcareRecruitment.Api.Services.Email;

/// <summary>
/// Settings for sending email via Brevo's transactional HTTPS API — deliberately not SMTP.
/// Raw SMTP (both port 465 and 587) was found to hang indefinitely from this app on the dev
/// machine, even though a plain TCP connection to the same host/port succeeded instantly outside
/// the app. Two antivirus products with "mail shield" components (Avast, 360 Total Security) were
/// found running alongside Windows Defender — those commonly intercept/block SMTP specifically
/// from unrecognized processes while leaving normal HTTPS traffic untouched, which matched the
/// symptom exactly (this same app's HTTPS calls to Adzuna worked fine throughout). Routing through
/// Brevo's HTTPS API sidesteps that category of problem entirely, and is more portable to whoever
/// else runs this app, since it doesn't depend on which security software happens to be installed.
/// </summary>
public class BrevoOptions
{
    /// <summary>A Brevo API key (starts with "xkeysib-"), NOT the SMTP key (starts with
    /// "xsmtpsib-") — those are different credentials issued from different tabs in the Brevo
    /// dashboard and are not interchangeable.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;

    /// <summary>True once ApiKey and FromAddress are both filled in. When false, <see cref="BrevoEmailSender"/>
    /// logs the email instead of attempting to send it — so local development never needs a
    /// working Brevo account configured.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(FromAddress);
}
