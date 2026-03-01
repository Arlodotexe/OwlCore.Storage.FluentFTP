using FluentFTP;
using FubarDev.FtpServer;
using FubarDev.FtpServer.FileSystem.DotNet;
using Microsoft.Extensions.DependencyInjection;
using OwlCore.Storage.CommonTests;
using System.Net;

namespace OwlCore.Storage.FluentFTP.Tests;

[TestClass]
public class FtpFolderTests : CommonIModifiableFolderTests
{
    private static ServiceProvider? _serviceProvider;
    private static IFtpServerHost? _ftpServer;
    private static string? _ftpRoot;
    private static int _port = 2122; // Different port from file tests to avoid conflicts

    private AsyncFtpClient _ftpClient = null!;

    // FTP does not support LastAccessedAt at the protocol level
    public override PropertyValueAvailability LastAccessedAtAvailability => PropertyValueAvailability.Never;

    // CreatedAt may or may not be available depending on server/parser support
    public override PropertyValueAvailability CreatedAtAvailability => PropertyValueAvailability.Maybe;

    // FTP protocol doesn't update LastAccessedAt
    public override PropertyUpdateBehavior LastAccessedAtUpdateBehavior => PropertyUpdateBehavior.Never;

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

    // FTP doesn't support setting folder timestamps at creation
    public override Task<IFolder?> CreateFolderWithCreatedAtAsync(DateTime createdAt) => Task.FromResult<IFolder?>(null);
    public override Task<IFolder?> CreateFolderWithLastModifiedAtAsync(DateTime lastModifiedAt) => Task.FromResult<IFolder?>(null);
    public override Task<IFolder?> CreateFolderWithLastAccessedAtAsync(DateTime lastAccessedAt) => Task.FromResult<IFolder?>(null);
    public override Task<IFile?> CreateFileInFolderWithLastModifiedAtAsync(IModifiableFolder folder, DateTime lastModifiedAt) => Task.FromResult<IFile?>(null);
    public override Task<CommonIModifiableFolderTests.CreateFileInFolderWithTimestampsResult?> CreateFileInFolderWithTimestampsAsync(IModifiableFolder folder, DateTime? createdAt, DateTime? lastModifiedAt, DateTime? lastAccessedAt) => Task.FromResult<CommonIModifiableFolderTests.CreateFileInFolderWithTimestampsResult?>(null);
}