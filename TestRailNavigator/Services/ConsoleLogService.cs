using System.Collections.Concurrent;

namespace TestRailNavigator.Services;

/// <summary>
/// Service for collecting and retrieving console log messages.
/// </summary>
public class ConsoleLogService
{
    private readonly ConcurrentQueue<string> _messages = new();

    public void Log(string message)
    {
        _messages.Enqueue($"{DateTime.Now:HH:mm:ss} {message}");
        while (_messages.Count > 100)
            _messages.TryDequeue(out _);
    }

    public IReadOnlyCollection<string> GetMessages() => _messages.ToArray();
}
