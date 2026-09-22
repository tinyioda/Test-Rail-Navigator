using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;
using TestRailNavigator.Services;

namespace TestRailNavigator.Tests;

/// <summary>Isolates settings and generated runtime data from the developer's configuration.</summary>
internal sealed class TestSettingsScope : IWebHostEnvironment, IDisposable
{
    /// <summary>Creates an isolated settings root.</summary>
    private TestSettingsScope()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "TestRailNavigator.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        Settings = new SettingsService(this);
    }

    /// <summary>Gets the isolated content root.</summary>
    public string RootPath { get; }

    /// <summary>Gets the settings service using the isolated root.</summary>
    public SettingsService Settings { get; }

    /// <inheritdoc />
    public string ApplicationName { get; set; } = "TestRailNavigator";

    /// <inheritdoc />
    public string EnvironmentName { get; set; } = "Production";

    /// <inheritdoc />
    public string ContentRootPath { get => RootPath; set => throw new NotSupportedException(); }

    /// <inheritdoc />
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    /// <inheritdoc />
    public string WebRootPath { get; set; } = string.Empty;

    /// <inheritdoc />
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

    /// <summary>Creates a fixture containing only the supplied dummy configuration.</summary>
    public static async Task<TestSettingsScope> CreateAsync(TestRailSettings settings)
    {
        var scope = new TestSettingsScope();
        await scope.Settings.SaveSettingsAsync(settings);
        return scope;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(RootPath, recursive: true);
    }
}
