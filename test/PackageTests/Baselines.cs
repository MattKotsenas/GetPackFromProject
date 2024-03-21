using System.Reflection;

using Xunit.Abstractions;

namespace GetPackFromProject.PackageTests;

public class Baselines
{
    private static readonly VerifySettings VerifySettings = new VerifySettings().ScrubNuspec();
    private readonly ITestOutputHelper _logger;

    public Baselines(ITestOutputHelper logger)
    {
        _logger = logger;
    }

    [Fact]
    public Task Match()
    {
        DirectoryInfo workingDirectory = GetWorkingDirectory();
        _logger.WriteLine($"Enumerating files:{string.Join("\n\t", workingDirectory.GetFiles().Select(f => f.FullName))}");

        FileInfo package = workingDirectory.GetFiles("GetPackFromProject.*.nupkg").OrderByDescending(f => f.LastWriteTimeUtc).First();

        return VerifyFile(package, VerifySettings);
    }

    private DirectoryInfo GetWorkingDirectory()
    {
        string location = Assembly.GetExecutingAssembly().Location;

        _logger.WriteLine($"Using assembly location: {location}");

        return new FileInfo(location).Directory ?? throw new InvalidOperationException("Could not find the directory of the current assembly.");
    }
}