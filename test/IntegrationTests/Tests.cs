using FluentAssertions;
using Microsoft.Build.Execution;
using Microsoft.Build.Utilities.ProjectCreation;

namespace GetPackFromProject.IntegrationTests;

[TestClass]

public class GivenAProjectWithAProjectReference: TestBase
{
    public static IEnumerable<object[]> TargetFrameworkData
    {
        get
        {
            foreach (bool useArtifacts in new[] { true, false })
            {
                yield return new object[] { new[] { "net8.0" }, new[] { "net7.0" }, useArtifacts };
                yield return new object[] { new[] { "net8.0" }, new[] { "net7.0", "net8.0" }, useArtifacts };
                yield return new object[] { new[] { "net7.0", "net8.0" }, new[] { "net7.0" }, useArtifacts };
                yield return new object[] { new[] { "net7.0", "net8.0" }, new[] { "net7.0", "net8.0" }, useArtifacts };
            }
        }
    }

    protected FileInfo Package { get; private set; }

    public GivenAProjectWithAProjectReference()
    {
        Package = WorkingDirectory.GetFiles("GetPackFromProject.*.nupkg").OrderByDescending(f => f.LastWriteTimeUtc).First();
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworkData))]
    public void WhenTheLeafDoesNotGenerateAPackageOnBuild_ItShouldWarn(string[] mainTfms, string[] leafTfms, bool useArtifactsOutput)
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator.Templates.DirectoryBuildProps(Temp, useArtifactsOutput);

            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(Temp, leafTfms, generatePackageOnBuild: false);

            ProjectCreator main = ProjectCreator.Templates
                .MainProject(Temp, mainTfms, package)
                .Property("GetPackFromProject_CopyToOutputDirectory", "Never")
                .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                {
                    { "AddPackageAsOutput", "true" }
                })
                .Save();

            main.TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.WarningEvents.Should()
                .HaveCount(1)
                .And.AllSatisfy(warning => warning.Code.Should().Be("GPP001"));

            buildOutput.Errors.Should().BeEmpty();
            result.Should().BeTrue();
        }
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworkData))]
    public void WhenTheLeafDoesNotGenerateAPackageOnBuild_WhenWarningsAreErrorsItShouldError(string[] mainTfms, string[] leafTfms, bool useArtifactsOutput)
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator.Templates.DirectoryBuildProps(Temp, useArtifactsOutput);

            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(Temp, leafTfms, generatePackageOnBuild: false);

            ProjectCreator.Templates
                .MainProject(Temp, mainTfms, package)
                .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                {
                    { "AddPackageAsOutput", "true" }
                })
                .Property("MSBuildTreatWarningsAsErrors", "true")
                .Save()
                .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.ErrorEvents.Should()
                .HaveCount(1)
                .And.AllSatisfy(error => error.Code.Should().Be("GPP001"));

            result.Should().BeFalse();
        }
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworkData))]
    public void WhenThePackageReferencePropertyIsNotUsed_ItShouldPass(string[] mainTfms, string[] leafTfms, bool useArtifactsOutput)
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator.Templates.DirectoryBuildProps(Temp, useArtifactsOutput);

            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(Temp, leafTfms, generatePackageOnBuild: false);

            ProjectCreator.Templates
                .MainProject(Temp, mainTfms, package)
                .ItemProjectReference(leafProject)
                .Save()
                .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.ErrorEvents.Should().BeEmpty();
            buildOutput.WarningEvents.Should().BeEmpty();

            result.Should().BeTrue();
        }
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworkData))]
    public void WhenTheConfigurationIsCorrect_ShouldPassWhenLeafProjectHasProperty(string[] mainTfms, string[] leafTfms, bool useArtifactsOutput)
    {
        string contentHasMetadata = "Content has metadata:";
        string projectsWithMetadata = "ProjectReferences with metadata:";
        string projectMetadata = "ProjectReferencesMetadata:";

        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator.Templates.DirectoryBuildProps(Temp, useArtifactsOutput);

            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(Temp, leafTfms, generatePackageOnBuild: true);

            ProjectCreator main = ProjectCreator.Templates
                .MainProject(Temp, mainTfms, package)
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
                .Save();

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
        return useArtifactsOutput
            ? Path.Combine(directory.FullName, "artifacts", "bin", "Sample")
            : Path.Combine(directory.FullName, "Sample", "bin", "Debug");
    }

    private static string GenerateLeafNupkgPath(bool useArtifactsOutput, DirectoryInfo directory)
    {
        return useArtifactsOutput
            ? Path.Combine(directory.FullName, "artifacts", "package", "debug", "Leaf.1.0.0-deadbeef.nupkg")
            : Path.Combine(directory.FullName, "Leaf", "bin", "Debug", "Leaf.1.0.0-deadbeef.nupkg");
    }

    private static string GenerateLeafNuspecPath(bool useArtifactsOutput, DirectoryInfo directory)
    {
        return useArtifactsOutput
            ? Path.Combine(directory.FullName, "artifacts", "obj", "Leaf", "Debug", "Leaf.1.0.0-deadbeef.nuspec")
            : Path.Combine(directory.FullName, "Leaf", "obj", "Debug", "Leaf.1.0.0-deadbeef.nuspec");
    }
}
