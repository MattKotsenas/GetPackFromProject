![Icon](https://raw.githubusercontent.com/MattKotsenas/GetPackFromProject/main/icon.png)

# GetPackFromProject

[![Build status](https://github.com/MattKotsenas/GetPackFromProject/actions/workflows/main.yml/badge.svg)](https://github.com/MattKotsenas/GetPackFromProject/actions/workflows/main.yml)
[![Nuget](https://img.shields.io/nuget/v/GetPackFromProject)](https://nuget.org/packages/GetPackFromProject)
[![Downloads](https://img.shields.io/nuget/dt/GetPackFromProject)](https://nuget.org/packages/GetPackFromProject)

An MSBuild task / helper to simplify testing NuGet packages by automatically ensuring the latest package is built and
placed in the output directory for test projects. To use, first install the package, then add the metadata
`AddPackageAsOutput=true` to any `<ProjectReference>` items like this:

```xml
<ItemGroup>
  <ProjectReference Include="..\MyPackage\MyPackage.csproj" AddPackageAsOutput="true" />
</ItemGroup>
```

Adding that metadata will do a few things:

1. Ensure the package is generated on every build

To avoid working with stale packages, the build will validate that any projects with this metadata have the `GeneratePackageOnBuild`
property set (by default, a project only creates a package when you run the Pack target).

2. Add the outputs of the pack operation (e.g. .nupkg and .nuspec files) as metadata on the `<ProjectReference>`

3. Add all .nupkg files as `<Content>` items for your build

This ensures that the packages can be copied to your output directory for tests.

## Finding the package in tests

Add this snippet to your unit tests to get the path to an output NuGet package:

```csharp
FileInfo package = new(Assembly.GetExecutingAssembly().Location)
    .Directory!
    .GetFiles("NameOfNuGetPackageToTest*.nupkg")
    .OrderByDescending(f => f.LastWriteTimeUtc)
    .First()
```
