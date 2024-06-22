using FluentFTP;
using OwlCore.Storage.CommonTests;

namespace OwlCore.Storage.FluentFTP.Tests;

[TestClass]
public class FtpFileTests : CommonIFileTests
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

    public override Task<IFile> CreateFileAsync()
    {
        return GenerateRandomFile(256_000);

        async Task<IFile> GenerateRandomFile(int fileSize)
        {
            // Create
            var rootFolder = await FtpFolder.GetFromFtpPathAsync(_ftpClient, "/");
            var folder = await rootFolder.CreateFolderAsync("owlcorestoragetest") as FtpFolder;

            var file = await folder!.CreateFileAsync(Ulid.NewUlid().ToString());

            // Write
            await file.WriteBytesAsync(GenerateRandomData(fileSize));

            await _ftpClient.GetReply();

            return file;
        }

        byte[] GenerateRandomData(int length)
        {
            var rand = new Random();
            var b = new byte[length];
            rand.NextBytes(b);

            return b;
        }
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _ftpClient.Disconnect();
        await _ftpClient.DisposeAsync();
    }
}