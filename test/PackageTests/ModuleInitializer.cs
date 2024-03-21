using System.Runtime.CompilerServices;

namespace GetPackFromProject.PackageTests;

internal class ModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        VerifyNupkg.Initialize();

        UseProjectRelativeDirectory("snapshots");
    }
}
