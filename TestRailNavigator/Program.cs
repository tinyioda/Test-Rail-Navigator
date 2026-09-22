using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TestRailNavigator.Data;
using TestRailNavigator.Services;

SQLitePCL.Batteries_V2.Init();

var builder = WebApplication.CreateBuilder(args);

// IIS hosting support
builder.Services.Configure<IISServerOptions>(options =>
{
    options.AutomaticAuthentication = false;
});

// Persist the Data Protection keyring outside the app content root so antiforgery tokens
// and authentication cookies survive app-pool recycles (i.e. every deploy). Without this, every
// redeploy regenerates the keyring and every logged-in browser tab gets HTTP 400 on its
// next POST because its cached __RequestVerificationToken can no longer be decrypted.
var dpKeysPath = builder.Configuration["DataProtection:KeysPath"] ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "TestRailNavigator", "dp-keys");
Directory.CreateDirectory(dpKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("TestRailNavigator")
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
    options.Conventions.ConfigureFilter(new ResponseCacheAttribute
    {
        Location = ResponseCacheLocation.None,
        NoStore = true
    });
});
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddHealthChecks();
builder.Services.AddScoped<AdminAuthenticationService>();
builder.Services.AddScoped<AdminCookieEvents>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.Cookie.Name = "TestRailNavigator.Admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.EventsType = typeof(AdminCookieEvents);
    });
builder.Services.AddAuthorization(options =>
{
    var administratorPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole(AdminAuthenticationService.AdministratorRole)
        .Build();
    options.AddPolicy(AdminAuthenticationService.AdministratorPolicy, administratorPolicy);
    options.FallbackPolicy = administratorPolicy;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AdminAuthenticationService.LoginRateLimitPolicy, context =>
        HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                })
            : RateLimitPartition.GetNoLimiter("login-page"));
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
        await context.HttpContext.Response.WriteAsync(
            "Too many sign-in attempts. Wait a minute and try again.", cancellationToken);
    };
});
builder.Services.AddHttpClient<TestRailClient>();
builder.Services.AddHttpClient<AzureDevOpsService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<ICaseEnrichmentService, OpenAiCompatibleEnrichmentService>(c =>
{
    // Chat completions can take a while for larger stories; 2 minutes is a reasonable upper bound
    // for a single AC -> structured case round trip.
    c.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddScoped<IIssueTrackerClient>(sp => sp.GetRequiredService<AzureDevOpsService>());
builder.Services.AddScoped<HierarchyGenerator>();
builder.Services.AddSingleton<ConsoleLogService>();
builder.Services.AddScoped<PermissionService>();

// Encrypted SQLite database
// Read the password once at startup directly from the settings file to avoid building
// a throwaway service provider (which would fragment singletons).
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "testrailnavigator.db");
var dbPassword = string.Empty;
var settingsPath = Path.Combine(builder.Environment.ContentRootPath, "testrail-settings.json");
if (File.Exists(settingsPath))
{
    try
    {
        await using var stream = File.OpenRead(settingsPath);
        var loaded = await System.Text.Json.JsonSerializer.DeserializeAsync<TestRailSettings>(stream);
        dbPassword = loaded?.DatabasePassword ?? string.Empty;
    }
    catch
    {
        // Settings file may be malformed; fall back to an unencrypted DB.
        dbPassword = string.Empty;
    }
}

var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
    Password = dbPassword
}.ToString();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

var app = builder.Build();

// Ensure the encrypted database is created.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseRateLimiter();

// Recover gracefully from stale antiforgery tokens (e.g. from a browser tab open
// across an app restart before DP keys were persisted). Instead of returning a raw
// HTTP 400, redirect the browser back to the original URL so Razor Pages re-issues a
// fresh token. Safe because AntiforgeryValidationException only fires on unsafe
// (POST/PUT/DELETE) methods and the redirect is a plain GET.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.Redirect(context.Request.Path + context.Request.QueryString);
        }
    }
});

app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();
app.MapHealthChecks("/healthz").AllowAnonymous();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

/// <summary>Exposes the application entry point for in-process integration coverage.</summary>
public partial class Program { }
