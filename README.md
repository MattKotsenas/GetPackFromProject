# GetPackFromProject

An MSBuild task helper to simplify testing NuGet packages. To use, first install the package XXXX, then add the metadata
`AddPackageAsOutput=true` to any `<ProjectReference>` items like this:

```xml
<ItemGroup>
  <ProjectReference Include="..\MyPackage\MyPackage.csproj" AddPackageAsOutput="true" />
</ItemGroup>
```

Adding that metadata will do a few things:

1. Ensuring the package is generated on every build

To avoid working with stale packages, the build will validate that any projects with this metadata have the `GeneratePackageOnBuild`
property set (by default, a project only creates a package when you run the Pack target).

2. Add the outputs of the pack operation (e.g. .nupkg and .nuspec files) as metadata on the `<ProjectReference>`

3. Add all .nupkg files as `<Content>` items for your build

This ensures that the packages can be copied to your output directory for tests.