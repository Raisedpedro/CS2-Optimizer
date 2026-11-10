using System.Text;
using CS2Optimizer.Core.Models;

namespace CS2Optimizer.Core.Services;

public sealed class AppLogger
{
    private readonly object _gate = new();
    private readonly List<LogEntry> _entries = new();

    public AppLogger(string logFile)
    {
        LogFile = logFile;
        try
        {
            var dir = System.IO.Path.GetDirectoryName(logFile);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        }
        catch { }
    }

    public string LogFile { get; }

    public event Action<LogEntry>? EntryAdded;

    public IReadOnlyList<LogEntry> Entries
    {
        get { lock (_gate) return _entries.ToArray(); }
    }

    public void Log(string text, LogLevel level = LogLevel.Info)
    {
        var entry = new LogEntry(DateTime.Now, text, level);
        lock (_gate)
        {
            _entries.Add(entry);
            try
            {
                File.AppendAllText(LogFile,
                    $"{entry.Time:HH:mm:ss}  {text}{Environment.NewLine}", Encoding.UTF8);
            }
            catch { }
        }

        EntryAdded?.Invoke(entry);
    }

    public void Title(string text) => Log(text, LogLevel.Title);
    public void Ok(string text) => Log(text, LogLevel.Ok);
    public void Warn(string text) => Log(text, LogLevel.Warn);
    public void Bad(string text) => Log(text, LogLevel.Bad);
}
