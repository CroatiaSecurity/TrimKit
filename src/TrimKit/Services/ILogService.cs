using TrimKit.Models;

namespace TrimKit.Services;

public interface ILogService
{
    void Log(LogLevel level, string message, string? details = null);
    IReadOnlyList<OperationLog> Logs { get; }
    event Action<OperationLog>? LogAdded;
    void Clear();
}
