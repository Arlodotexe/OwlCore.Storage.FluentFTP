using FluentFTP;
using Nerdbank.Streams;

namespace OwlCore.Storage.FluentFTP;

/// <summary>
/// Initializes an instance of <see cref="FtpFolder"/>.
/// </summary>
/// <param name="ftpClient">The FTP client to use for the file operations.</param>
/// <param name="item">The FTP listing item to use to provide information.</param>
public class FtpFile(AsyncFtpClient ftpClient, FtpListItem item) : IChildFile
{
    public FtpListItem FtpListItem => item;

    public string Id => Path;

    public string Path => FtpListItem.FullName;

    public string Name => FtpListItem.Name;

    public async Task<IFolder?> GetParentAsync(CancellationToken cancellationToken = default)
    {
        await ftpClient.EnsureConnectedAsync(cancellationToken);

        var parentPath = global::System.IO.Path.GetDirectoryName(Id);

        if (string.IsNullOrEmpty(parentPath))
            return null;

        var folder = await ftpClient.GetStorableFromPathAsync(parentPath, cancellationToken);

        if (folder is not IFolder)
            throw new InvalidOperationException();

        return (IFolder)folder;
    }

    public async Task<Stream> OpenStreamAsync(FileAccess accessMode, CancellationToken cancellationToken = default)
    {
        await ftpClient.EnsureConnectedAsync(cancellationToken);

        switch (accessMode)
        {
            case FileAccess.Read:
                return await ftpClient.OpenRead(Id, token: cancellationToken);
            case FileAccess.Write:
                return await ftpClient.OpenWrite(Id, token: cancellationToken);
            case FileAccess.ReadWrite:
                {
                    var readStream = await ftpClient.OpenRead(Id, token: cancellationToken);
                    var writeStream = await ftpClient.OpenWrite(Id, token: cancellationToken);

                    return FullDuplexStream.Splice(readStream, writeStream);
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(accessMode));
        }
    }
}