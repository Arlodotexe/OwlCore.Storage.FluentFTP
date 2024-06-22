using FluentFTP;
using OwlCore.Storage.CommonTests;

namespace OwlCore.Storage.FluentFTP.Tests;

[TestClass]
public class FtpFolderTests : CommonIModifiableFolderTests
{
    private AsyncFtpClient _ftpClient;

    [TestInitialize]
    public async Task InitAsync()
    {
        var serverHost = Environment.GetEnvironmentVariable("SERVER_HOST");
        var port = Convert.ToInt32(Environment.GetEnvironmentVariable("PORT"));
        var username = Environment.GetEnvironmentVariable("USERNAME");
        var password = Environment.GetEnvironmentVariable("PASSWORD");

        _ftpClient = new AsyncFtpClient(serverHost, username, password, port);
    }

    public override async Task<IModifiableFolder> CreateModifiableFolderAsync()
    {
        var rootFolder = await FtpFolder.GetFromFtpPathAsync(_ftpClient, "/");
        var folder = await rootFolder.CreateFolderAsync("owlcorestoragetest") as FtpFolder;

        var ulid = Ulid.NewUlid().ToString();

        foreach (var character in Path.GetInvalidFileNameChars())
            ulid = ulid.Replace(character, '_');

        var childFolder = await folder!.CreateFolderAsync(ulid);

        Assert.IsNotNull(childFolder);

        return (childFolder as IModifiableFolder)!;
    }

    public override async Task<IModifiableFolder> CreateModifiableFolderWithItems(int fileCount, int folderCount)
    {
        var rootFolder = await FtpFolder.GetFromFtpPathAsync(_ftpClient, "/");
        var folder = await rootFolder.CreateFolderAsync("owlcorestoragetest") as FtpFolder;

        var ulid = Ulid.NewUlid().ToString();

        foreach (var character in Path.GetInvalidFileNameChars())
            ulid = ulid.Replace(character, '_');

        var childFolder = await folder!.CreateFolderAsync(ulid) as IModifiableFolder;

        Assert.IsNotNull(childFolder);

        for (var i = 0; i < fileCount; i++)
        {
            var file = await childFolder.CreateFileAsync($"{ulid}_{i}.txt");
            Assert.IsNotNull(file);
        }

        for (var i = 0; i < folderCount; i++)
        {
            var subFolder = await childFolder.CreateFolderAsync($"{ulid}_{i}");
            Assert.IsNotNull(subFolder);
        }

        return childFolder;
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _ftpClient.Disconnect();
        await _ftpClient.DisposeAsync();
    }
}