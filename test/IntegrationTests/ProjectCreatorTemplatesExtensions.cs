using Microsoft.Build.Utilities.ProjectCreation;

namespace GetPackFromProject.IntegrationTests;

internal static class ProjectCreatorTemplatesExtensions
{
    public static ProjectCreator ProjectThatProducesAPackage(this ProjectCreatorTemplates templates, bool generatePackageOnBuild, string[] targetFrameworks)
    {
        ProjectCreator project = templates
            .SdkCsproj(targetFrameworks)
            .Property("Version", "1.0.0-deadbeef");

        if (generatePackageOnBuild)
        {
            project.Property("GeneratePackageOnBuild", "true");
        }

        return project;
    }
}
