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
        CreateRenamedCopyOfDelegate fallback,
        CancellationToken cancellationToken
    )
    {
        return await CreateCopyOfInteroperableAsync(sourceClient, targetClient, fileToCopy, targetFolder, overwrite, fileToCopy.Name, fallback, cancellationToken);
    }

    private async Task<IChildFile> CreateCopyOfInteroperableAsync(
        AsyncFtpClient sourceClient,
        AsyncFtpClient targetClient,
        IFile fileToCopy,
        IModifiableFolder targetFolder,
        bool overwrite,
        string newName,
        CreateRenamedCopyOfDelegate fallback,
        CancellationToken cancellationToken
    )
    {
        await Task.WhenAll(
            sourceClient.EnsureConnectedAsync(cancellationToken),
            targetClient.EnsureConnectedAsync(cancellationToken)
        );

        var targetFilePath = global::System.IO.Path.Combine(Id, newName);

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
            return await fallback(this, fileToCopy, overwrite, newName, cancellationToken);
        }

        var file = await targetClient.GetStorableFromPathAsync(targetFilePath, cancellationToken);

        if (file is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)file;
    }

    private async Task<IChildFile> MoveFromInteroperableAsync(
        AsyncFtpClient sourceClient,
        AsyncFtpClient targetClient,
        IModifiableFolder sourceFolder,
        IChildFile fileToMove,
        IModifiableFolder targetFolder,
        bool overwrite,
        MoveRenamedFromDelegate fallback,
        CancellationToken cancellationToken
    )
    {
        return await MoveFromInteroperableAsync(sourceClient, targetClient, sourceFolder, fileToMove, targetFolder, overwrite, fileToMove.Name, fallback, cancellationToken);
    }

    private async Task<IChildFile> MoveFromInteroperableAsync(
        AsyncFtpClient sourceClient,
        AsyncFtpClient targetClient,
        IModifiableFolder sourceFolder,
        IChildFile fileToMove,
        IModifiableFolder targetFolder,
        bool overwrite,
        string newName,
        MoveRenamedFromDelegate fallback,
        CancellationToken cancellationToken
    )
    {
        await Task.WhenAll(
            sourceClient.EnsureConnectedAsync(cancellationToken),
            targetClient.EnsureConnectedAsync(cancellationToken)
        );

        var targetFilePath = global::System.IO.Path.Combine(Id, newName);

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
            return await fallback(this, fileToMove, sourceFolder, overwrite, newName, cancellationToken);
        }

        await sourceClient.DeleteFile(fileToMove.Id, cancellationToken);

        var file = await targetClient.GetStorableFromPathAsync(targetFilePath, cancellationToken);

        if (file is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)file;
    }
}