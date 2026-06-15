using System.Collections.ObjectModel;
using TrimKit.Models;

namespace TrimKit.Services;

public class LogService : ILogService
{
    private readonly List<OperationLog> _logs = [];

    public IReadOnlyList<OperationLog> Logs => _logs.AsReadOnly();
    public event Action<OperationLog>? LogAdded;

    public void Log(LogLevel level, string message, string? details = null)
    {
        var entry = new OperationLog
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Details = details
        };

        _logs.Add(entry);
        LogAdded?.Invoke(entry);
    }

    public void Clear()
    {
        _logs.Clear();
    }
}
