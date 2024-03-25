using FluentAssertions;

using Microsoft.Build.Execution;
using Microsoft.Build.Utilities.ProjectCreation;

using Xunit.Abstractions;

namespace GetPackFromProject.IntegrationTests;

// TODO: WhenTheConfigurationIsCorrect_ShouldPassWhenLeafProjectHasProperty fails
// when it is run by itself in multi-targeting scenarios

public class GivenAProjectWithAProjectReference: TestBase
{
    protected FileInfo Package { get; private set; }

    public GivenAProjectWithAProjectReference(ITestOutputHelper logger) : base(logger)
    {
        logger.WriteLine($"Enumerating files:{string.Join("\n\t", WorkingDirectory.GetFiles().Select(f => f.FullName))}");

        Package = WorkingDirectory.GetFiles("GetPackFromProject.*.nupkg").OrderByDescending(f => f.LastWriteTimeUtc).First();
    }

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net7.0", "net8.0")]
    public void WhenTheLeafDoesNotGenerateAPackageOnBuild_ItShouldWarn(params string[] targetFrameworks)
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: false, targetFrameworks).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator.Templates
                .SdkCsproj(targetFrameworks)
                .Property("GetPackFromProject_CopyToOutputDirectory", "Never")
                .ItemPackageReference(package)
                .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                {
                    { "AddPackageAsOutput", "true" }
                })
                .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"))
                .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.WarningEvents.Should()
                .HaveCount(1)
                .And.AllSatisfy(warning => warning.Code.Should().Be("GPP001"));

            buildOutput.Errors.Should().BeEmpty();
            result.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net7.0", "net8.0")]
    public void WhenTheLeafDoesNotGenerateAPackageOnBuild_WhenWarningsAreErrorsItShouldError(params string[] targetFrameworks)
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: false, targetFrameworks).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator.Templates
                .SdkCsproj(targetFrameworks)
                .ItemPackageReference(package)
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
    [InlineData("net8.0")]
    [InlineData("net7.0", "net8.0")]
    public void WhenThePackageReferencePropertyIsNotUsed_ItShouldPass(params string[] targetFrameworks)
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: false, targetFrameworks).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator.Templates
                .SdkCsproj(targetFrameworks)
                .ItemPackageReference(package)
                .ItemProjectReference(leafProject)
                .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"))
                .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.ErrorEvents.Should().BeEmpty();
            buildOutput.WarningEvents.Should().BeEmpty();

            result.Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net7.0", "net8.0")]
    public void WhenTheConfigurationIsCorrect_ShouldPassWhenLeafProjectHasProperty(params string[] targetFrameworks)
    {
        string contentHasMetadata = "Content has metadata:";
        string projectsWithMetadata = "ProjectReferences with metadata:";
        string projectMetadata = "ProjectReferencesMetadata:";
        char sep = Path.DirectorySeparatorChar;

        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: true, targetFrameworks).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator main = ProjectCreator.Templates
                .SdkCsproj(targetFrameworks)
                .ItemPackageReference(package)
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
                .Where(message => message == $"{contentHasMetadata}{Temp.FullName}{sep}Leaf{sep}bin{sep}Debug{sep}Leaf.1.0.0-deadbeef.nupkg")
                .Should().HaveCount(targetFrameworks.Length);
            buildOutput.Messages
                .Where(message => message == $"{projectsWithMetadata}{leafProject.FullPath}")
                .Should().HaveCount(targetFrameworks.Length);
            buildOutput.Messages
                .Where(message => message == $"{projectMetadata}{Temp.FullName}{sep}Leaf{sep}bin{sep}Debug{sep}Leaf.1.0.0-deadbeef.nupkg;{Temp.FullName}{sep}Leaf{sep}obj{sep}Debug{sep}Leaf.1.0.0-deadbeef.nuspec")
                .Should()
                .HaveCount(targetFrameworks.Length);

            result.Should().BeTrue();

            string binDir = Path.Combine(Temp.FullName, "Sample", "bin", "Debug");
            Directory.GetFiles(binDir, "Leaf*.nupkg", SearchOption.AllDirectories)
                .Should()
                .HaveCount(targetFrameworks.Length, "there should be a .nupkg per output directory");
        }
    }

    //[Theory]
    //[InlineData("net8.0")]
    //[InlineData("net7.0", "net8.0")]
    //public void WhenTheConfigurationIsCorrect_ShouldHandleAndCleanupLockFiles(params string[] targetFrameworks)
    //{
    //    using (PackageRepository.Create(Temp.FullName)
    //        .Package(Package, out Package package))
    //    {
    //        ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: true, targetFrameworks).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

    //        ProjectCreator main = ProjectCreator.Templates
    //            .SdkCsproj(targetFrameworks)
    //            .PropertyGroup()
    //                .Property("EnableSimulateLock", "true")
    //                .Property("GetPackFromProject_LockMaxRetries", "1")
    //                .Property("GetPackFromProject_LockSleepSeconds", "0")
    //            .ItemPackageReference(package)
    //            .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
    //            {
    //                { "AddPackageAsOutput", "true" }
    //            })
    //            .Target("SimulateLockFile", beforeTargets: "BeforeBuild", condition: "('$(IsInnerBuild)' == 'true' OR '$(TargetFrameworks)' == '') AND '$(EnableSimulateLock)' == 'true'")
    //                .Task(name: "Message", parameters: new Dictionary<string, string?>
    //                {
    //                    { "Text", $"Simulating lock file at path '$(BaseIntermediateOutputPath)\\GetPackFromProject.lock'." },
    //                    { "Importance", "Low" }
    //                })
    //                .Task(name: "WriteLinesToFile", parameters: new Dictionary<string, string?>
    //                {
    //                    { "File", "$(BaseIntermediateOutputPath)\\GetPackFromProject.lock" },
    //                    { "Lines", "this-simulates-a-build-already-in-progress" }
    //                })
    //            .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"));

    //        main.TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

    //        buildOutput.ErrorEvents
    //            .Where(error =>
    //                error.Message is not null &&
    //                error.Message.StartsWith("Unable to acquire lock file") &&
    //                error.Message.EndsWith("after '1' tries."))
    //            .Should()
    //            .HaveCount(targetFrameworks.Length);
    //        buildOutput.WarningEvents.Should().BeEmpty();

    //        result.Should().BeFalse();

    //        // In failure cases, lock files remain
    //        string[] lockPaths = targetFrameworks.Select(tf => Path.Combine(Temp.FullName, "Sample", "obj", "GetPackFromProject.lock")).ToArray();
    //        lockPaths.Should().OnlyContain(lockPath => File.Exists(lockPath), "lock files should exist");

    //        // Clean deletes a lock file
    //        main.TryBuild(target: "Clean", out result, out buildOutput);
    //        buildOutput.ErrorEvents.Should().BeEmpty();
    //        result.Should().BeTrue();
    //        lockPaths.Should().OnlyContain(lockPath => !File.Exists(lockPath), "lock files should be deleted");

    //        // In success cases, the lock file is gone
    //        Dictionary<string, string> properties = new()
    //        {
    //            { "EnableSimulateLock", "false" }
    //        };
    //        main.TryBuild(target: "Build", properties, out result, out buildOutput);
    //        buildOutput.ErrorEvents.Should().BeEmpty();
    //        result.Should().BeTrue();
    //        lockPaths.Should().OnlyContain(lockPath => !File.Exists(lockPath), "lock files should be deleted");
    //    }
    //}
}
