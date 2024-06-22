using FluentFTP;

namespace OwlCore.Storage.FluentFTP;

public partial class FtpFile
{
    /// <summary>
    /// Gets an <see cref="FtpFile"/> from the provided path.
    /// </summary>
    /// <param name="ftpClient">The FTP client to use for retrieval.</param>
    /// <param name="path">The path to retrieve from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="FtpFile"/> that represents the file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when file isn't found.</exception>
    public static async Task<FtpFile> GetFromFtpPathAsync(AsyncFtpClient ftpClient, string path, CancellationToken cancellationToken = default)
    {
        var file = await TryGetFromFtpPathAsync(ftpClient, path, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        return file is null
            ? throw new FileNotFoundException($"Cannot find file in FTP server with path \"{path}\".")
            : file;
    }

    /// <summary>
    /// Tries to get an <see cref="FtpFile"/> from the provided path.
    /// </summary>
    /// <param name="ftpClient">The FTP client to use for retrieval.</param>
    /// <param name="path">The path to retrieve from.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file if found. `null` if it isn't found or something else went wrong.</returns>
    public static async Task<FtpFile?> TryGetFromFtpPathAsync(AsyncFtpClient ftpClient, string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var c in global::System.IO.Path.GetInvalidPathChars())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (path.Contains(c))
                throw new FormatException($"Provided path contains invalid character '{c}'.");
        }

        var item = await ftpClient.GetStorableFromPathAsync(path, cancellationToken);

        return item is FtpFile file ? file : null;
    }
}