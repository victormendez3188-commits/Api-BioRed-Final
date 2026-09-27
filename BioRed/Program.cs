using System.Security.Claims;
using BioRed.Application.Security;
using BioRed.Infrastructure;
using BioRed.Infrastructure.Security;
using BioRed.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using BioRed.OpenApi;
using BioRed.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using BioRed.Infrastructure.Payments;
using BioRed.Middleware;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

const string FrontendCorsPolicy = "Frontend";
const string AuthenticationRateLimitPolicy = "authentication";
const string RegistrationRateLimitPolicy = "registration";

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<AuthOperationTransformer>();
});

// Configuración de Entity Framework Core y SQL Server
var connectionString =
    builder.Configuration.GetConnectionString("BioRedConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "No se encontró la cadena de conexión BioRedConnection. " +
        "Configúrela con User Secrets o una variable de entorno.");
}

builder.Services.AddInfrastructure(connectionString);
builder.Services
    .AddOptions<PayPalOptions>()
    .Bind(builder.Configuration.GetSection(PayPalOptions.SectionName));
builder.Services
    .AddOptions<ReceiptEmailOptions>()
    .Bind(builder.Configuration.GetSection(ReceiptEmailOptions.SectionName));
builder.Services
    .AddOptions<AdminSeedOptions>()
    .Bind(
        builder.Configuration.GetSection(
            AdminSeedOptions.SectionName));

// Obtener la configuración JWT desde appsettings.json y User Secrets
var jwtSection =
    builder.Configuration.GetSection(JwtOptions.SectionName);

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(jwtSection)
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Key),
        "No se encontró la clave secreta Jwt:Key.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Issuer),
        "Jwt:Issuer es obligatorio.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Audience),
        "Jwt:Audience es obligatorio.")
    .Validate(
        options => options.AccessTokenMinutes > 0,
        "Jwt:AccessTokenMinutes debe ser mayor que cero.")
    .Validate(
        options => options.RefreshTokenDays > 0,
        "Jwt:RefreshTokenDays debe ser mayor que cero.")
    .ValidateOnStart();

var jwtOptions =
    jwtSection.Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "No se encontró la configuración JWT.");

byte[] signingKey;

try
{
    signingKey = Convert.FromBase64String(jwtOptions.Key);
}
catch (FormatException exception)
{
    throw new InvalidOperationException(
        "La configuración Jwt:Key no contiene una clave Base64 válida.",
        exception);
}

if (signingKey.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key debe contener como mínimo 32 bytes.");
}

// Configuración de autenticación JWT
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.SaveToken = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,

            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,

            ValidAlgorithms = new[]
            {
                SecurityAlgorithms.HmacSha256
            }
        };
    });

builder.Services.AddSingleton<ITokenService>(_ =>
    new JwtTokenService(
        signingKey,
        jwtOptions.Issuer,
        jwtOptions.Audience,
        jwtOptions.AccessTokenMinutes));

builder.Services.AddSingleton<IRefreshTokenService>(_ =>
    new RefreshTokenService(
        jwtOptions.RefreshTokenDays));

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? Array.Empty<string>();

allowedOrigins = allowedOrigins
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

var apiDocumentationEnabled = builder.Configuration
    .GetValue("ApiDocumentation:Enabled", false);

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        if (allowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin();
        }
        else if (allowedOrigins.Length == 0)
        {
            policy.SetIsOriginAllowed(_ => false);
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(
        AuthenticationRateLimitPolicy,
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy(
        RegistrationRateLimitPolicy,
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(10)
            }));
});

builder.Services.AddSingleton<
    IAuthorizationPolicyProvider,
    PermissionPolicyProvider>();

builder.Services.AddSingleton<
    IAuthorizationHandler,
    PermissionAuthorizationHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    var adminSeeder =
        scope.ServiceProvider
            .GetRequiredService<AdminSeeder>();

    await adminSeeder.SeedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
    app.UseHsts();

    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        await next();
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors(FrontendCorsPolicy);

// El orden es importante
app.UseAuthentication();
app.UseMiddleware<ApiAuditMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

if (apiDocumentationEnabled)
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();

app.Run();
