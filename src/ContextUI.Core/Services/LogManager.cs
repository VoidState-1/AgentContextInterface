using ContextUI.Core.Abstractions;
using ContextUI.Core.Models;

namespace ContextUI.Core.Services;

/// <summary>
/// 日志管理器配置
/// </summary>
public class LogManagerConfig
{
    /// <summary>
    /// Level 1 最大数量（完整日志）
    /// </summary>
    public int Level1MaxCount { get; init; } = 10;

    /// <summary>
    /// Level 2 最大数量（摘要日志）
    /// </summary>
    public int Level2MaxCount { get; init; } = 30;

    /// <summary>
    /// Level 3 最大数量（简要日志）
    /// </summary>
    public int Level3MaxCount { get; init; } = 100;

    /// <summary>
    /// 缓冲区最大容量
    /// </summary>
    public int MaxBufferSize { get; init; } = 200;
}

/// <summary>
/// 日志管理器实现
/// </summary>
public class LogManager : ILogManager
{
    private readonly List<LogEntry> _logs = [];
    private readonly LogManagerConfig _config;
    private int _seq = 0;

    public LogManager() : this(new LogManagerConfig()) { }

    public LogManager(LogManagerConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// 当前序列号
    /// </summary>
    public int CurrentSeq => _seq;

    /// <summary>
    /// 日志追加事件
    /// </summary>
    public event Action<LogEntry>? OnAppended;

    /// <summary>
    /// 追加日志条目
    /// </summary>
    public int Append(LogEntry entry)
    {
        entry.Seq = ++_seq;
        _logs.Add(entry);

        // 检查是否需要压缩
        if (_logs.Count > _config.MaxBufferSize)
        {
            Compact();
        }

        UpdateLevels();
        OnAppended?.Invoke(entry);

        return entry.Seq;
    }

    /// <summary>
    /// 获取日志
    /// </summary>
    public IEnumerable<LogEntry> GetLogs(int? maxLevel = null)
    {
        if (maxLevel == null)
        {
            return _logs.AsReadOnly();
        }
        return _logs.Where(l => l.Level <= maxLevel.Value);
    }

    /// <summary>
    /// 根据序列号获取日志
    /// </summary>
    public LogEntry? GetBySeq(int seq)
    {
        return _logs.FirstOrDefault(l => l.Seq == seq);
    }

    /// <summary>
    /// 压缩日志缓冲区
    /// </summary>
    public void Compact()
    {
        while (_logs.Count > _config.MaxBufferSize)
        {
            // 找到最早的非永久日志
            var oldest = _logs.FirstOrDefault(l => !l.IsPersistent);
            if (oldest == null) break;

            _logs.Remove(oldest);
        }
    }

    /// <summary>
    /// 更新日志级别
    /// </summary>
    private void UpdateLevels()
    {
        var count = _logs.Count;
        for (int i = 0; i < count; i++)
        {
            var reverseIndex = count - 1 - i;  // 从最新到最旧
            var log = _logs[reverseIndex];

            if (i < _config.Level1MaxCount)
            {
                log.Level = 1;
            }
            else if (i < _config.Level2MaxCount)
            {
                log.Level = 2;
            }
            else if (i < _config.Level3MaxCount)
            {
                log.Level = 3;
            }
            else
            {
                log.Level = 4;
            }
        }
    }
}
