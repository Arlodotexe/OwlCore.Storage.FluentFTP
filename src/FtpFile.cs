using FluentFTP;
using Nerdbank.Streams;

namespace OwlCore.Storage.FluentFTP;

public partial class FtpFile : IChildFile
{
    internal readonly AsyncFtpClient _ftpClient;

    /// <summary>
    /// Initializes an instance of <see cref="FtpFile"/>.
    /// </summary>
    /// <param name="ftpClient">The FTP client to use for FTP operations.</param>
    /// <param name="item">The FTP listing item to use to provide information.</param>
    public FtpFile(AsyncFtpClient ftpClient, FtpListItem item)
    {
        _ftpClient = ftpClient;
        FtpListItem = item;
    }

    /// <summary>
    /// Gets or sets the interval for property watcher polling.
    /// </summary>
    public TimeSpan PropertyWatcherInterval { get; set; } = TimeSpan.FromSeconds(1);

    public FtpListItem FtpListItem { get; }

    public string Id => Path;

    public string Path => FtpListItem.FullName;

    public string Name => FtpListItem.Name;

    public async Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default)
    {
        await _ftpClient.EnsureConnectedAsync(cancellationToken);

        var parentPath = global::System.IO.Path.GetDirectoryName(Id);

        if (string.IsNullOrEmpty(parentPath))
            return null;

        var folder = await _ftpClient.GetStorableFromPathAsync(parentPath, cancellationToken);

        if (folder is not IFolder)
            throw new InvalidOperationException();

        return (IFolder)folder;
    }

    public async Task<Stream> OpenStreamAsync(FileAccess accessMode, CancellationToken cancellationToken = default)
    {
        await _ftpClient.EnsureConnectedAsync(cancellationToken);

        switch (accessMode)
        {
            case FileAccess.Read:
                return await _ftpClient.OpenRead(Id, token: cancellationToken);
            case FileAccess.Write:
                return await _ftpClient.OpenWrite(Id, token: cancellationToken);
            case FileAccess.ReadWrite:
                {
                    var readStream = await _ftpClient.OpenRead(Id, token: cancellationToken);
                    var writeStream = await _ftpClient.OpenWrite(Id, token: cancellationToken);

                    return FullDuplexStream.Splice(readStream, writeStream);
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(accessMode));
        }
    }
}

public partial class FtpFile : ICreatedAt, ILastModifiedAt
{
    /// <inheritdoc />
    public ICreatedAtProperty CreatedAt => new FtpCreatedAtProperty(this, FtpListItem, _ftpClient.Config, PropertyWatcherInterval);

    /// <inheritdoc />
    public ILastModifiedAtProperty LastModifiedAt => new FtpLastModifiedAtProperty(this, _ftpClient, Path, PropertyWatcherInterval);
}
