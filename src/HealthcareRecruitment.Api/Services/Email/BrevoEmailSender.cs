using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace HealthcareRecruitment.Api.Services.Email;

/// <summary>
/// Sends email via Brevo's transactional HTTPS API (see <see cref="BrevoOptions"/> for why this
/// isn't SMTP). Bounded to 15 seconds so a network hiccup can't hang the caller's request —
/// registration, verification resend, password reset — forever. On any failure, timeout included,
/// the full email is logged as a fallback so the code/link inside it is still usable for testing
/// even when real delivery fails. When Brevo isn't configured at all, every send just logs — local
/// development never needs a working Brevo account.
/// </summary>
public class BrevoEmailSender(HttpClient http, IOptions<BrevoOptions> options, ILogger<BrevoEmailSender> logger) : IEmailSender
{
    private const string SendEndpoint = "https://api.brevo.com/v3/smtp/email";
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(15);
    private readonly BrevoOptions _options = options.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, string textBody, CancellationToken ct)
    {
        if (!_options.IsConfigured)
        {
            logger.LogInformation(
                "Brevo not configured — logging email instead of sending.\nTo: {To}\nSubject: {Subject}\n\n{Body}",
                toEmail, subject, textBody);
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(SendTimeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint);
            request.Headers.Add("api-key", _options.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = JsonContent.Create(new
            {
                sender = new { name = _options.FromName, email = _options.FromAddress },
                to = new[] { new { email = toEmail } },
                subject,
                htmlContent = htmlBody,
                textContent = textBody,
            });

            using var response = await http.SendAsync(request, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(CancellationToken.None);
                throw new HttpRequestException($"Brevo API returned {(int)response.StatusCode} {response.StatusCode}: {body}");
            }
        }
        catch (Exception ex)
        {
            var reason = timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested
                ? "timed out after 15s"
                : ex.Message;

            logger.LogWarning(ex,
                "Failed to send email to {To} via Brevo API ({Reason}) — logging full content as fallback.\nSubject: {Subject}\n\n{Body}",
                toEmail, reason, subject, textBody);
        }
    }
}
