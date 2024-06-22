using FluentFTP;

namespace OwlCore.Storage.FluentFTP;

public partial class FtpFolder
{
    public static async Task<FtpFolder> GetFromFtpPathAsync(AsyncFtpClient ftpClient, string path, CancellationToken cancellationToken = default)
    {
        var folder = await TryGetFromFtpPathAsync(ftpClient, path, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return folder is null
            ? throw new FileNotFoundException($"Cannot find folder in FTP server with path \"{path}\".")
            : folder;
    }

    public static async Task<FtpFolder?> TryGetFromFtpPathAsync(AsyncFtpClient ftpClient, string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var c in global::System.IO.Path.GetInvalidPathChars())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (path.Contains(c))
                throw new FormatException($"Provided path contains invalid character '{c}'.");
        }

        var item = await ftpClient.GetStorableFromPathAsync(path, cancellationToken);

        return item is FtpFolder folder ? folder : null;
    }
}