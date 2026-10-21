using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using HealthcareRecruitment.Core.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace HealthcareRecruitment.Client.Services;

public class AuthService(HttpClient http, IJSRuntime js) : AuthenticationStateProvider
{
    private const string StorageKey = "healthcare-jobs.auth-token";

    public event Action? Changed;

    public bool IsAuthenticated { get; private set; }
    public string? Email { get; private set; }
    public string? FullName { get; private set; }
    public string? LastError { get; private set; }

    /// <summary>Set by <see cref="LoginAsync"/> when the credentials were correct but the account
    /// isn't verified yet — the caller should route to the verify-email page instead of just
    /// showing <see cref="LastError"/> as a dead end.</summary>
    public string? UnverifiedEmail { get; private set; }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identity = new ClaimsIdentity();

        if (IsAuthenticated)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, FullName ?? "User"),
                new Claim(ClaimTypes.Email, Email ?? "")
            };
            
            identity = new ClaimsIdentity(claims, "jwt"); 
        }

        var user = new ClaimsPrincipal(identity);
        return Task.FromResult(new AuthenticationState(user));
    }

    public async Task InitializeAsync()
    {
        var token = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            var response = await http.GetAsync("api/auth/me");
            if (response.IsSuccessStatusCode)
            {
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (auth is not null)
                {
                    ApplySession(auth);
                    return;
                }
            }
        }
        catch { }

        await ClearSessionAsync();
    }

    /// <summary>Creates the account. Returns true if the request succeeded — the account still
    /// needs the emailed code submitted to <see cref="VerifyEmailAsync"/> before it can log in.</summary>
    public async Task<bool> RegisterAsync(string email, string fullName, string password)
    {
        LastError = null;
        var response = await http.PostAsJsonAsync("api/auth/register", new RegisterRequest
        {
            Email = email,
            FullName = fullName,
            Password = password,
        });

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        LastError = await response.Content.ReadAsStringAsync() switch
        {
            { Length: > 0 } body => body.Trim('"'),
            _ => "Registration failed. Please try again.",
        };

        Changed?.Invoke();
        return false;
    }

    /// <summary>Submits the emailed verification code. On success, logs the user straight in.</summary>
    public async Task<bool> VerifyEmailAsync(string email, string code)
    {
        LastError = null;
        var response = await http.PostAsJsonAsync("api/auth/verify-email", new VerifyEmailRequest
        {
            Email = email,
            Code = code,
        });

        return await HandleAuthResponseAsync(response);
    }

    /// <summary>Asks the server to email a fresh code. Returns the server's message to show the
    /// user (it's phrased safely either way — it never confirms or denies the email is registered).</summary>
    public async Task<string> ResendVerificationAsync(string email)
    {
        const string fallback = "If that email is registered and not yet verified, a new code has been sent.";

        var response = await http.PostAsJsonAsync("api/auth/resend-verification", new ResendVerificationRequest
        {
            Email = email,
        });

        if (!response.IsSuccessStatusCode)
        {
            return fallback;
        }

        try
        {
            var message = await response.Content.ReadFromJsonAsync<MessageResponse>();
            return message?.Message ?? fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        LastError = null;
        UnverifiedEmail = null;
        var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password,
        });

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            var body = await response.Content.ReadFromJsonAsync<EmailNotVerifiedResponse>();
            UnverifiedEmail = body?.Email ?? email;
            LastError = body?.Message ?? "Please verify your email before logging in.";
            Changed?.Invoke();
            return false;
        }

        return await HandleAuthResponseAsync(response);
    }

    /// <summary>Asks the server to email a password reset code. Returns the server's message to
    /// show the user (it's phrased safely either way — it never confirms or denies the email is
    /// registered).</summary>
    public async Task<string> ForgotPasswordAsync(string email)
    {
        const string fallback = "If that email is registered, we've sent a password reset code.";

        var response = await http.PostAsJsonAsync("api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = email,
        });

        if (!response.IsSuccessStatusCode)
        {
            return fallback;
        }

        try
        {
            var message = await response.Content.ReadFromJsonAsync<MessageResponse>();
            return message?.Message ?? fallback;
        }
        catch (Exception)
        {
            // Server returned something that wasn't the expected JSON shape — don't crash the page over it.
            return fallback;
        }
    }

    /// <summary>Submits the emailed reset code with a new password. On success, logs the user
    /// straight in.</summary>
    public async Task<bool> ResetPasswordAsync(string email, string code, string newPassword)
    {
        LastError = null;
        var response = await http.PostAsJsonAsync("api/auth/reset-password", new ResetPasswordRequest
        {
            Email = email,
            Code = code,
            NewPassword = newPassword,
        });

        return await HandleAuthResponseAsync(response);
    }

    public async Task LogoutAsync()
    {
        await ClearSessionAsync();
    }

    public async Task<bool> DeleteAccountAsync()
    {
        var response = await http.DeleteAsync("api/auth/me");
        return response.IsSuccessStatusCode;
    }

    private async Task<bool> HandleAuthResponseAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            LastError = await response.Content.ReadAsStringAsync() switch
            {
                { Length: > 0 } body => body.Trim('"'),
                _ => "Something went wrong. Please try again.",
            };
            Changed?.Invoke();
            return false;
        }

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        if (auth is null)
        {
            LastError = "Unexpected response from the server.";
            Changed?.Invoke();
            return false;
        }

        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, auth.Token);
        ApplySession(auth);
        return true;
    }

    private void ApplySession(AuthResponse auth)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        IsAuthenticated = true;
        Email = auth.Email;
        FullName = auth.FullName;
        LastError = null;
        Changed?.Invoke();
        
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private async Task ClearSessionAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        http.DefaultRequestHeaders.Authorization = null;
        IsAuthenticated = false;
        Email = null;
        FullName = null;
        Changed?.Invoke();
        
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}