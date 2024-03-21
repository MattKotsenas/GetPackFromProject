using Microsoft.Build.Utilities.ProjectCreation;
using System.Runtime.CompilerServices;

#if !NET5_0_OR_GREATER
// Define a module initializer for older .NET frameworks
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class ModuleInitializerAttribute : Attribute { }
}
#endif

namespace GetPackFromProject.IntegrationTests
{
    internal class ModuleInitializer
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            MSBuildAssemblyResolver.Register();
        }
    }
}
