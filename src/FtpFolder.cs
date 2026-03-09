using FluentFTP;
using System.Runtime.CompilerServices;

namespace OwlCore.Storage.FluentFTP;

/// <summary>
/// Represents a <see cref="IFolder" /> in an FTP storage system, providing access
/// to folder properties and content through the FluentFTP library.
/// </summary>
public partial class FtpFolder :
    IModifiableFolder,
    IChildFolder,
    IGetItem,
    IGetFirstByName,
    IGetItemRecursive,
    ICreateRenamedCopyOf,
    IMoveRenamedFrom
{
    internal readonly AsyncFtpClient _ftpClient;

    /// <summary>
    /// Initializes an instance of <see cref="FtpFolder"/>.
    /// </summary>
    /// <param name="ftpClient">The FTP client to use for the file operations.</param>
    /// <param name="item">The FTP listing item to use to provide information.</param>
    public FtpFolder(AsyncFtpClient ftpClient, FtpListItem item)
    {
        _ftpClient = ftpClient;
        FtpListItem = item;
    }

    /// <summary>
    /// Gets or sets the interval for property watcher polling.
    /// </summary>
    public TimeSpan PropertyWatcherInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The underlying FTP listing item that provides information about this folder.
    /// </summary>
    public FtpListItem FtpListItem { get; }

    /// <inheritdoc />
    public string Name => FtpListItem.Name;

    /// <inheritdoc />
    public string Id => Path;

    /// <inheritdoc />
    public string Path => FtpListItem.FullName;

    /// <inheritdoc />
    public Task<IChildFile> CreateCopyOfAsync(IFile fileToCopy, bool overwrite, CancellationToken cancellationToken, CreateCopyOfDelegate fallback)
    {
        // For code deduplication in this implementation,
        // route to the overload with rename support
        // while using the given non-rename overload as fallback.
        // This also discards the filled newName param in the fallback, which is originally passed into the newName param in the following method call:
        return CreateCopyOfAsync(fileToCopy, overwrite, newName: fileToCopy.Name, cancellationToken, (modifiableFolder, file, overwrite, _, cancellationToken) => fallback(modifiableFolder, file, overwrite, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<IChildFile> CreateCopyOfAsync(IFile fileToCopy, bool overwrite, string newName, CancellationToken cancellationToken, CreateRenamedCopyOfDelegate fallback)
    {
        await _ftpClient.EnsureConnectedAsync(cancellationToken);

        if (fileToCopy is not FtpFile)
            return await fallback(this, fileToCopy, overwrite, newName, cancellationToken);
        else
        {
            var ftpFile = (FtpFile)fileToCopy;

            var targetHostUri = new Uri($"ftp://{_ftpClient.Host}:{_ftpClient.Port}");
            var sourceHostUri = new Uri($"ftp://{ftpFile._ftpClient.Host}:{ftpFile._ftpClient.Port}");

            if (sourceHostUri != targetHostUri)
            {
                return await CreateCopyOfInteroperableAsync(ftpFile._ftpClient, _ftpClient, fileToCopy, this, overwrite, newName, fallback, cancellationToken);
            }
        }

        var newFilePath = global::System.IO.Path.Combine(Id, newName);

        if (!overwrite && await _ftpClient.FileExists(newFilePath, cancellationToken))
            throw new FileAlreadyExistsException("Destination file already exists.");

        // Get source LastModifiedAt before copying
        DateTime? sourceLastModified = null;
        if (fileToCopy is ILastModifiedAt lastModifiedSource)
        {
            sourceLastModified = await lastModifiedSource.LastModifiedAt.GetValueAsync(cancellationToken);
        }

        using (var stream = await _ftpClient.OpenRead(fileToCopy.Id, token: cancellationToken))
        {
            var status = await _ftpClient.UploadStream(stream, newFilePath, FtpRemoteExists.Overwrite, token: cancellationToken);

            if (status == FtpStatus.Failed)
                throw new Exception("Failed to copy file.");
        }

        // Preserve LastModifiedAt on the copy (copy semantics)
        if (sourceLastModified is not null)
        {
            try
            {
                await _ftpClient.SetModifiedTime(newFilePath, sourceLastModified.Value, cancellationToken);
            }
            catch
            {
                // Server may not support MFMT - best effort
            }
        }

        var item = await _ftpClient.GetStorableFromPathAsync(newFilePath, cancellationToken);

        if (item is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)item;
    }

    /// <inheritdoc />
    public Task<IChildFile> MoveFromAsync(IChildFile fileToMove, IModifiableFolder source, bool overwrite, CancellationToken cancellationToken, MoveFromDelegate fallback)
    {
        // For code deduplication in this implementation,
        // route to the overload with rename support
        // while using the given non-rename overload as fallback.
        // This also discards the filled newName param in the fallback, which is originally passed into the newName param in the following method call:
        return MoveFromAsync(fileToMove, source, overwrite, newName: fileToMove.Name, cancellationToken, (modifiableFolder, file, source, overwrite, _, cancellationToken) => fallback(modifiableFolder, file, source, overwrite, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<IChildFile> MoveFromAsync(IChildFile fileToMove, IModifiableFolder source, bool overwrite, string newName, CancellationToken cancellationToken, MoveRenamedFromDelegate fallback)
    {
        await _ftpClient.EnsureConnectedAsync(cancellationToken);

        if (source is not FtpFolder)
            return await fallback(this, fileToMove, source, overwrite, newName, cancellationToken);
        else
        {
            var ftpFile = (FtpFile)fileToMove;

            var targetHostUri = new Uri($"ftp://{_ftpClient.Host}:{_ftpClient.Port}");
            var sourceHostUri = new Uri($"ftp://{ftpFile._ftpClient.Host}:{ftpFile._ftpClient.Port}");

            if (sourceHostUri != targetHostUri)
            {
                return await MoveFromInteroperableAsync(ftpFile._ftpClient, _ftpClient, source, fileToMove, this, overwrite, newName, fallback, cancellationToken);
            }
        }

        var newFilePath = global::System.IO.Path.Combine(Id, newName);

        if (!overwrite && await _ftpClient.FileExists(newFilePath, cancellationToken))
            throw new FileAlreadyExistsException("Destination file already exists.");

        if (!await _ftpClient.MoveFile(fileToMove.Id, newFilePath, FtpRemoteExists.Overwrite, cancellationToken))
            throw new Exception($"Cannot move file \"{fileToMove.Id}\" to destination path \"{Id}\".");

        var item = await _ftpClient.GetStorableFromPathAsync(newFilePath, cancellationToken);

        if (item is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)item;
    }

    /// <inheritdoc />
    public async Task<IChildFile> CreateFileAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        await _ftpClient.EnsureConnectedAsync(cancellationToken);

        var newFilePath = global::System.IO.Path.Combine(Id, name);

        var status = await _ftpClient.UploadBytes(
            [],
            newFilePath,
            overwrite ? FtpRemoteExists.Overwrite : FtpRemoteExists.Skip,
            token: cancellationToken
        );

        if (status == FtpStatus.Failed)
            throw new Exception($"Failed to create file with name \"{name}\" in path \"{Id}\".");

        var item = await _ftpClient.GetStorableFromPathAsync(newFilePath, cancellationToken);

        if (item is not IChildFile)
            throw new InvalidOperationException();

        return (IChildFile)item;
    }

    /// <inheritdoc />
    public async Task<IChildFolder> CreateFolderAsync(string name, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        await _ftpClient.EnsureConnectedAsync(cancellationToken);

        var folderPath = global::System.IO.Path.Combine(Id, name);
        var folderExists = await _ftpClient.DirectoryExists(folderPath, cancellationToken);

        if (overwrite)
            await _ftpClient.DeleteDirectory(folderPath, FtpListOption.Recursive, cancellationToken);
        else if (folderExists)
        {
            var existing = await _ftpClient.GetStorableFromPathAsync(folderPath, cancellationToken);

            if (existing is not IChildFolder)
                throw new InvalidOperationException();

            return (IChildFolder)existing;
        }

        if (!await _ftpClient.CreateDirectory(folderPath, cancellationToken))
            throw new Exception($"Failed to create folder with name \"{name}\" in path \"{Id}\".");

        var item = await _ftpClient.GetStorableFromPathAsync(folderPath, cancellationToken);

        if (item is not IChildFolder)
            throw new InvalidOperationException();

        return (IChildFolder)item;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(IStorableChild item, CancellationToken cancellationToken = default)
    {
        await _ftpClient.EnsureConnectedAsync(cancellationToken);

        if (item is IFolder)
        {
            await _ftpClient.DeleteDirectory(item.Id, FtpListOption.Recursive, cancellationToken);
            return;
        }

        await _ftpClient.DeleteFile(item.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IStorableChild> GetFirstByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await _ftpClient.EnsureConnectedAsync(cancellationToken);
        return await GetItemAsync(global::System.IO.Path.Combine(Id, name), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IFolderWatcher> GetFolderWatcherAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Cannot create a watcher for FTP folders.");
    }

    /// <inheritdoc />
    public async Task<IStorableChild> GetItemAsync(string id, CancellationToken cancellationToken = default)
    {
        await _ftpClient.EnsureConnectedAsync(cancellationToken);

        var item = await _ftpClient.GetStorableFromPathAsync(id, cancellationToken)
            ?? throw new FileNotFoundException($"Could not find item with path \"{id}\".");

        if (!id.Contains(item.Id))
            throw new FileNotFoundException("The provided Id does not belong to an item in this folder.");

        if (item is IChildFolder folder)
            return folder;

        return (IChildFile)item;
    }

    /// <inheritdoc />
    public Task<IStorableChild> GetItemRecursiveAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetItemAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<IStorableChild> GetItemsAsync(StorableType type = StorableType.All, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (type == StorableType.None)
            throw new ArgumentOutOfRangeException(nameof(type), $"{nameof(StorableType)}.{type} is not valid here.");

        await _ftpClient.EnsureConnectedAsync(cancellationToken);

#if NETSTANDARD2_0
        var listing = await _ftpClient.GetListing(Id, token: cancellationToken);
        foreach (var item in listing)
        {
            if (item.Name == "." || item.Name == "..") continue;

            bool typeMatch = type switch
            {
                StorableType.File => item.Type == FtpObjectType.File,
                StorableType.Folder => item.Type == FtpObjectType.Directory,
                _ => true
            };

            if (!typeMatch) continue;

            if (item.Type == FtpObjectType.Directory)
                yield return new FtpFolder(_ftpClient, item);
            else
                yield return new FtpFile(_ftpClient, item);
        }
#else
        var enumerable = _ftpClient.GetListingEnumerable(Id, cancellationToken)
            .Where(item => item.Name != "." && item.Name != "..") // Filter out . and .. directory entries
            .Where(item => type switch
            {
                StorableType.File => item.Type == FtpObjectType.File,
                StorableType.Folder => item.Type == FtpObjectType.Directory,
                _ => true
            })
            .Select<FtpListItem, IStorableChild>(item =>
            {
                if (item.Type == FtpObjectType.Directory)
                    return new FtpFolder(_ftpClient, item);

                return new FtpFile(_ftpClient, item);
            });

        await foreach (var item in enumerable)
            yield return item;
#endif
    }

    /// <inheritdoc />
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
}

public partial class FtpFolder : ICreatedAt, ILastModifiedAt
{
    /// <inheritdoc />
    public ICreatedAtProperty CreatedAt => new FtpCreatedAtProperty(this, FtpListItem, _ftpClient.Config, PropertyWatcherInterval);

    /// <inheritdoc />
    public ILastModifiedAtProperty LastModifiedAt => new FtpFolderLastModifiedAtProperty(this, FtpListItem, _ftpClient.Config, PropertyWatcherInterval);
}
