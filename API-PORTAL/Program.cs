using API_PORTAL.Auth;
using API_PORTAL.Configurations;
using API_PORTAL.Data;
using API_PORTAL.Email;
using API_PORTAL.Entities;
using API_PORTAL.Errors;
using API_PORTAL.Integration;
using API_PORTAL.Reporting;
using API_PORTAL.Seed;
using API_PORTAL.Services;
using API_PORTAL.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shared.Reporting;


if (args is ["--install-pdf-browser"])
{
    string[] installArguments = OperatingSystem.IsLinux()
        ? ["install", "--with-deps", "--only-shell", "chromium"]
        : ["install", "--only-shell", "chromium"];

    Environment.ExitCode = Microsoft.Playwright.Program.Main(installArguments);
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
});

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("api-portal");

if (!builder.Environment.IsEnvironment("Testing"))
{
    var dataProtectionKeysPath = builder.Configuration[
        "DataProtection:KeysPath"] ?? Path.Combine(
            builder.Environment.ContentRootPath,
            ".keys");

    dataProtection.PersistKeysToFileSystem(
        new DirectoryInfo(dataProtectionKeysPath));
}

builder.Services.AddScoped<
    ProblemDetailsResultFilter>();

builder.Services
    .AddControllersWithViews(options =>
    {
        options.Filters.AddService<ProblemDetailsResultFilter>();
    })
    .ConfigureApiBehaviorOptions(
        ApiBehaviorConfiguration.Configure)
    .AddJsonOptions(ApiJsonConfiguration.Configure);

builder.Services.AddProblemDetails(
    ProblemDetailsConfiguration.Configure);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference =
                        new OpenApiReference
                        {
                            Type =
                                ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                },
                Array.Empty<string>()
            }
        });
});


var connectionString =
    builder.Configuration.GetConnectionString("PortalDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'PortalDatabase' was not found.");

var dataSource = PortalDataSourceFactory.Create(connectionString);

builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContextPool<PortalDbContext>(options =>
{
    options.UseNpgsql(
        dataSource,
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                "portal");
        });
});

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(
        builder.Configuration.GetSection(
            JwtOptions.SectionName))
    .Validate(options =>
        !string.IsNullOrWhiteSpace(options.Issuer) &&
        !string.IsNullOrWhiteSpace(options.Audience) &&
        !string.IsNullOrWhiteSpace(options.SigningKey) &&
        Encoding.UTF8.GetByteCount(
            options.SigningKey) >= 32,
        "Configurația JWT a Portalului este invalidă.")
    .ValidateOnStart();

builder.Services
    .AddOptions<DmsIntegrationOptions>()
    .Bind(builder.Configuration.GetSection(
        DmsIntegrationOptions.SectionName))
    .Validate(options =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(options.SharedSecret) &&
        options.SharedSecret.Length >= 32,
        "Configurația integrării Portal → DMS este invalidă.")
    .ValidateOnStart();

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<TracePropagationHandler>();

builder.Services.AddHttpClient<
    IDmsRegistryTypeClient,
    DmsRegistryTypeClient>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<DmsIntegrationOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .AddHttpMessageHandler<TracePropagationHandler>();

builder.Services.AddHttpClient<
    IDmsSubmissionRegistrationClient,
    DmsSubmissionRegistrationClient>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<DmsIntegrationOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<TracePropagationHandler>();

builder.Services.AddHttpClient<
    IDmsResponseDocumentClient,
    DmsResponseDocumentClient>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<DmsIntegrationOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(15);
    })
    .AddHttpMessageHandler<TracePropagationHandler>();

builder.Services.AddHttpClient<
    IDmsRegistrationReceiptClient,
    DmsRegistrationReceiptClient>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<DmsIntegrationOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<TracePropagationHandler>();

builder.Services.AddHttpClient<
    IDmsClarificationClient,
    DmsClarificationClient>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<DmsIntegrationOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<TracePropagationHandler>();

builder.Services.AddHttpClient<
    IDmsCancellationClient,
    DmsCancellationClient>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<DmsIntegrationOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(15);
    })
    .AddHttpMessageHandler<TracePropagationHandler>();

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<PortalOutboxDispatcher>();
}

var jwtIssuer =
    builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException(
        "Jwt:Issuer nu este configurat.");

var jwtAudience =
    builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException(
        "Jwt:Audience nu este configurat.");

var jwtSigningKey =
    builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException(
        "Jwt:SigningKey nu este configurat.");

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,

                ValidateAudience = true,
                ValidAudience = jwtAudience,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtSigningKey)),

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),

                NameClaimType =
                    JwtRegisteredClaimNames.Email,

                RoleClaimType =
                    ClaimTypes.Role
            };
    });

builder.Services.AddAuthorization();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";

    options.Cookie.Name =
        "__Host-portal-csrf";

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy =
        CookieSecurePolicy.Always;
    options.Cookie.SameSite =
        SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>>();

builder.Services.AddScoped<
    IJwtTokenService,
    JwtTokenService>();

builder.Services.AddSingleton<ITotpService, TotpService>();

builder.Services.AddSingleton<
    IAccountTokenService,
    AccountTokenService>();

builder.Services
    .AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(
        EmailOptions.SectionName));

builder.Services.AddScoped<
    IEmailSender,
    SmtpEmailSender>();

builder.Services.AddScoped<
    IAccountEmailService,
    AccountEmailService>();

builder.Services.AddSingleton<
    IServiceHtmlSanitizer,
    ServiceHtmlSanitizer>();

builder.Services.AddSingleton<
    ServiceFormSchemaValidator>();

builder.Services.AddSingleton<
    ServiceFormValueValidator>();

builder.Services.Configure<SubmissionFileStorageOptions>(
    builder.Configuration.GetSection("FileStorage"));

builder.Services
    .AddOptions<SubmissionFileDownloadOptions>()
    .Bind(builder.Configuration.GetSection(
        SubmissionFileDownloadOptions.SectionName))
    .Validate(options =>
        !string.IsNullOrWhiteSpace(options.SigningKey) &&
        Encoding.UTF8.GetByteCount(options.SigningKey) >= 32 &&
        options.LifetimeMinutes is >= 1 and <= 60,
        "Configurația URL-urilor de descărcare este invalidă.")
    .ValidateOnStart();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit =
        SubmissionFileStorageService.MaxSubmissionRequestSize;
});

builder.Services.AddSingleton<SubmissionFileContentDetector>();
builder.Services.AddSingleton<SubmissionFileStorageService>();
builder.Services.AddSingleton<SubmissionFileDownloadTokenService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPdfRenderer, ChromiumPdfRenderer>();
builder.Services.AddSingleton<PortalReportDefinitionValidator>();
builder.Services.AddScoped<PortalReportPreviewService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:3000",
                    "http://localhost:3001",
                    "http://localhost:3002")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

builder.Services.AddScoped<
    IRefreshSessionLock,
    PostgresRefreshSessionLock>();

builder.Services.ConfigureOptions<ForwardedHeadersConfiguration>();

var app = builder.Build();

app.UseForwardedHeaders();

app.UseMiddleware<TraceIdMiddleware>();

if (app.Environment.IsEnvironment("Testing"))
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages(async statusContext =>
{
    var httpContext =
        statusContext.HttpContext;

    var response =
        httpContext.Response;

    var problem =
        ApiProblemDetails.Create(
            httpContext,
            response.StatusCode,
            ApiProblemDetails.TitleForStatus(
                response.StatusCode),
            "Resursa solicitată nu a putut fi procesată.");

    response.ContentType =
        "application/problem+json; charset=utf-8";

    var json =
        JsonSerializer.Serialize(problem);

    await response.WriteAsync(
        json,
        System.Text.Encoding.UTF8,
        httpContext.RequestAborted);
});

if (app.Environment.IsDevelopment() ||
    app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWhen(
    context =>
        !context.Request.Path.StartsWithSegments("/api/internal") &&
        !context.Request.Path.StartsWithSegments("/api/callbacks"),
    branch => branch.UseHttpsRedirection());
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider
        .GetRequiredService<PortalDbContext>();

    await db.Database.MigrateAsync();

    await PortalDbSeeder.SeedAsync(
        scope.ServiceProvider);
}

app.Run();

public partial class Program
{
}
