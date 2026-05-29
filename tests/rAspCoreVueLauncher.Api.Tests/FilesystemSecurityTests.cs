using System.Net;
using FluentAssertions;
using NSubstitute;
using rAspCoreVueLauncher.Api.Tests.Infrastructure;
using rAspCoreVueLauncher.Shared.Filesystem;

namespace rAspCoreVueLauncher.Api.Tests;

[TestClass]
public class FilesystemSecurityTests
{
    private static IFilesystemService StubFs()
    {
        var fs = Substitute.For<IFilesystemService>();
        fs.List(Arg.Any<string?>())
          .Returns(new DirectoryListing("/x", null, Array.Empty<FileEntry>()));
        return fs;
    }

    [TestMethod]
    public async Task Filesystem_WithoutToken_WhenTokenConfigured_Is401()
    {
        await using var factory = new TestAppFactory { FilesystemSubstitute = StubFs(), FsToken = "secret" };
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/filesystem/list");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Filesystem_WithValidToken_Is200()
    {
        await using var factory = new TestAppFactory { FilesystemSubstitute = StubFs(), FsToken = "secret" };
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Launcher-Token", "secret");

        var response = await client.GetAsync("/api/filesystem/list");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task NonLoopbackHost_Is403()
    {
        await using var factory = new TestAppFactory { FilesystemSubstitute = StubFs() };
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Host = "evil.example.com";

        var response = await client.GetAsync("/api/filesystem/list");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [TestMethod]
    public async Task NoTokenConfigured_FilesystemAccessibleInDev()
    {
        await using var factory = new TestAppFactory { FilesystemSubstitute = StubFs() }; // no FsToken
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/filesystem/list");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
