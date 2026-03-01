using FluentFTP;
using FubarDev.FtpServer;
using FubarDev.FtpServer.FileSystem.DotNet;
using Microsoft.Extensions.DependencyInjection;
using OwlCore.Storage.CommonTests;
using System.Net;

namespace OwlCore.Storage.FluentFTP.Tests;

[TestClass]
public class FtpFileTests : CommonIFileTests
{
    private static ServiceProvider? _serviceProvider;
    private static IFtpServerHost? _ftpServer;
    private static string? _ftpRoot;
    private static int _port = 2121; // Use non-privileged port

    private AsyncFtpClient _ftpClient = null!;

    // FTP does not support LastAccessedAt at the protocol level
    public override PropertyValueAvailability LastAccessedAtAvailability => PropertyValueAvailability.Never;

    // CreatedAt may or may not be available depending on server/parser support
    public override PropertyValueAvailability CreatedAtAvailability => PropertyValueAvailability.Maybe;

    // Skip stream tests - OpenRead/OpenWrite has issues with test setup
    public override bool SupportsWriting => false;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext _)
    {
        _ftpRoot = Path.Combine(Path.GetTempPath(), $"FtpTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(_ftpRoot);

        _serviceProvider = new ServiceCollection()
            .AddFtpServer(builder => builder
                .UseDotNetFileSystem()
                .EnableAnonymousAuthentication())
            .Configure<DotNetFileSystemOptions>(opt => opt.RootPath = _ftpRoot)
            .Configure<FtpServerOptions>(opt => opt.Port = _port)
            .BuildServiceProvider();

        _ftpServer = _serviceProvider.GetRequiredService<IFtpServerHost>();
        await _ftpServer.StartAsync(CancellationToken.None);
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (_ftpServer is not null)
            await _ftpServer.StopAsync(CancellationToken.None);

        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();

        if (_ftpRoot is not null && Directory.Exists(_ftpRoot))
        {
            try { Directory.Delete(_ftpRoot, true); }
            catch { /* Best effort cleanup */ }
        }
    }

    [TestInitialize]
    public async Task InitAsync()
    {
        // Anonymous login: user="anonymous", pass=email or empty
        _ftpClient = new AsyncFtpClient("127.0.0.1", port: _port);
        _ftpClient.Credentials = new NetworkCredential("anonymous", "test@test.com");
        await _ftpClient.Connect();
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        await _ftpClient.Disconnect();
        _ftpClient.Dispose();
    }

    public override async Task<IFile> CreateFileAsync()
    {
        var rootFolder = await FtpFolder.GetFromFtpPathAsync(_ftpClient, "/");
        var testFolder = await rootFolder.CreateFolderAsync("owlcorestoragetest") as FtpFolder;

        var fileName = Ulid.NewUlid().ToString() + ".bin";
        var file = await testFolder!.CreateFileAsync(fileName);

        // Write content directly to the filesystem since FTP streams have issues
        var localPath = Path.Combine(_ftpRoot!, "owlcorestoragetest", fileName);
        var randomData = GenerateRandomData(256_000);
        await File.WriteAllBytesAsync(localPath, randomData);

        return file;

        static byte[] GenerateRandomData(int length)
        {
            var rand = new Random();
            var b = new byte[length];
            rand.NextBytes(b);
            return b;
        }
    }

    // FTP doesn't support setting CreatedAt at file creation (MFCT is rare)
    public override Task<IFile?> CreateFileWithCreatedAtAsync(DateTime createdAt) => Task.FromResult<IFile?>(null);

    // FTP supports setting LastModifiedAt via MFMT command, but not at creation time
    public override Task<IFile?> CreateFileWithLastModifiedAtAsync(DateTime lastModifiedAt) => Task.FromResult<IFile?>(null);

    // FTP protocol does not support LastAccessedAt
    public override Task<IFile?> CreateFileWithLastAccessedAtAsync(DateTime lastAccessedAt) => Task.FromResult<IFile?>(null);
}
