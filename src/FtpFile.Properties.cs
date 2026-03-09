using FluentFTP;

namespace OwlCore.Storage.FluentFTP;

/// <summary>Creation timestamp property for FTP-backed storage items.</summary>
public sealed class FtpCreatedAtProperty : SimpleMutableStorageProperty<DateTime?>, ICreatedAtProperty
{
    private readonly TimeSpan _watcherInterval;

    /// <summary>
    /// Creates a new instance of <see cref="FtpCreatedAtProperty"/>.
    /// </summary>
    /// <param name="owner">The storable that owns this property.</param>
    /// <param name="item">The FTP listing item.</param>
    /// <param name="config">The FTP client configuration for timezone settings.</param>
    /// <param name="watcherInterval">How often checks for updates should be made.</param>
    public FtpCreatedAtProperty(IStorable owner, FtpListItem item, FtpConfig config, TimeSpan watcherInterval)
        : base(
            id: owner.Id + "/" + nameof(ICreatedAt.CreatedAt),
            name: nameof(ICreatedAt.CreatedAt),
            getter: () =>
            {
                if (item.Created == DateTime.MinValue)
                    return null;
                    
                // FluentFTP handles conversion based on TimeConversion setting
                // item.Created is already converted per TimeConversion; ensure local time for storage contract
                return FtpTimeHelper.ToLocalTime(item.Created, config);
            })
    {
        _watcherInterval = watcherInterval;
    }

    /// <inheritdoc/>
    public override async Task<IStoragePropertyWatcher<DateTime?>> GetWatcherAsync(CancellationToken cancellationToken)
    {
        var initialValue = await GetValueAsync(cancellationToken);
        return new FtpTimerBasedPropertyWatcher<DateTime?>(this, _watcherInterval, initialValue);
    }
}

/// <summary>Last modified timestamp property for FTP-backed files.</summary>
public sealed class FtpLastModifiedAtProperty : SimpleModifiableStorageProperty<DateTime?>, IModifiableLastModifiedAtProperty
{
    private readonly TimeSpan _watcherInterval;

    /// <summary>
    /// Creates a new instance of <see cref="FtpLastModifiedAtProperty"/>.
    /// </summary>
    /// <param name="owner">The storable that owns this property.</param>
    /// <param name="client">The FTP client.</param>
    /// <param name="path">The FTP path to the file.</param>
    /// <param name="watcherInterval">How often checks for updates should be made.</param>
    public FtpLastModifiedAtProperty(IStorable owner, AsyncFtpClient client, string path, TimeSpan watcherInterval)
        : base(
            id: owner.Id + "/" + nameof(ILastModifiedAt.LastModifiedAt),
            name: nameof(ILastModifiedAt.LastModifiedAt),
            asyncGetter: async ct =>
            {
                ct.ThrowIfCancellationRequested();
                await client.EnsureConnectedAsync(ct);
                var modified = await client.GetModifiedTime(path, ct);
                if (modified == DateTime.MinValue)
                    return null;
                // GetModifiedTime returns based on TimeConversion setting; ensure local time for storage contract
                return FtpTimeHelper.ToLocalTime(modified, client.Config);
            },
            asyncSetter: async (v, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                if (v is null)
                    throw new ArgumentNullException(nameof(v), "Cannot set LastModifiedAt to null on FTP.");
                await client.EnsureConnectedAsync(ct);
                // Convert from local time back to what FluentFTP expects based on TimeConversion
                var ftpTime = FtpTimeHelper.FromLocalTime(v.Value, client.Config);
                await client.SetModifiedTime(path, ftpTime, ct);
            })
    {
        _watcherInterval = watcherInterval;
    }

    /// <inheritdoc/>
    public override async Task<IStoragePropertyWatcher<DateTime?>> GetWatcherAsync(CancellationToken cancellationToken)
    {
        var initialValue = await GetValueAsync(cancellationToken);
        return new FtpTimerBasedPropertyWatcher<DateTime?>(this, _watcherInterval, initialValue);
    }
}

/// <summary>Last modified timestamp property for FTP-backed folders (read-only, from listing).</summary>
public sealed class FtpFolderLastModifiedAtProperty : SimpleMutableStorageProperty<DateTime?>, ILastModifiedAtProperty
{
    private readonly TimeSpan _watcherInterval;

    /// <summary>
    /// Creates a new instance of <see cref="FtpFolderLastModifiedAtProperty"/>.
    /// </summary>
    /// <param name="owner">The storable that owns this property.</param>
    /// <param name="item">The FTP listing item.</param>
    /// <param name="config">The FTP client configuration for timezone settings.</param>
    /// <param name="watcherInterval">How often checks for updates should be made.</param>
    public FtpFolderLastModifiedAtProperty(IStorable owner, FtpListItem item, FtpConfig config, TimeSpan watcherInterval)
        : base(
            id: owner.Id + "/" + nameof(ILastModifiedAt.LastModifiedAt),
            name: nameof(ILastModifiedAt.LastModifiedAt),
            getter: () =>
            {
                if (item.Modified == DateTime.MinValue)
                    return null;

                // FluentFTP handles conversion based on TimeConversion setting; ensure local time for storage contract
                return FtpTimeHelper.ToLocalTime(item.Modified, config);
            })
    {
        _watcherInterval = watcherInterval;
    }

    /// <inheritdoc/>
    public override async Task<IStoragePropertyWatcher<DateTime?>> GetWatcherAsync(CancellationToken cancellationToken)
    {
        var initialValue = await GetValueAsync(cancellationToken);
        return new FtpTimerBasedPropertyWatcher<DateTime?>(this, _watcherInterval, initialValue);
    }
}
