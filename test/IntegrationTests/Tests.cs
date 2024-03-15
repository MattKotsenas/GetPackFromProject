using FluentAssertions;
using Microsoft.Build.Execution;
using Microsoft.Build.Utilities.ProjectCreation;

namespace GetPackFromProject.IntegrationTests;

public class GivenAProjectWithAProjectReference: TestBase
{
    protected FileInfo Package { get; private set; }

    public GivenAProjectWithAProjectReference()
    {
        Package = WorkingDirectory.GetFiles("GetPackFromProject.*.nupkg").OrderByDescending(f => f.LastWriteTimeUtc).First();
    }

    [Fact]
    public void WhenTheLeafDoesNotGenerateAPackageOnBuild_ItShouldWarn()
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: false).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator.Templates
                .SdkCsproj(targetFramework: "net8.0")
                .ItemPackageReference(package)
                .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                {
                    { "AddPackageAsOutput", "true" }
                })
                .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"))
                .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.Errors.Should().BeEmpty();
            result.Should().BeTrue();

            buildOutput.WarningEvents.Should().HaveCount(1).And.Subject.Single().Code.Should().Be("GPP001");
        }
    }

    [Fact]
    public void WhenTheLeafDoesNotGenerateAPackageOnBuild_WhenWarningsAreErrorsItShouldError()
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: false).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator.Templates
                .SdkCsproj(targetFramework: "net8.0")
                .ItemPackageReference(package)
                .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                {
                    { "AddPackageAsOutput", "true" }
                })
                .Property("MSBuildTreatWarningsAsErrors", "true")
                .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"))
                .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.ErrorEvents.Should().HaveCount(1).And.Subject.Single().Code.Should().Be("GPP001");

            result.Should().BeFalse();
        }
    }

    [Fact]
    public void WhenThePackageReferencePropertyIsNotUsed_ItShouldPass()
    {
        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: false).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator.Templates
                .SdkCsproj(targetFramework: "net8.0")
                .ItemPackageReference(package)
                .ItemProjectReference(leafProject)
                .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"))
                .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.ErrorEvents.Should().BeEmpty();
            buildOutput.WarningEvents.Should().BeEmpty();

            result.Should().BeTrue();
        }
    }

    [Fact]
    public void WhenTheConfigurationIsCorrect_ShouldPassWhenLeafProjectHasProperty()
    {
        string contentHasMetadata = "Content has metadata:";
        string projectsWithMetadata = "ProjectReferences with metadata:";
        string projectMetadata = "ProjectReferencesMetadata:";

        using (PackageRepository.Create(Temp.FullName)
            .Package(Package, out Package package))
        {
            ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: true).Save(Path.Combine(Temp.FullName, "Leaf", "Leaf.csproj"));

            ProjectCreator.Templates
                .SdkCsproj(targetFramework: "net8.0")
                .ItemPackageReference(package)
                .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                {
                    { "AddPackageAsOutput", "true" }
                })
                .Target("PrintContentItemsTestValidation", afterTargets: "Build")
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
                .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"))
                .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

            buildOutput.ErrorEvents.Should().BeEmpty();
            buildOutput.WarningEvents.Should().BeEmpty();

            buildOutput.Messages
                .Should()
                .ContainSingle(message => message.StartsWith(contentHasMetadata))
                .Which.Should()
                .MatchEquivalentOf($"{contentHasMetadata}{Temp.FullName}\\Leaf\\bin\\Debug\\Leaf.1.0.0-deadbeef.nupkg");
            buildOutput.Messages
                .Should()
                .ContainSingle(message => message.StartsWith(projectsWithMetadata))
                .Which.Should()
                .MatchEquivalentOf($"{projectsWithMetadata}{leafProject.FullPath}");
            buildOutput.Messages
                .Should()
                .ContainSingle(message => message.StartsWith(projectMetadata))
                .Which.Should()
                .MatchEquivalentOf($"{projectMetadata}{Temp.FullName}\\Leaf\\bin\\Debug\\Leaf.1.0.0-deadbeef.nupkg;{Temp.FullName}\\Leaf\\obj\\Debug\\Leaf.1.0.0-deadbeef.nuspec");

            result.Should().BeTrue();
        }
    }
}