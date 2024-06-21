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
        var rootFolder = await _ftpClient.GetStorableFromPathAsync("/owlcorestoragetest") as FtpFolder;

        if (rootFolder == null)
        {
            await _ftpClient.CreateDirectory("/owlcorestoragetest");
            rootFolder = await _ftpClient.GetStorableFromPathAsync("/owlcorestoragetest") as FtpFolder;
        }

        var ulid = Ulid.NewUlid().ToString();

        foreach (var character in Path.GetInvalidFileNameChars())
            ulid = ulid.Replace(character, '_');

        var folder = await rootFolder!.CreateFolderAsync(ulid);

        Assert.IsNotNull(folder);

        return (folder as IModifiableFolder)!;
    }

    public override async Task<IModifiableFolder> CreateModifiableFolderWithItems(int fileCount, int folderCount)
    {
        var rootFolder = await _ftpClient.GetStorableFromPathAsync("/owlcorestoragetest") as FtpFolder;

        if (rootFolder == null)
        {
            Assert.IsTrue(await _ftpClient.CreateDirectory("/owlcorestoragetest"));
            rootFolder = await _ftpClient.GetStorableFromPathAsync("/owlcorestoragetest") as FtpFolder;
        }

        var ulid = Ulid.NewUlid().ToString();

        foreach (var character in Path.GetInvalidFileNameChars())
            ulid = ulid.Replace(character, '_');

        var folder = await rootFolder!.CreateFolderAsync(ulid) as IModifiableFolder;

        Assert.IsNotNull(folder);

        for (var i = 0; i < fileCount; i++)
        {
            var file = await folder.CreateFileAsync($"{ulid}_{i}.txt");
            Assert.IsNotNull(file);
        }

        for (var i = 0; i < folderCount; i++)
        {
            var subFolder = await folder.CreateFolderAsync($"{ulid}_{i}");
            Assert.IsNotNull(subFolder);
        }

        return folder;
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _ftpClient.Disconnect();
        await _ftpClient.DisposeAsync();
    }
}