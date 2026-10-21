using System.Text;
using HealthcareRecruitment.Api.Data;
using HealthcareRecruitment.Api.Services;
using HealthcareRecruitment.Api.Services.Ai;
using HealthcareRecruitment.Api.Services.Auth;
using HealthcareRecruitment.Api.Services.Email;
using HealthcareRecruitment.Api.Services.Sources;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models; // Required for Swagger setup

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ✅ ESSENTIAL ADDITION: Configured Swagger to allow JWT Token pasting for testing secure endpoints
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Healthcare Recruitment API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

//  ESSENTIAL FIX: Load connection string securely from configuration!
// This checks appsettings.json for "DefaultConnection", but falls back to your local string if missing.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Port=3306;Database=HealthcareRecruitmentDb;User=root;Password=Abayomi10_;AllowUserVariables=True;";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// The Blazor WASM client runs on a different origin during development.
builder.Services.AddCors(options => options.AddPolicy("Client", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("ClientOrigins").Get<string[]>() ?? [])
          .AllowAnyHeader()
          .AllowAnyMethod()));

// Vacancy aggregation: options, resilient HTTP clients (retry/backoff), background poller.
builder.Services.Configure<AdzunaOptions>(builder.Configuration.GetSection("Adzuna"));
builder.Services.Configure<CareerjetOptions>(builder.Configuration.GetSection("Careerjet"));
builder.Services.Configure<JoobleOptions>(builder.Configuration.GetSection("Jooble"));
builder.Services.Configure<SampleDataOptions>(builder.Configuration.GetSection("SampleData"));
builder.Services.Configure<AggregationOptions>(builder.Configuration.GetSection("Aggregation"));

builder.Services.AddHttpClient<AdzunaSource>().AddStandardResilienceHandler();
builder.Services.AddHttpClient<CareerjetSource>().AddStandardResilienceHandler();
builder.Services.AddHttpClient<JoobleSource>().AddStandardResilienceHandler();
builder.Services.AddSingleton<SampleDataSource>();

builder.Services.AddTransient<IVacancySource>(sp => sp.GetRequiredService<AdzunaSource>());
builder.Services.AddTransient<IVacancySource>(sp => sp.GetRequiredService<CareerjetSource>());
builder.Services.AddTransient<IVacancySource>(sp => sp.GetRequiredService<JoobleSource>());
builder.Services.AddTransient<IVacancySource>(sp => sp.GetRequiredService<SampleDataSource>());

//  ESSENTIAL FIX: Automatically starts your Job Fetcher in the background
builder.Services.AddHostedService<VacancyAggregationService>();

// AI provider: OpenRouter and Local Ollama Embeddings for RAG.
builder.Services.Configure<OpenRouterOptions>(builder.Configuration.GetSection("OpenRouter"));
builder.Services.AddHttpClient<OpenRouterClient>().AddStandardResilienceHandler();
builder.Services.AddTransient<IAiJsonClient>(sp => sp.GetRequiredService<OpenRouterClient>());
builder.Services.AddHttpClient<OllamaEmbeddingService>();

// Resume-driven AI Career Assistant: summarizes an uploaded resume, recommends vacancies from
// it, and generates personalized interview prep — all grounded in the signed-in user's own data.
builder.Services.AddScoped<AiJsonRunner>();
builder.Services.AddScoped<AiAssistantService>();

// Auth: JWT bearer tokens for the account-backed features (saved jobs, applications, AI assistant).
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<TokenService>();

// Email: verification codes + (later) password resets, sent via Brevo's HTTPS API rather than
// SMTP — see BrevoOptions for why. Logs instead of sending when unconfigured.
builder.Services.Configure<BrevoOptions>(builder.Configuration.GetSection("Brevo"));
builder.Services.AddHttpClient<BrevoEmailSender>();
builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<BrevoEmailSender>());

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration section is missing.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Apply pending migrations on startup so the database is always ready.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("Client");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();