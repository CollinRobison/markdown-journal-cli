namespace markdown_journal_cli.Tests.Infrastructure.FileSystem;

/// <summary>
/// Identifies write-path methods where faults can be injected.
/// Using an enum (rather than strings) keeps injections refactor-safe.
/// </summary>
public enum FaultInjectPoint
{
    CreateFile,
    CreateMarkdownFile,
    UpdateFile,
    DeleteFile,
    DeleteDirectory,
    RenameFile,
}

/// <summary>
/// A <see cref="TestFileSystem"/> wrapper that throws a configured exception on the
/// <em>Nth</em> invocation of a named write method, enabling deterministic fault injection
/// in rollback tests.
/// </summary>
public sealed class FaultInjectingFileSystem : TestFileSystem
{
    private readonly record struct FaultSpec(FaultInjectPoint Point, int OnCallNumber, Exception Exception);

    private readonly List<FaultSpec> _faults = [];
    private readonly Dictionary<FaultInjectPoint, int> _callCounts = [];

    public void InjectFaultOn(FaultInjectPoint point, int onCallNumber, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        _faults.Add(new FaultSpec(point, onCallNumber, ex));
    }

    public override void CreateFile(string path, string fileName, string body)
    {
        MaybeThrow(FaultInjectPoint.CreateFile);
        base.CreateFile(path, fileName, body);
    }

    public override void CreateMarkdownFile(string path, string fileName, string body)
    {
        MaybeThrow(FaultInjectPoint.CreateMarkdownFile);
        base.CreateMarkdownFile(path, fileName, body);
    }

    public override void UpdateFile(string path, string fileName, string body)
    {
        MaybeThrow(FaultInjectPoint.UpdateFile);
        base.UpdateFile(path, fileName, body);
    }

    public override void DeleteFile(string filePath)
    {
        MaybeThrow(FaultInjectPoint.DeleteFile);
        base.DeleteFile(filePath);
    }

    public override void DeleteDirectory(string path)
    {
        MaybeThrow(FaultInjectPoint.DeleteDirectory);
        base.DeleteDirectory(path);
    }

    public override void RenameFile(string oldPath, string newPath)
    {
        MaybeThrow(FaultInjectPoint.RenameFile);
        base.RenameFile(oldPath, newPath);
    }

    /// <summary>Resets all call counters so subsequent InjectFaultOn calls count from 1 again.</summary>
    public void ResetCallCounts() => _callCounts.Clear();

    private void MaybeThrow(FaultInjectPoint point)
    {
        _callCounts.TryGetValue(point, out var count);
        count++;
        _callCounts[point] = count;

        foreach (var spec in _faults)
        {
            if (spec.Point == point && spec.OnCallNumber == count)
                throw spec.Exception;
        }
    }
}
