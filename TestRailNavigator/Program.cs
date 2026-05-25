using Microsoft.AspNetCore.DataProtection;
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
// and session cookies survive app-pool recycles (i.e. every deploy). Without this, every
// redeploy regenerates the keyring and every logged-in browser tab gets HTTP 400 on its
// next POST because its cached __RequestVerificationToken can no longer be decrypted.
var dpKeysPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "TestRailNavigator", "dp-keys");
Directory.CreateDirectory(dpKeysPath);
builder.Services.AddDataProtection()
    .SetApplicationName("TestRailNavigator")
    .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddHttpClient<TestRailClient>();
builder.Services.AddHttpClient<AzureDevOpsService>();
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
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

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

app.UseSession();

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

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
