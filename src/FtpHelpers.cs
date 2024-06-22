using FluentFTP;

namespace OwlCore.Storage.FluentFTP
{
    public static class FtpHelpers
    {
        internal static Task EnsureConnectedAsync(this AsyncFtpClient client, CancellationToken cancellationToken = default)
        {
            return client.IsConnected ? Task.CompletedTask : client.Connect(true, cancellationToken);
        }

        internal static async Task<IStorable?> GetStorableFromPathAsync(this AsyncFtpClient client, string path, CancellationToken cancellationToken = default)
        {
            await client.EnsureConnectedAsync(cancellationToken);

            var item = await client.GetObjectInfo(path, true, cancellationToken);

            if (item == null) return null;

            return item.Type switch
            {
                FtpObjectType.Directory => new FtpFolder(client, item),
                FtpObjectType.File => new FtpFile(client, item),
                FtpObjectType.Link => new FtpFile(client, item),
                _ => throw new NotSupportedException("Item type is not supported."),
            };
        }
    }
}
