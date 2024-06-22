using FluentFTP;

namespace OwlCore.Storage.FluentFTP;

// Interoperability between FTP servers.
public partial class FtpFolder
{
    private async Task<IChildFile> CreateCopyOfInteroperableAsync(
        AsyncFtpClient sourceClient,
        AsyncFtpClient targetClient,
        IFile fileToCopy,
        IModifiableFolder targetFolder,
        bool overwrite,
        CreateCopyOfDelegate fallback,
        CancellationToken cancellationToken
    )
    {
        await Task.WhenAll(
            sourceClient.EnsureConnectedAsync(cancellationToken),
            targetClient.EnsureConnectedAsync(cancellationToken)
        );

        var targetFilePath = global::System.IO.Path.Combine(Id, fileToCopy.Name);

        var status = await sourceClient.TransferFile(
            fileToCopy.Id,
            targetClient,
            targetFilePath,
            existsMode: overwrite ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip,
            token: cancellationToken
        );

        if (status == FtpStatus.Failed)
        {
            // Either the server does not support FXP or the transfer failed.
            // Only thing we can do in this case is to use the fallback
            // implementation.
            return await fallback(this, fileToCopy, overwrite, cancellationToken);
        }

        var file = await targetClient.GetStorableFromPathAsync(targetFilePath, cancellationToken);

        if (file is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)file;
    }

    private async Task<IChildFile> MoveFromInteroperableAsync(
        AsyncFtpClient sourceClient,
        AsyncFtpClient targetClient,
        IFile fileToMove,
        IModifiableFolder targetFolder,
        bool overwrite,
        MoveFromDelegate fallback,
        CancellationToken cancellationToken
    )
    {
        await Task.WhenAll(
            sourceClient.EnsureConnectedAsync(cancellationToken),
            targetClient.EnsureConnectedAsync(cancellationToken)
        );

        var targetFilePath = global::System.IO.Path.Combine(Id, fileToMove.Name);

        var status = await sourceClient.TransferFile(
            fileToMove.Id,
            targetClient,
            targetFilePath,
            existsMode: overwrite ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip,
            token: cancellationToken
        );

        if (status == FtpStatus.Failed)
        {
            // Either the server does not support FXP or the transfer failed.
            // Only thing we can do in this case is to use the fallback
            // implementation.
            return await fallback(this, fileToMove, overwrite, cancellationToken);
        }

        await sourceClient.DeleteFile(fileToMove.Id, cancellationToken);

        var file = await targetClient.GetStorableFromPathAsync(targetFilePath, cancellationToken);

        if (file is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)file;
    }
}