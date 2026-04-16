namespace TestRailNavigator.Services;

/// <summary>
/// Service for collecting and retrieving console log messages.
/// Thread-safe ring buffer of the most recent <see cref="MaxMessages"/> entries.
/// </summary>
public class ConsoleLogService
{
    /// <summary>Maximum number of messages retained.</summary>
    public const int MaxMessages = 100;

    private readonly Lock _gate = new();
    private readonly Queue<string> _messages = new(MaxMessages);

    /// <summary>
    /// Appends a timestamped message, dropping the oldest when the buffer is full.
    /// </summary>
    public void Log(string message)
    {
        var line = $"{DateTimeOffset.Now:HH:mm:ss} {message}";
        lock (_gate)
        {
            if (_messages.Count >= MaxMessages)
            {
                _messages.Dequeue();
            }

            _messages.Enqueue(line);
        }
    }

    /// <summary>Returns a snapshot of the current messages.</summary>
    public IReadOnlyCollection<string> GetMessages()
    {
        lock (_gate)
        {
            return _messages.ToArray();
        }
    }
}
