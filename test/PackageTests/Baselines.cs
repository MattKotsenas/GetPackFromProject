using System.Reflection;

namespace GetPackFromProject.PackageTests;

[TestClass]
public partial class Baselines
{
    [TestMethod]
    public Task Match()
    {
        DirectoryInfo workingDirectory = GetWorkingDirectory();

        FileInfo package = workingDirectory.GetFiles("GetPackFromProject.*.nupkg").OrderByDescending(f => f.LastWriteTimeUtc).First();

        return VerifyFile(package).ScrubNuspec();
    }

    private DirectoryInfo GetWorkingDirectory()
    {
        string location = Assembly.GetExecutingAssembly().Location;

        return new FileInfo(location).Directory ?? throw new InvalidOperationException("Could not find the directory of the current assembly.");
    }
}