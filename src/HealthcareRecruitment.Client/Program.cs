using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HealthcareRecruitment.Client;
using HealthcareRecruitment.Client.Services;
using MudBlazor.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Ensure the base URL explicitly targets the active backend API port (5190)
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5190";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

builder.Services.AddMudServices();
builder.Services.AddScoped<BookmarkService>();

// Authentication & Authorization Services
builder.Services.AddScoped<AuthService>(); 
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<AuthService>());

// Feature Services
builder.Services.AddScoped<SavedJobsService>();
builder.Services.AddScoped<ApplicationsService>();
builder.Services.AddScoped<AiAssistantService>();

var host = builder.Build();

// Restore a saved session (if any) before the app renders, so pages see the correct auth state immediately.
await host.Services.GetRequiredService<AuthService>().InitializeAsync();

await host.RunAsync();