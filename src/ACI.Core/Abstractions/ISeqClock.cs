namespace ACI.Core.Abstractions;

/// <summary>
/// 逻辑时钟接口 - 提供全局递增的序列号
/// </summary>
public interface ISeqClock
{
    /// <summary>
    /// 当前序列号
    /// </summary>
    int CurrentSeq { get; }

    /// <summary>
    /// 获取下一个序列号
    /// </summary>
    int Next();
}
