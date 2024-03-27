using System.Collections;

using FluentAssertions;

using Microsoft.Build.Execution;
using Microsoft.Build.Utilities.ProjectCreation;

using Xunit.Abstractions;

namespace GetPackFromProject.IntegrationTests;

public class TargetFrameworkData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        foreach (bool useArtifacts in new[] { true, false })
        {
            yield return new object[] { new[] { "net8.0" }, new[] { "net7.0" }, useArtifacts };
            yield return new object[] { new[] { "net8.0" }, new[] { "net7.0", "net8.0" }, useArtifacts };
            yield return new object[] { new[] { "net7.0", "net8.0" }, new[] { "net7.0" }, useArtifacts };
            yield return new object[] { new[] { "net7.0", "net8.0" }, new[] { "net7.0", "net8.0" }, useArtifacts };
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public class GivenAProjectWithAProjectReference: TestBase
{
    protected FileInfo Package { get; private set; }

    public GivenAProjectWithAProjectReference(ITestOutputHelper logger) : base(logger)
    {
        logger.WriteLine($"Enumerating files:{string.Join("\n\t", WorkingDirectory.GetFiles().Select(f => f.FullName))}");

        Package = WorkingDirectory.GetFiles("GetPackFromProject.*.nupkg").OrderByDescending(f => f.LastWriteTimeUtc).First();
    }

    [Theory]
    [ClassData(typeof(TargetFrameworkData))]
    public void WhenTheLeafDoesNotGenerateAPackageOnBuild_ItShouldWarn(string[] mainTfms, string[] leafTfms, bool useArtifactsOutput)
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator.Templates.DirectoryBuildProps(Temp, useArtifactsOutput);

            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(leafTfms, generatePackageOnBuild: false).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator main = ProjectCreator.Templates
                .MainProject(mainTfms, package)
                .Property("GetPackFromProject_CopyToOutputDirectory", "Never")
                .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                {
                    { "AddPackageAsOutput", "true" }
                })
                .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"));

            main.TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.WarningEvents.Should()
                .HaveCount(1)
                .And.AllSatisfy(warning => warning.Code.Should().Be("GPP001"));

            buildOutput.Errors.Should().BeEmpty();
            result.Should().BeTrue();
        }
    }

    [Theory]
    [ClassData(typeof(TargetFrameworkData))]
    public void WhenTheLeafDoesNotGenerateAPackageOnBuild_WhenWarningsAreErrorsItShouldError(string[] mainTfms, string[] leafTfms, bool useArtifactsOutput)
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator.Templates.DirectoryBuildProps(Temp, useArtifactsOutput);

            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(leafTfms, generatePackageOnBuild: false).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator.Templates
                .MainProject(mainTfms, package)
                .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                {
                    { "AddPackageAsOutput", "true" }
                })
                .Property("MSBuildTreatWarningsAsErrors", "true")
                .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"))
                .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.ErrorEvents.Should()
                .HaveCount(1)
                .And.AllSatisfy(error => error.Code.Should().Be("GPP001"));

            result.Should().BeFalse();
        }
    }

    [Theory]
    [ClassData(typeof(TargetFrameworkData))]
    public void WhenThePackageReferencePropertyIsNotUsed_ItShouldPass(string[] mainTfms, string[] leafTfms, bool useArtifactsOutput)
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator.Templates.DirectoryBuildProps(Temp, useArtifactsOutput);

            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(leafTfms, generatePackageOnBuild: false).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator.Templates
                .MainProject(mainTfms, package)
                .ItemProjectReference(leafProject)
                .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"))
                .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.ErrorEvents.Should().BeEmpty();
            buildOutput.WarningEvents.Should().BeEmpty();

            result.Should().BeTrue();
        }
    }

    [Theory]
    [ClassData(typeof(TargetFrameworkData))]
    public void WhenTheConfigurationIsCorrect_ShouldPassWhenLeafProjectHasProperty(string[] mainTfms, string[] leafTfms, bool useArtifactsOutput)
    {
        string contentHasMetadata = "Content has metadata:";
        string projectsWithMetadata = "ProjectReferences with metadata:";
        string projectMetadata = "ProjectReferencesMetadata:";

        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator.Templates.DirectoryBuildProps(Temp, useArtifactsOutput);

            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(leafTfms, generatePackageOnBuild: true).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator main = ProjectCreator.Templates
                .MainProject(mainTfms, package)
                .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                {
                    { "AddPackageAsOutput", "true" }
                })
                .Target("PrintContentItemsTestValidation", afterTargets: "Build", condition: "'$(IsInnerBuild)' == 'true' OR '$(TargetFrameworks)' == ''")
                    .Task(name: "Message", parameters: new Dictionary<string, string?>
                    {
                        { "Text", $"{contentHasMetadata}@(Content->WithMetadataValue('IsPackageFromProjectReference', 'true'))" },
                        { "Importance", "High" }
                    })
                    .Task(name: "Message", parameters: new Dictionary<string, string?>
                    {
                        { "Text", $"{projectsWithMetadata}@(ProjectReference->HasMetadata('PackageOutputs'))" },
                        { "Importance", "High" }
                    })
                    .Task(name: "Message", parameters: new Dictionary<string, string?>
                    {
                        { "Text", $"{projectMetadata}%(ProjectReference.PackageOutputs)" },
                        { "Importance", "High" }
                    })
                .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"));

            main.TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.ErrorEvents.Should().BeEmpty();
            buildOutput.WarningEvents.Should().BeEmpty();

            buildOutput.Messages
                .Where(message => message == $"{contentHasMetadata}{GenerateLeafNupkgPath(useArtifactsOutput, Temp)}")
                .Should().HaveCount(mainTfms.Length);
            buildOutput.Messages
                .Where(message => message == $"{projectsWithMetadata}{leafProject.FullPath}")
                .Should().HaveCount(mainTfms.Length);
            buildOutput.Messages
                .Where(message => message == $"{projectMetadata}{GenerateLeafNupkgPath(useArtifactsOutput, Temp)};{GenerateLeafNuspecPath(useArtifactsOutput, Temp)}")
                .Should()
                .HaveCount(mainTfms.Length);

            result.Should().BeTrue();

            string binDir = Path.Combine(GenerateMainPackageDirectory(Temp, useArtifactsOutput));
            Directory.GetFiles(binDir, "Leaf*.nupkg", SearchOption.AllDirectories)
                .Should()
                .HaveCount(mainTfms.Length, "there should be a .nupkg per output directory");
        }
    }

    private static string GenerateMainPackageDirectory(DirectoryInfo directory, bool useArtifactsOutput)
    {
        if (useArtifactsOutput)
        {
            return Path.Combine(directory.FullName, "artifacts", "bin", "Sample");
        }

        return Path.Combine(directory.FullName, "Sample", "bin", "Debug");
    }

    private static string GenerateLeafNupkgPath(bool useArtifactsOutput, DirectoryInfo directory)
    {
        char sep = Path.DirectorySeparatorChar;

        if (useArtifactsOutput)
        {
            return $"{directory.FullName}{sep}artifacts{sep}package{sep}debug{sep}Leaf.1.0.0-deadbeef.nupkg";
        }

        return $"{directory.FullName}{sep}Leaf{sep}bin{sep}Debug{sep}Leaf.1.0.0-deadbeef.nupkg";
    }

    private static string GenerateLeafNuspecPath(bool useArtifactsOutput, DirectoryInfo directory)
    {
        char sep = Path.DirectorySeparatorChar;

        if (useArtifactsOutput)
        {
            return $"{directory.FullName}{sep}artifacts{sep}obj{sep}Leaf{sep}Debug{sep}Leaf.1.0.0-deadbeef.nuspec";
        }

        return $"{directory.FullName}{sep}Leaf{sep}obj{sep}Debug{sep}Leaf.1.0.0-deadbeef.nuspec";
    }
}
