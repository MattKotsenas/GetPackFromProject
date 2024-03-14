using Microsoft.Build.Utilities.ProjectCreation;

namespace GetPackFromProject.IntegrationTests;

internal static class ProjectCreatorTemplatesExtensions
{
    public static ProjectCreator ProjectThatProducesAPackage(this ProjectCreatorTemplates templates, bool generatePackageOnBuild, string targetFramework = "net8.0")
    {
        ProjectCreator project = templates
            .SdkCsproj(targetFramework: targetFramework);

        if (generatePackageOnBuild)
        {
            project.Property("GeneratePackageOnBuild", "true");
        }

        return project;
    }

    public static ProjectCreator ProjectThatImportsTargets(this ProjectCreatorTemplates templates, DirectoryInfo buildBasePath, Action<ProjectCreator> customAction, string targetFramework = "net8.0")
    {
        return templates.SdkCsproj(targetFramework: targetFramework)
            .Import(Path.Combine(buildBasePath.FullName, "build", "GetPackFromProject.props"))
            .CustomAction(customAction)
            .Import(Path.Combine(buildBasePath.FullName, "build", "GetPackFromProject.targets"));
    }
}
