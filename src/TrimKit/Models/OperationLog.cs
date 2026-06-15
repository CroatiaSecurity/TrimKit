namespace TrimKit.Models;

public class OperationLog
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public enum LogLevel
{
    Info,
    Warning,
    Error,
    Success
}
