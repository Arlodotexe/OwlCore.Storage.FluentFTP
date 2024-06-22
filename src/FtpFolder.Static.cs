using FluentFTP;

namespace OwlCore.Storage.FluentFTP;

// Helper methods to get folders from path directly.
public partial class FtpFolder
{
    /// <summary>
    /// Gets an <see cref="FtpFolder"/> from the provided path.
    /// </summary>
    /// <param name="ftpClient">The FTP client to use for retrieval.</param>
    /// <param name="path">The path to retrieve from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="FtpFolder"/> that represents the folder.</returns>
    /// <exception cref="FileNotFoundException">Thrown when folder isn't found.</exception>
    public static async Task<FtpFolder> GetFromFtpPathAsync(AsyncFtpClient ftpClient, string path, CancellationToken cancellationToken = default)
    {
        var folder = await TryGetFromFtpPathAsync(ftpClient, path, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return folder is null
            ? throw new FileNotFoundException($"Cannot find folder in FTP server with path \"{path}\".")
            : folder;
    }

    /// <summary>
    /// Tries to get an <see cref="FtpFolder"/> from the provided path.
    /// </summary>
    /// <param name="ftpClient">The FTP client to use for retrieval.</param>
    /// <param name="path">The path to retrieve from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The folder if found. `null` if it isn't found or something else went wrong.</returns>
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

        return item as FtpFolder;
    }
}