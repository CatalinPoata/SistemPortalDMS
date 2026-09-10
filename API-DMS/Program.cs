using API_DMS.Auth;
using API_DMS.Configurations;
using API_DMS.Data;
using API_DMS.Email;
using API_DMS.Entities;
using API_DMS.Errors;
using API_DMS.Integration;
using API_DMS.Filters;
using API_DMS.Reports;
using API_DMS.Seed;
using API_DMS.Services;
using API_DMS.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Shared.Reporting;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

if (args is ["--install-pdf-browser"])
{
    string[] installArguments = OperatingSystem.IsLinux()
        ? ["install", "--with-deps", "--only-shell", "chromium"]
        : ["install", "--only-shell", "chromium"];

    Environment.ExitCode =
        Microsoft.Playwright.Program.Main(installArguments);

    return;
}


var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Token JWT pentru autentificare."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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

var connectionString =
    builder.Configuration.GetConnectionString("DmsDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'DmsDatabase' was not found.");

var dataSource = DmsDataSourceFactory.Create(connectionString);

builder.Services.AddSingleton(dataSource);

builder.Services.AddDbContextPool<DmsDbContext>(options =>
{
    options.UseNpgsql(
        dataSource,
        npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable(
                "__EFMigrationsHistory",
                "dms");
        });
});

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(
        JwtOptions.SectionName))
    .Validate(options =>
        !string.IsNullOrWhiteSpace(options.Issuer) &&
        !string.IsNullOrWhiteSpace(options.Audience) &&
        !string.IsNullOrWhiteSpace(options.SigningKey) &&
        Encoding.UTF8.GetByteCount(options.SigningKey) >= 32,
        "Configurația JWT este invalidă.")
    .ValidateOnStart();

builder.Services
    .AddOptions<PortalIntegrationOptions>()
    .Bind(builder.Configuration.GetSection(
        PortalIntegrationOptions.SectionName))
    .Validate(options =>
        Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) &&
        !string.IsNullOrWhiteSpace(options.SharedSecret) &&
        options.SharedSecret.Length >= 32 &&
        !string.IsNullOrWhiteSpace(options.ActorEmail),
        "Configurația integrării Portal → DMS este invalidă.")
    .ValidateOnStart();

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<TracePropagationHandler>();

builder.Services.AddHttpClient<
    IPortalSubmissionFileClient,
    PortalSubmissionFileClient>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<
                Microsoft.Extensions.Options.IOptions<PortalIntegrationOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<TracePropagationHandler>();

builder.Services.AddHttpClient<
    IPortalRegistryEventCallbackClient,
    PortalRegistryEventCallbackClient>((serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<
                Microsoft.Extensions.Options.IOptions<PortalIntegrationOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(15);
    })
    .AddHttpMessageHandler<TracePropagationHandler>();

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<DmsOutboxDispatcher>();
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

builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>>();

builder.Services.AddScoped<
    IJwtTokenService,
    JwtTokenService>();

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("api-dms");

if (!builder.Environment.IsEnvironment("Testing"))
{
    var dataProtectionKeysPath = builder.Configuration[
        "DataProtection:KeysPath"] ?? Path.Combine(
            builder.Environment.ContentRootPath,
            ".keys");

    dataProtection.PersistKeysToFileSystem(
        new DirectoryInfo(dataProtectionKeysPath));
}
builder.Services.AddSingleton<ITotpService, TotpService>();

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
        "__Host-dms-csrf";

    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy =
        CookieSecurePolicy.Always;
    options.Cookie.SameSite =
        SameSiteMode.Strict;
    options.Cookie.Path = "/";
    options.Cookie.IsEssential = true;
});

builder.Services.AddScoped<RegistryNumberAllocator>();
builder.Services.AddScoped<RegistryEntryWorkflowService>();

builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection("FileStorage"));

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit =
        FileStorageService.MaxUploadRequestSize;
});

builder.Services.AddSingleton<FileContentDetector>();
builder.Services.AddSingleton<FileStorageService>();

builder.Services.AddScoped<
    UploadSizeFilter>();

builder.Services.AddScoped<RegistryTaskService>();

builder.Services
    .AddOptions<FileDownloadOptions>()
    .Bind(builder.Configuration.GetSection(
        FileDownloadOptions.SectionName))
    .Validate(options =>
        !string.IsNullOrWhiteSpace(options.SigningKey) &&
        Encoding.UTF8.GetByteCount(options.SigningKey) >= 32,
        "FileDownload:SigningKey trebuie să aibă minimum 32 bytes.")
    .Validate(options =>
        options.LifetimeMinutes > 0 &&
        options.LifetimeMinutes <= 15,
        "Durata URL-ului nu poate depăși 15 minute.")
    .Validate(options =>
        string.IsNullOrWhiteSpace(options.PublicBaseUrl) ||
        Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out _),
        "FileDownload:PublicBaseUrl trebuie să fie un URL absolut.")
    .ValidateOnStart();

builder.Services.AddSingleton<FileDownloadTokenService>();

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

builder.Services.AddSingleton(
    TimeProvider.System);

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
builder.Services.AddSingleton<
    IPdfRenderer,
    ChromiumPdfRenderer>();

builder.Services.AddSingleton<
    DmsReportDefinitionValidator>();

builder.Services.AddScoped<DmsReportPreviewService>();


var app = builder.Build();

app.UseForwardedHeaders();

app.UseMiddleware<TraceIdMiddleware>();

app.UseExceptionHandler();

app.UseStatusCodePages(async statusContext =>
{
    var httpContext = statusContext.HttpContext;
    var response = httpContext.Response;

    var problem = ApiProblemDetails.Create(
        httpContext,
        response.StatusCode,
        ApiProblemDetails.TitleForStatus(
            response.StatusCode),
        "Resursa solicitată nu a putut fi procesată.");

    response.ContentType =
        "application/problem+json; charset=utf-8";

    var json = JsonSerializer.Serialize(problem);

    await response.WriteAsync(
        json,
        System.Text.Encoding.UTF8,
        httpContext.RequestAborted);
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWhen(
    context =>
        !context.Request.Path.StartsWithSegments("/api/integration") &&
        !context.Request.Path.StartsWithSegments("/api/internal"),
    branch => branch.UseHttpsRedirection());
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapControllers();

if (app.Environment.IsDevelopment())
{
    await using var scope =
        app.Services.CreateAsyncScope();

    var db = scope.ServiceProvider
        .GetRequiredService<DmsDbContext>();

    await db.Database.MigrateAsync();

    await DmsDbSeeder.SeedAsync(
        scope.ServiceProvider);
}

app.Run();

public partial class Program
{
}
