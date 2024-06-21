using FluentFTP;
using System.Runtime.CompilerServices;

namespace OwlCore.Storage.FluentFTP;

/// <summary>
/// Initializes an instance of <see cref="FtpFolder"/>.
/// </summary>
/// <param name="ftpClient">The FTP client to use for the file operations.</param>
/// <param name="item">The FTP listing item to use to provide information.</param>
public class FtpFolder(AsyncFtpClient ftpClient, FtpListItem item) :
    IModifiableFolder,
    IChildFolder,
    IGetItem,
    IGetFirstByName,
    IGetItemRecursive,
    IMoveFrom,
    ICreateCopyOf
{
    public FtpListItem FtpListItem => item;

    public string Name => FtpListItem.Name;

    public string Id => Path;

    public string Path => FtpListItem.FullName;

    public async Task<IChildFile> CreateCopyOfAsync(IFile fileToCopy, bool overwrite, CancellationToken cancellationToken, CreateCopyOfDelegate fallback)
    {
        await ftpClient.EnsureConnectedAsync(cancellationToken);

        if (fileToCopy is not FtpFile)
            return await fallback(this, fileToCopy, overwrite, cancellationToken);

        var newFilePath = global::System.IO.Path.Combine(Id, fileToCopy.Name);

        if (!overwrite && await ftpClient.FileExists(newFilePath, cancellationToken))
            goto GetStorable;

        using (var stream = await ftpClient.OpenRead(fileToCopy.Id, token: cancellationToken))
        {
            var status = await ftpClient.UploadStream(stream, newFilePath, FtpRemoteExists.Overwrite, token: cancellationToken);

            if (status == FtpStatus.Failed)
                throw new Exception("Failed to copy file.");
        }

    GetStorable:
        var item = await ftpClient.GetStorableFromPathAsync(newFilePath, cancellationToken);

        if (item is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)item;
    }

    public async Task<IChildFile> CreateFileAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        await ftpClient.EnsureConnectedAsync(cancellationToken);

        var newFilePath = global::System.IO.Path.Combine(Id, name);

        var status = await ftpClient.UploadBytes(
            [],
            newFilePath,
            overwrite ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip,
            token: cancellationToken
        );

        if (status == FtpStatus.Failed)
            throw new Exception($"Failed to create file with name \"{name}\" in path \"{Id}\".");

        var item = await ftpClient.GetStorableFromPathAsync(newFilePath, cancellationToken);

        if (item is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)item;
    }

    public async Task<IChildFolder> CreateFolderAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        await ftpClient.EnsureConnectedAsync(cancellationToken);

        var folderPath = global::System.IO.Path.Combine(Id, name);
        var folderExists = await ftpClient.DirectoryExists(folderPath, cancellationToken);

        if (overwrite)
            await ftpClient.DeleteDirectory(folderPath, FtpListOption.Recursive, cancellationToken);
        else if (folderExists)
        {
            var existing = await ftpClient.GetStorableFromPathAsync(folderPath, cancellationToken);

            if (existing is not IChildFolder)
                throw new InvalidOperationException();

            return (IChildFolder)existing;
        }

        if (!await ftpClient.CreateDirectory(folderPath, cancellationToken))
            throw new Exception($"Failed to create folder with name \"{name}\" in path \"{Id}\".");

        var item = await ftpClient.GetStorableFromPathAsync(folderPath, cancellationToken);

        if (item is not IChildFolder)
            throw new InvalidOperationException();

        return (IChildFolder)item;
    }

    public async Task DeleteAsync(IStorableChild item, CancellationToken cancellationToken = default)
    {
        await ftpClient.EnsureConnectedAsync(cancellationToken);

        if (item is IFolder)
        {
            await ftpClient.DeleteDirectory(item.Id, FtpListOption.Recursive, cancellationToken);
            return;
        }

        await ftpClient.DeleteFile(item.Id, cancellationToken);
    }

    public async Task<IStorableChild> GetFirstByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await ftpClient.EnsureConnectedAsync(cancellationToken);
        return await GetItemAsync(global::System.IO.Path.Combine(Id, name), cancellationToken);
    }

    public Task<IFolderWatcher> GetFolderWatcherAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Cannot create a watcher for FTP folders.");
    }

    public async Task<IStorableChild> GetItemAsync(string id, CancellationToken cancellationToken = default)
    {
        await ftpClient.EnsureConnectedAsync(cancellationToken);

        var item = await ftpClient.GetStorableFromPathAsync(id, cancellationToken)
            ?? throw new FileNotFoundException($"Could not find item with path \"{id}\".");

        if (!id.Contains(item.Id))
            throw new FileNotFoundException("The provided Id does not belong to an item in this folder.");

        if (item is IChildFolder folder)
            return folder;

        return (IChildFile)item;
    }

    public Task<IStorableChild> GetItemRecursiveAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetItemAsync(id, cancellationToken);
    }

    public async IAsyncEnumerable<IStorableChild> GetItemsAsync(StorableType type = StorableType.All, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (type == StorableType.None)
            throw new ArgumentOutOfRangeException(nameof(type), $"{nameof(StorableType)}.{type} is not valid here.");

        await ftpClient.EnsureConnectedAsync(cancellationToken);

        var enumerable = ftpClient.GetListingEnumerable(Id, cancellationToken)
            .Where(item => type switch
            {
                StorableType.File => item.Type == FtpObjectType.File,
                StorableType.Folder => item.Type == FtpObjectType.Directory,
                _ => true
            })
            .Select<FtpListItem, IStorableChild>(item =>
            {
                if (item.Type == FtpObjectType.Directory)
                    return new FtpFolder(ftpClient, item);

                return new FtpFile(ftpClient, item);
            });

        await foreach (var item in enumerable)
            yield return item;
    }

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

    public async Task<IChildFile> MoveFromAsync(IChildFile fileToMove, IModifiableFolder source, bool overwrite, CancellationToken cancellationToken, MoveFromDelegate fallback)
    {
        await ftpClient.EnsureConnectedAsync(cancellationToken);

        if (source is not FtpFolder)
            return await fallback(this, fileToMove, source, overwrite, cancellationToken);

        var newFilePath = global::System.IO.Path.Combine(Id, fileToMove.Name);

        if (!overwrite && await ftpClient.FileExists(newFilePath, cancellationToken))
            goto GetStorable;

        if (!await ftpClient.MoveFile(fileToMove.Id, newFilePath, FtpRemoteExists.Overwrite, cancellationToken))
            throw new Exception($"Cannot move file \"{fileToMove.Id}\" to destination path \"{Id}\".");

    GetStorable:
        var item = await ftpClient.GetStorableFromPathAsync(newFilePath, cancellationToken);

        if (item is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)item;
    }
}
