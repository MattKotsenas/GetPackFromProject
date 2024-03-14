using System.Reflection;

namespace GetPackFromProject.PackageTests;

public class Baselines
{
    private static readonly VerifySettings VerifySettings = new VerifySettings().ScrubNuspec();

    [Fact]
    public Task Match()
    {
        FileInfo package = GetWorkingDirectory().GetFiles("GetPackFromProject.*.nupkg").OrderByDescending(f => f.LastWriteTimeUtc).First();

        return VerifyFile(package, VerifySettings);
    }

    private static DirectoryInfo GetWorkingDirectory()
    {
        string location = Assembly.GetExecutingAssembly().Location;
        return new FileInfo(location).Directory ?? throw new InvalidOperationException("Could not find the directory of the current assembly.");
    }
}