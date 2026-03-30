using markdown_journal_cli.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging;

namespace markdown_journal_cli.Infrastructure.Transactions;

/// <summary>
/// Default implementation of <see cref="IFileTransactionCoordinator"/>.
/// Maintains a thread-local ambient scope and creates <c>FileTransactionScope</c> instances backed
/// by <see cref="IInMemoryFileBuffer"/>, <see cref="IDeletionRollbackStrategy"/>, and
/// the supplied <see cref="ILoggerFactory"/>.
/// </summary>
public sealed class FileTransactionCoordinator(
    IFileSystem fileSystem,
    IInMemoryFileBuffer buffer,
    IDeletionRollbackStrategy deletionStrategy,
    ILoggerFactory loggerFactory
) : IFileTransactionCoordinator
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly IInMemoryFileBuffer _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    private readonly IDeletionRollbackStrategy _deletionStrategy = deletionStrategy ?? throw new ArgumentNullException(nameof(deletionStrategy));
    private readonly ILoggerFactory _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

    [ThreadStatic]
    private static IFileTransactionScope? _current;

    public IFileTransactionScope? Current => _current;

    public IFileTransactionScope Begin()
    {
        if (_current is { IsCommitted: false, IsRolledBack: false })
        {
            throw new InvalidOperationException(
                "A transaction scope is already active on this thread. Use BeginOrJoin() to participate in the existing scope."
            );
        }

        var scope = new FileTransactionScope(
            _fileSystem,
            _buffer,
            _deletionStrategy,
            _loggerFactory.CreateLogger<FileTransactionScope>(),
            onDisposed: () => _current = null
        );

        _current = scope;
        return scope;
    }

    public IFileTransactionScope BeginOrJoin()
    {
        var current = _current;
        if (current is { IsCommitted: false, IsRolledBack: false })
            return new JoinedTransactionScope(current);

        return Begin();
    }
}
