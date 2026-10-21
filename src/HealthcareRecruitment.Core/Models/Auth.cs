namespace HealthcareRecruitment.Core.Models;

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Returned on successful register/login. Never carries the password or its hash.</summary>
public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

/// <summary>Returned after Register — no token yet, since the account can't be used until the
/// emailed verification code is entered.</summary>
public class RegisterResponse
{
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class VerifyEmailRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ResendVerificationRequest
{
    public string Email { get; set; } = string.Empty;
}

public class MessageResponse
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>Body of the 403 Login returns when the credentials are correct but the account isn't
/// verified yet — lets the client route straight to the "enter code" screen instead of showing a
/// generic error.</summary>
public class EmailNotVerifiedResponse
{
    public string Error { get; set; } = "email_not_verified";
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
