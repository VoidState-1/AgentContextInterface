using ContextUI.Core.Models;

namespace ContextUI.Core.Abstractions;

/// <summary>
/// 日志管理器接口 - 负责日志存储和生命周期
/// </summary>
public interface ILogManager
{
    /// <summary>
    /// 当前序列号
    /// </summary>
    int CurrentSeq { get; }

    /// <summary>
    /// 追加日志条目（接受任何子类）
    /// </summary>
    int Append(LogEntry entry);

    /// <summary>
    /// 获取日志（可按级别过滤）
    /// </summary>
    IEnumerable<LogEntry> GetLogs(int? maxLevel = null);

    /// <summary>
    /// 根据序列号获取日志
    /// </summary>
    LogEntry? GetBySeq(int seq);

    /// <summary>
    /// 手动触发压缩
    /// </summary>
    void Compact();

    /// <summary>
    /// 日志追加事件
    /// </summary>
    event Action<LogEntry>? OnAppended;
}
