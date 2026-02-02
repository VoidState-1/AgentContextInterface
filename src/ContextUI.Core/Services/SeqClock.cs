using ContextUI.Core.Abstractions;

namespace ContextUI.Core.Services;

/// <summary>
/// 逻辑时钟实现
/// </summary>
public class SeqClock : ISeqClock
{
    private int _seq = 0;

    /// <summary>
    /// 当前序列号
    /// </summary>
    public int CurrentSeq => _seq;

    /// <summary>
    /// 获取下一个序列号
    /// </summary>
    public int Next() => ++_seq;
}
