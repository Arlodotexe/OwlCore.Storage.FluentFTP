using FluentFTP;

namespace OwlCore.Storage.FluentFTP;

// Interoperability between FTP servers.
public partial class FtpFolder
{
    private async Task<IChildFile> CreateCopyOfInteroperableAsync(
        AsyncFtpClient sourceClient,
        AsyncFtpClient targetClient,
        IFile fileToCopy,
        string targetFilePath,
        bool overwrite,
        CancellationToken cancellationToken
    )
    {
        await Task.WhenAll(
            sourceClient.EnsureConnectedAsync(cancellationToken),
            targetClient.EnsureConnectedAsync(cancellationToken)
        );

        var status = await sourceClient.TransferFile(
            fileToCopy.Id,
            targetClient,
            targetFilePath,
            existsMode: overwrite ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip,
            token: cancellationToken
        );

        if (status == FtpStatus.Failed)
            throw new Exception("Failed to copy file to target server.");

        var file = await targetClient.GetStorableFromPathAsync(targetFilePath, cancellationToken);

        if (file is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)file;
    }

    private async Task<IChildFile> MoveFromInteroperableAsync(
        AsyncFtpClient sourceClient,
        AsyncFtpClient targetClient,
        IFile fileToMove,
        string targetFilePath,
        bool overwrite,
        CancellationToken cancellationToken
    )
    {
        await Task.WhenAll(
            sourceClient.EnsureConnectedAsync(cancellationToken),
            targetClient.EnsureConnectedAsync(cancellationToken)
        );

        var status = await sourceClient.TransferFile(
            fileToMove.Id,
            targetClient,
            targetFilePath,
            existsMode: overwrite ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip,
            token: cancellationToken
        );

        if (status == FtpStatus.Failed)
            throw new Exception("Failed to move file to target server.");

        await sourceClient.DeleteFile(fileToMove.Id, cancellationToken);

        var file = await targetClient.GetStorableFromPathAsync(targetFilePath, cancellationToken);

        if (file is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)file;
    }
}