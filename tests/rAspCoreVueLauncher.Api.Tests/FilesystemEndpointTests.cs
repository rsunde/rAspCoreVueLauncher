using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using rAspCoreVueLauncher.Api.Tests.Infrastructure;
using rAspCoreVueLauncher.Shared.Filesystem;

namespace rAspCoreVueLauncher.Api.Tests;

[TestClass]
public class FilesystemEndpointTests
{
    private static (TestAppFactory factory, HttpClient client, IFilesystemService fs) Build()
    {
        var fs = Substitute.For<IFilesystemService>();
        var factory = new TestAppFactory { FilesystemSubstitute = fs };
        return (factory, factory.CreateClient(), fs);
    }

    [TestMethod]
    public async Task List_ReturnsDirectoryListing()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;
        var listing = new DirectoryListing("/home/x", "/home",
            new[] { new FileEntry("a.txt", "/home/x/a.txt", false, 3, DateTimeOffset.UnixEpoch, FileAttributes.Normal) });
        fs.List(Arg.Any<string?>()).Returns(listing);

        var response = await client.GetAsync("/api/filesystem/list?path=/home/x");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DirectoryListing>();
        body!.Path.Should().Be("/home/x");
        body.Entries.Should().HaveCount(1);
        fs.Received(1).List("/home/x");
    }

    [TestMethod]
    public async Task Roots_ReturnsEntries()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;
        fs.Roots().Returns(new[] { new FileEntry("C:\\", "C:\\", true, 0, default, FileAttributes.Directory) });

        var response = await client.GetAsync("/api/filesystem/roots");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<FileEntry>>();
        body!.Should().ContainSingle(e => e.Name == "C:\\");
    }

    [TestMethod]
    public async Task Read_ReturnsTextContent()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;
        fs.ReadTextAsync("/f.txt", Arg.Any<CancellationToken>()).Returns("hello");

        var response = await client.GetAsync("/api/filesystem/read?path=/f.txt");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("hello");
    }

    [TestMethod]
    public async Task Write_ReturnsOk()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/filesystem/write",
            new WriteFileRequest("/f.txt", "data", true));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await fs.Received(1).WriteAsync(
            Arg.Is<WriteFileRequest>(r => r.Path == "/f.txt" && r.Content == "data"),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Mkdir_ReturnsCreated()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/filesystem/mkdir", new MkdirRequest("/d"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        fs.Received(1).CreateDirectory(Arg.Is<MkdirRequest>(r => r.Path == "/d"));
    }

    [TestMethod]
    public async Task Move_ReturnsOk()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/filesystem/move",
            new MoveRequest("/a", "/b", false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fs.Received(1).Move(Arg.Is<MoveRequest>(r => r.Source == "/a" && r.Destination == "/b"));
    }

    [TestMethod]
    public async Task Copy_ReturnsOk()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/filesystem/copy",
            new CopyRequest("/a", "/b", false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await fs.Received(1).CopyAsync(Arg.Is<CopyRequest>(r => r.Source == "/a"), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Delete_ReturnsOk()
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;

        var response = await client.PostAsJsonAsync("/api/filesystem/delete",
            new DeleteRequest("/a", false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        fs.Received(1).Delete(Arg.Is<DeleteRequest>(r => r.Path == "/a" && !r.Permanent));
    }

    [TestMethod]
    [DataRow(FilesystemError.NotFound, HttpStatusCode.NotFound)]
    [DataRow(FilesystemError.AccessDenied, HttpStatusCode.Forbidden)]
    [DataRow(FilesystemError.Conflict, HttpStatusCode.Conflict)]
    [DataRow(FilesystemError.TooLarge, HttpStatusCode.RequestEntityTooLarge)]
    [DataRow(FilesystemError.TrashUnsupported, HttpStatusCode.Conflict)]
    public async Task List_MapsFilesystemErrorToStatus(FilesystemError error, HttpStatusCode expected)
    {
        var (factory, client, fs) = Build();
        await using var _ = factory;
        fs.List(Arg.Any<string?>()).Throws(new FilesystemException(error, "boom"));

        var response = await client.GetAsync("/api/filesystem/list?path=/x");

        response.StatusCode.Should().Be(expected);
    }
}
