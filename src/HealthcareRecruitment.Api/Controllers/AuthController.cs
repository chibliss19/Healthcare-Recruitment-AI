using System.Security.Cryptography;
using System.Text;
using HealthcareRecruitment.Api.Data;
using HealthcareRecruitment.Api.Data.Entities;
using HealthcareRecruitment.Api.Services.Auth;
using HealthcareRecruitment.Api.Services.Email;
using HealthcareRecruitment.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserEntity = HealthcareRecruitment.Api.Data.Entities.User;

namespace HealthcareRecruitment.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, TokenService tokens, IEmailSender emailSender, ILogger<AuthController> logger) : ControllerBase
{
    private static readonly TimeSpan VerificationCodeLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Creates the account but does not log the user in — it can't be used until the
    /// emailed verification code is submitted to <see cref="VerifyEmail"/>.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest("Email and full name are required.");
        }

        if (request.Password.Length < 8)
        {
            return BadRequest("Password must be at least 8 characters.");
        }

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return Conflict("An account with this email already exists.");
        }

        var code = GenerateSixDigitCode();

        var user = new UserEntity
        {
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false,
            VerificationToken = code,
            VerificationTokenExpiresAt = DateTime.UtcNow.Add(VerificationCodeLifetime),
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        await SendVerificationCodeEmailAsync(user, code, ct);

        logger.LogInformation("New user registered, pending email verification: {Email}", user.Email);

        return Ok(new RegisterResponse
        {
            Email = user.Email,
            Message = "Account created. Enter the verification code we emailed you to finish signing up.",
        });
    }

    /// <summary>Submits the code emailed at registration (or resend). On success the account is
    /// verified and logged in immediately — no separate login step needed, since the user has
    /// already proven both the password (at registration) and ownership of the email (this code).</summary>
    [HttpPost("verify-email")]
    public async Task<ActionResult<AuthResponse>> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var code = request.Code.Trim();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return BadRequest("Invalid email or verification code.");
        }

        if (user.IsEmailVerified)
        {
            return BadRequest("This email is already verified. Please log in.");
        }

        var validCode = !string.IsNullOrEmpty(user.VerificationToken)
            && user.VerificationTokenExpiresAt is { } expiresAt
            && expiresAt > DateTime.UtcNow
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(code),
                Encoding.UTF8.GetBytes(user.VerificationToken));

        if (!validCode)
        {
            return BadRequest("Invalid or expired verification code.");
        }

        user.IsEmailVerified = true;
        user.VerificationToken = null;
        user.VerificationTokenExpiresAt = null;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Email verified: {Email}", user.Email);

        return BuildAuthResponse(user);
    }

    /// <summary>Anonymous by design — the user isn't logged in yet at this point in the flow.
    /// Doesn't reveal whether an email is registered, to avoid enumeration.</summary>
    [HttpPost("resend-verification")]
    public async Task<ActionResult<MessageResponse>> ResendVerification(ResendVerificationRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            return Ok(new MessageResponse { Message = "If that email is registered and not yet verified, a new code has been sent." });
        }

        if (user.IsEmailVerified)
        {
            return Ok(new MessageResponse { Message = "This email is already verified — you can log in." });
        }

        var code = GenerateSixDigitCode();
        user.VerificationToken = code;
        user.VerificationTokenExpiresAt = DateTime.UtcNow.Add(VerificationCodeLifetime);
        await db.SaveChangesAsync(ct);

        await SendVerificationCodeEmailAsync(user, code, ct);

        return Ok(new MessageResponse { Message = "A new verification code has been sent to your email." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Same generic message whether the email is unknown or the password is wrong —
        // don't reveal which one it was.
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized("Invalid email or password.");
        }

        if (!user.IsEmailVerified)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new EmailNotVerifiedResponse
            {
                Email = user.Email,
                Message = "Please verify your email before logging in.",
            });
        }

        return BuildAuthResponse(user);
    }

    /// <summary>Always returns the same generic message whether or not the email is registered —
    /// can't be used to enumerate accounts. Safe to call repeatedly; each call issues a fresh code.</summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<MessageResponse>> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        const string genericMessage = "If that email is registered, we've sent a password reset code.";

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is not null)
        {
            var code = GenerateSixDigitCode();
            user.PasswordResetToken = code;
            user.PasswordResetTokenExpiresAt = DateTime.UtcNow.Add(VerificationCodeLifetime);
            await db.SaveChangesAsync(ct);

            await SendPasswordResetEmailAsync(user, code, ct);
        }

        return Ok(new MessageResponse { Message = genericMessage });
    }

    /// <summary>Submits the emailed reset code along with a new password. On success, logs the
    /// user straight in — they've already proven both email ownership (this code) and deliberate
    /// intent by choosing the new password, so a separate login step adds nothing.</summary>
    [HttpPost("reset-password")]
    public async Task<ActionResult<AuthResponse>> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        if (request.NewPassword.Length < 8)
        {
            return BadRequest("Password must be at least 8 characters.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var code = request.Code.Trim();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return BadRequest("Invalid email or reset code.");
        }

        var validCode = !string.IsNullOrEmpty(user.PasswordResetToken)
            && user.PasswordResetTokenExpiresAt is { } expiresAt
            && expiresAt > DateTime.UtcNow
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(code),
                Encoding.UTF8.GetBytes(user.PasswordResetToken));

        if (!validCode)
        {
            return BadRequest("Invalid or expired reset code.");
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiresAt = null;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Password reset: {Email}", user.Email);

        return BuildAuthResponse(user);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthResponse>> Me(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null) return Unauthorized();

        return BuildAuthResponse(user);
    }

    [Authorize]
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var user = await db.Users.FindAsync([userId], ct);

        if (user is null) return NotFound();

        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);

        return Ok();
    }

    private async Task SendVerificationCodeEmailAsync(UserEntity user, string code, CancellationToken ct)
    {
        const string subject = "Your verification code — Healthcare Vacancies NI";
        var textBody = $"""
            Hi {user.FullName},

            Your verification code is: {code}

            Enter this code in the app to finish creating your account. It expires in 15 minutes.

            If you didn't request this, you can ignore this email.
            """;
        var htmlBody = $"""
            <p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
            <p>Your verification code is:</p>
            <p style="font-size: 28px; font-weight: 700; letter-spacing: 4px;">{code}</p>
            <p>Enter this code in the app to finish creating your account. It expires in 15 minutes.</p>
            <p>If you didn't request this, you can ignore this email.</p>
            """;

        await emailSender.SendAsync(user.Email, subject, htmlBody, textBody, ct);
    }

    private async Task SendPasswordResetEmailAsync(UserEntity user, string code, CancellationToken ct)
    {
        const string subject = "Your password reset code — Healthcare Vacancies NI";
        var textBody = $"""
            Hi {user.FullName},

            Your password reset code is: {code}

            Enter this code in the app to choose a new password. It expires in 15 minutes.

            If you didn't request this, you can ignore this email — your password won't change.
            """;
        var htmlBody = $"""
            <p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
            <p>Your password reset code is:</p>
            <p style="font-size: 28px; font-weight: 700; letter-spacing: 4px;">{code}</p>
            <p>Enter this code in the app to choose a new password. It expires in 15 minutes.</p>
            <p>If you didn't request this, you can ignore this email — your password won't change.</p>
            """;

        await emailSender.SendAsync(user.Email, subject, htmlBody, textBody, ct);
    }

    private static string GenerateSixDigitCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private AuthResponse BuildAuthResponse(UserEntity user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse { Token = token, ExpiresAt = expiresAt, Email = user.Email, FullName = user.FullName };
    }
}
