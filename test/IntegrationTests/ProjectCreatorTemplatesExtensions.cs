using Microsoft.Build.Evaluation;
using Microsoft.Build.Utilities.ProjectCreation;

namespace GetPackFromProject.IntegrationTests;

internal static class ProjectCreatorTemplatesExtensions
{
    public static ProjectCreator ProjectThatProducesAPackage(this ProjectCreatorTemplates templates, DirectoryInfo directory, string[] targetFrameworks, bool generatePackageOnBuild)
    {
        ProjectCreator project = templates
            .SdkCsproj(targetFrameworks)
            .Property("Version", "1.0.0-deadbeef");

        if (generatePackageOnBuild)
        {
            project.Property("GeneratePackageOnBuild", "true");
        }

        project.Save(Path.Combine(directory.FullName, "Leaf", "Leaf.csproj"));

        return project;
    }

    public static ProjectCreator MainProject(this ProjectCreatorTemplates templates, string[] targetFrameworks, Package package)
    {
        ProjectCreator project = templates
            .SdkCsproj(targetFrameworks)
            .ItemPackageReference(package);

        return project;
    }

    public static ProjectCreator DirectoryBuildProps(this ProjectCreatorTemplates templates, DirectoryInfo directory, bool useArtifactsOutput)
    {
        ProjectCreator project = ProjectCreator.Create(
                    path: Path.Combine(directory.FullName, "Directory.Build.props"),
                    projectFileOptions: NewProjectFileOptions.None);

        if (useArtifactsOutput)
        {
            project.Property("UseArtifactsOutput", "true");
        }

        return project.Save();
    }

}
