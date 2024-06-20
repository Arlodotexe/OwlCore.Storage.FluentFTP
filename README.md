# OwlCore.Storage.FluentFTP [![Version](https://img.shields.io/nuget/v/OwlCore.Storage.FluentFTP.svg)](https://www.nuget.org/packages/OwlCore.Storage.FluentFTP)

An implementation of OwlCore.Storage that works with FTP.

## Usage

```cs
var ftpClient = new AsyncFtpClient(...);

await ftpClient.ConnectAsync();

// Enumerate FTP server root folder.
await foreach (var item in ftpClient.GetListingAsyncEnumerable("/"))
{
    if (item.Type == FtpObjectType.Directory)
    {
        // Get an instance of FtpFolder.
        var folder = new FtpFolder(ftpClient, item);

        // Get files from the folder.
        await foreach (var file in folder.GetFilesAsync())
        {
             // Do something with the file.
        }
    }
}
```

## Financing

We accept donations [here](https://github.com/sponsors/Arlodotexe) and [here](https://www.patreon.com/arlodotexe), and we do not have any active bug bounties.

## Versioning

Version numbering follows the Semantic versioning approach. However, if the major version is `0`, the code is considered alpha and breaking changes may occur as a minor update.

## License

All OwlCore code is licensed under the MIT License. OwlCore is licensed under the MIT License. See the [LICENSE](./src/LICENSE.txt) file for more details.