using System.Text.Json;

namespace ACI.Storage;

/// <summary>
/// 基于文件系统的会话存储实现。
/// 每个会话快照存储为一个独立的 JSON 文件。
/// 文件名格式：{sessionId}.json
/// </summary>
public class FileSessionStore : ISessionStore
{
    private readonly string _basePath;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// 创建文件存储实例。
    /// </summary>
    /// <param name="basePath">存储目录路径。不存在时自动创建。</param>
    public FileSessionStore(string basePath)
    {
        _basePath = basePath;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// 保存会话快照到文件。
    /// 使用 write-then-rename 策略防止写入中断导致文件损坏。
    /// </summary>
    public async Task SaveAsync(SessionSnapshot snapshot, CancellationToken ct = default)
    {
        EnsureDirectory();

        var filePath = GetFilePath(snapshot.SessionId);
        var tempPath = filePath + ".tmp";

        try
        {
            // 先写入临时文件，确保 stream 在 Move 之前关闭
            {
                await using var stream = File.Create(tempPath);
                await JsonSerializer.SerializeAsync(stream, snapshot, _jsonOptions, ct);
                await stream.FlushAsync(ct);
            }

            // 原子性替换目标文件（stream 已关闭，不会文件锁冲突）
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            // 清理临时文件（静默失败）
            try { File.Delete(tempPath); } catch { /* ignore */ }
            throw;
        }
    }

    /// <summary>
    /// 从文件加载会话快照。
    /// </summary>
    public async Task<SessionSnapshot?> LoadAsync(string sessionId, CancellationToken ct = default)
    {
        var filePath = GetFilePath(sessionId);
        if (!File.Exists(filePath)) return null;

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<SessionSnapshot>(stream, _jsonOptions, ct);
        }
        catch (JsonException)
        {
            // 文件损坏，返回 null
            return null;
        }
    }

    /// <summary>
    /// 删除会话快照文件。
    /// </summary>
    public Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        var filePath = GetFilePath(sessionId);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 列出所有已保存的会话摘要。
    /// 通过读取每个文件的前几个字段来提取摘要信息，避免完整反序列化。
    /// </summary>
    public async Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct = default)
    {
        EnsureDirectory();

        var result = new List<SessionSummary>();
        var files = Directory.GetFiles(_basePath, "*.json");

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await using var stream = File.OpenRead(file);
                var snapshot = await JsonSerializer.DeserializeAsync<SessionSnapshot>(
                    stream, _jsonOptions, ct);

                if (snapshot != null)
                {
                    result.Add(new SessionSummary
                    {
                        SessionId = snapshot.SessionId,
                        CreatedAt = snapshot.CreatedAt,
                        SnapshotAt = snapshot.SnapshotAt,
                        AgentCount = snapshot.Agents.Count
                    });
                }
            }
            catch
            {
                // 跳过损坏的文件
            }
        }

        return result.OrderByDescending(s => s.SnapshotAt).ToList();
    }

    /// <summary>
    /// 判断会话快照文件是否存在。
    /// </summary>
    public Task<bool> ExistsAsync(string sessionId, CancellationToken ct = default)
    {
        return Task.FromResult(File.Exists(GetFilePath(sessionId)));
    }

    /// <summary>
    /// 获取指定会话 ID 对应的文件路径。
    /// </summary>
    private string GetFilePath(string sessionId)
        => Path.Combine(_basePath, $"{sessionId}.json");

    /// <summary>
    /// 确保存储目录存在。
    /// </summary>
    private void EnsureDirectory()
    {
        if (!Directory.Exists(_basePath))
        {
            Directory.CreateDirectory(_basePath);
        }
    }
}
