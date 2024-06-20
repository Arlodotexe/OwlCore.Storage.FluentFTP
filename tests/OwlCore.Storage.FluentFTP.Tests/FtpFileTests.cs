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
        _ftpClient = new AsyncFtpClient("192.168.197.136", "Anonymous", "", 21);
    }

    [TestMethod]
    [AllEnumFlagCombinations(typeof(FileAccess))]
    public async Task OpenStreamAndTryEachAccessMode(FileAccess accessMode)
    {
        var file = await CreateFileAsync();

        if (accessMode == 0)
        {
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => file.OpenStreamAsync(accessMode));
            return;
        }

        // Don't test writing if not supported.
        if (!SupportsWriting)
            accessMode ^= FileAccess.Write;

        // If removing write access resulted in empty flag.
        if (accessMode == 0)
            return;

        using var stream = await file.OpenStreamAsync(accessMode);

        if (accessMode.HasFlag(FileAccess.Read))
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            Assert.AreNotEqual(0, memoryStream.ToArray().Length);
        }

        if (accessMode.HasFlag(FileAccess.Write) && SupportsWriting)
        {
            stream.WriteByte(0);
        }
    }

    public override Task<IFile> CreateFileAsync()
    {
        return GenerateRandomFile(256_000);

        async Task<IFile> GenerateRandomFile(int fileSize)
        {
            // Create
            var folder = await _ftpClient.GetStorableFromPathAsync("/owlcorestoragetest") as FtpFolder;

            if (folder == null)
            {
                Assert.IsTrue(await _ftpClient.CreateDirectory("/owlcorestoragetest"));
                folder = await _ftpClient.GetStorableFromPathAsync("/owlcorestoragetest") as FtpFolder;
            }

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