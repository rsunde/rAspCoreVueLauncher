using FluentAssertions;
using NSubstitute;
using rAspCoreVueLauncher.Api.Filesystem;
using rAspCoreVueLauncher.Shared.Filesystem;

namespace rAspCoreVueLauncher.Api.Tests;

[TestClass]
public class FilesystemServiceTests
{
    private static string NewTempFile()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, "x");
        return path;
    }

    [TestMethod]
    public void Delete_SoftDelete_RoutesToTrash()
    {
        var trash = Substitute.For<IFileTrash>();
        trash.IsSupported.Returns(true);
        var service = new FilesystemService(trash);
        var path = NewTempFile();
        try
        {
            service.Delete(new DeleteRequest(path, Permanent: false));

            trash.Received(1).TrashFile(path);
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void Delete_Permanent_BypassesTrash()
    {
        var trash = Substitute.For<IFileTrash>();
        trash.IsSupported.Returns(true);
        var service = new FilesystemService(trash);
        var path = NewTempFile();

        service.Delete(new DeleteRequest(path, Permanent: true));

        trash.DidNotReceive().TrashFile(Arg.Any<string>());
        File.Exists(path).Should().BeFalse();
    }

    [TestMethod]
    public void Delete_SoftDelete_OnUnsupportedTrash_Throws()
    {
        var trash = Substitute.For<IFileTrash>();
        trash.IsSupported.Returns(false);
        var service = new FilesystemService(trash);
        var path = NewTempFile();
        try
        {
            var act = () => service.Delete(new DeleteRequest(path, Permanent: false));

            act.Should().Throw<FilesystemException>()
                .Which.Error.Should().Be(FilesystemError.TrashUnsupported);
            File.Exists(path).Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void Delete_MissingPath_ThrowsNotFound()
    {
        var service = new FilesystemService(Substitute.For<IFileTrash>());
        var act = () => service.Delete(new DeleteRequest(
            Path.Combine(Path.GetTempPath(), "does-not-exist-" + Path.GetRandomFileName()), false));

        act.Should().Throw<FilesystemException>()
            .Which.Error.Should().Be(FilesystemError.NotFound);
    }
}
