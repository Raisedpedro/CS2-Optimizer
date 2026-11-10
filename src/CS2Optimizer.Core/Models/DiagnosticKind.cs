namespace CS2Optimizer.Core.Models;

public enum DiagnosticKind
{
    Section,
    Info,
    Ok,
    Warn,
    Bad,
    Todo
}

public sealed record DiagnosticEntry(DiagnosticKind Kind, string Text);

public enum LogLevel
{
    Info,
    Ok,
    Warn,
    Bad,
    Title
}

public sealed record LogEntry(DateTime Time, string Text, LogLevel Level);
