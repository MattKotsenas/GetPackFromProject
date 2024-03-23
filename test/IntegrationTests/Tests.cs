using System.Globalization;
using System.Linq.Expressions;
using System;

using FluentAssertions;
using FluentAssertions.Execution;

using Microsoft.Build.Execution;
using Microsoft.Build.Utilities.ProjectCreation;

using Xunit.Abstractions;
using FluentAssertions.Collections;
using FluentAssertions.Primitives;

namespace GetPackFromProject.IntegrationTests;

public class GivenAProjectWithAProjectReference: TestBase
{
    protected FileInfo Package { get; private set; }

    public GivenAProjectWithAProjectReference(ITestOutputHelper logger)
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
                .HaveCount(targetFrameworks.Length)
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
                .HaveCount(targetFrameworks.Length)
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

            ProjectCreator.Templates
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
                .Save(Path.Combine(Temp.FullName, "Sample", $"Sample.csproj"))
                .TryBuild(restore: true, target: "Build", out bool result, out BuildOutput buildOutput, out IDictionary<string, TargetResult>? outputs);

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
}


//internal class CollectionContainsExtensionsAssertions<TCollection, T, TAssertions> : ReferenceTypeAssertions<TCollection, TAssertions>
//    where TCollection : IEnumerable<T>
//    where TAssertions : CollectionContainsExtensionsAssertions<TCollection, T, TAssertions>
//{
//    public CollectionContainsExtensionsAssertions(TCollection actualValue)
//        : base(actualValue)
//    {
//    }

//    protected override string Identifier => "collection";

//    // TODO: Fix xmldocs

//    /// <summary>
//    /// Expects the current collection to contain only a single item matching the specified <paramref name="predicate"/>.
//    /// </summary>
//    /// <param name="predicate">The predicate that will be used to find the matching items.</param>
//    /// <param name="because">
//    /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
//    /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
//    /// </param>
//    /// <param name="becauseArgs">
//    /// Zero or more objects to format using the placeholders in <paramref name="because" />.
//    /// </param>
//    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
//    public AndWhichConstraint<TAssertions, T> Contain(Expression<Func<T, bool>> predicate, OccurrenceConstraint occurrence,
//        string because = "", params object[] becauseArgs)
//    {
//        if (predicate is null) { throw new ArgumentNullException(nameof(predicate)); }

//        const string expectationPrefix =
//            "Expected {context:collection} to contain a single item matching {0}{reason}, ";

//        bool success = Execute.Assertion
//            .BecauseOf(because, becauseArgs)
//            .ForCondition(Subject is not null)
//            .FailWith(expectationPrefix + "but found <null>.", predicate);

//        T[] matches = Array.Empty<T>();

//        if (success)
//        {
//            ICollection<T> actualItems = (Subject is not null) ? Subject.ToList() : [];//.ConvertOrCastToCollection();

//            Execute.Assertion
//                .ForCondition(actualItems.Count > 0)
//                .BecauseOf(because, becauseArgs)
//                .FailWith(expectationPrefix + "but the collection is empty.", predicate);

//            matches = actualItems.Where(predicate.Compile()).ToArray();
//            int count = matches.Length;

//            Execute.Assertion
//                .ForConstraint(occurrence, count)
//                .FailWith(expectationPrefix + "but " + count.ToString(CultureInfo.InvariantCulture) + " such items were found.");
//        }

//        return new AndWhichConstraint<TAssertions, T>((TAssertions)this, matches);
//    }
//}