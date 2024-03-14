using FluentAssertions;
using Microsoft.Build.Execution;
using Microsoft.Build.Utilities.ProjectCreation;

namespace GetPackFromProject.IntegrationTests;

public class GivenAProjectWithAProjectReference: TestBase
{
    public class WhenTheLeafDoesNotGenerateAPackageOnBuild : GivenAProjectWithAProjectReference
    {
        [Fact]
        public void ItShouldWarn()
        {
            {
                using (PackageRepository.Create(Temp.FullName))
                {
                    ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: false).Save(Path.Combine(Temp.FullName, "Leaf.csproj"));

                    ProjectCreator.Templates.ProjectThatImportsTargets(WorkingDirectory, creator => creator
                        .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                        {
                        { "AddPackageAsOutput", "true" }
                        }))
                        .Save(Path.Combine(Temp.FullName, $"Sample.csproj"))
                        .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

                    buildOutput.Errors.Should().BeEmpty();
                    result.Should().BeTrue();

                    buildOutput.WarningEvents.Should().HaveCount(1).And.Subject.Single().Code.Should().Be("GPP001");
                }
            }
        }

        [Fact]
        public void WhenWarningsAreErrorsItShouldError()
        {
            {
                using (PackageRepository.Create(Temp.FullName))
                {
                    ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: false).Save(Path.Combine(Temp.FullName, "Leaf.csproj"));

                    ProjectCreator.Templates.ProjectThatImportsTargets(WorkingDirectory, creator => creator
                        .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                        {
                         { "AddPackageAsOutput", "true" }
                        })
                        .Property("MSBuildTreatWarningsAsErrors", "true"))
                        .Save(Path.Combine(Temp.FullName, $"Sample.csproj"))
                        .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

                    buildOutput.ErrorEvents.Should().HaveCount(1).And.Subject.Single().Code.Should().Be("GPP001");

                    result.Should().BeFalse();
                }
            }
        }
    }

    public class WhenThePackageReferencePropertyIsNotUsed : GivenAProjectWithAProjectReference
    {
        [Fact]
        public void ItShouldPass()
        {
            {
                using (PackageRepository.Create(Temp.FullName))
                {
                    ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: false).Save(Path.Combine(Temp.FullName, "Leaf.csproj"));

                    ProjectCreator.Templates.ProjectThatImportsTargets(WorkingDirectory, creator => creator
                        .ItemProjectReference(leafProject))
                        .Save(Path.Combine(Temp.FullName, $"Sample.csproj"))
                        .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

                    buildOutput.ErrorEvents.Should().BeEmpty();
                    buildOutput.WarningEvents.Should().BeEmpty();

                    result.Should().BeTrue();
                }
            }
        }
    }

    public class WhenTheConfigurationIsCorrect : GivenAProjectWithAProjectReference
    {
        [Fact]
        public void ShouldPassWhenLeafProjectHasProperty()
        {
            string fromProjectReferenceMetadata = "Content has metadata:";
            string projectMetadata = "ProjectReference has metadata:";

            {
                using (PackageRepository.Create(Temp.FullName))
                {
                    ProjectCreator leafProject = ProjectCreator.Templates.ProjectThatProducesAPackage(generatePackageOnBuild: true).Save(Path.Combine(Temp.FullName, "Leaf.csproj"));

                    ProjectCreator.Templates.ProjectThatImportsTargets(WorkingDirectory, creator => creator
                        .ItemProjectReference(leafProject, metadata: new Dictionary<string, string?>
                        {
                        { "AddPackageAsOutput", "true" }
                        }))
                        .Target("PrintContentItemsTestValidation", afterTargets: "Build")
                            .Task(name: "Message", parameters: new Dictionary<string, string?>
                            {
                            { "Text", $"{fromProjectReferenceMetadata}@(Content->WithMetadataValue('IsPackageFromProjectReference', 'true'))" },
                            { "Importance", "High" }
                            })
                            .Task(name: "Message", parameters: new Dictionary<string, string?>
                            {
                            { "Text", $"{projectMetadata}@(ProjectReference->HasMetadata('PackageOutputs'))" },
                            { "Importance", "High" }
                            })
                        .Save(Path.Combine(Temp.FullName, $"Sample.csproj"))
                        .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

                    buildOutput.ErrorEvents.Should().BeEmpty();
                    buildOutput.WarningEvents.Should().BeEmpty();

                    buildOutput.Messages
                        .Should()
                        .ContainSingle(message => message.StartsWith(fromProjectReferenceMetadata))
                        .Which.Should()
                        .MatchEquivalentOf($"{fromProjectReferenceMetadata}{Temp.FullName}\\bin\\Debug\\Leaf.1.0.0.nupkg");
                    buildOutput.Messages
                        .Should()
                        .ContainSingle(message => message.StartsWith(projectMetadata))
                        .Which.Should()
                        .MatchEquivalentOf($"{projectMetadata}{leafProject.FullPath}");

                    // TODO: Ensure that the metadata on <ProjectReference> is set correctly

                    result.Should().BeTrue();
                }
            }
        }
    }
}
