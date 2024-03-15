using System.Reflection;

namespace GetPackFromProject.IntegrationTests;

public abstract class TestBase : IDisposable
{
    private bool _disposed;

    protected DirectoryInfo Temp { get; private set; }
    protected static DirectoryInfo WorkingDirectory { get; } = GetWorkingDirectory();

    protected TestBase()
    {
        Temp = new DirectoryInfo(GetRandomTempPath());

        Temp.Create();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed state
            }

            // Free unmanaged resources (unmanaged objects) and override finalizer
            TryDeleteDirectory(Temp);

            _disposed = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private static void TryDeleteDirectory(DirectoryInfo directory)
    {
        Exception? lastException = null;

        for (int i = 0; i < 3; i++)
        {
            try
            {
                directory.Delete(recursive: true);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        if (lastException is not null)
        {
            throw new Exception($"Failed to delete temp directory '{directory.FullName}' after multiple retries.", lastException);
        }
    }

    private static string GetRandomTempPath()
    {
        var temp = Path.GetTempPath();
        var fileName = Path.GetRandomFileName();
        return Path.Combine(temp, fileName);
    }

    private static DirectoryInfo GetWorkingDirectory()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string location = GetAssemblyLocation(assembly);

        var dirInfo = new FileInfo(location).Directory ?? throw new Exception($"Unable to get directory from assembly location '{assembly.Location}'.");

        return dirInfo;
    }

    private static string GetAssemblyLocation(Assembly assembly)
    {
#if NETFRAMEWORK
        Uri codebase = new(assembly.CodeBase);
        return Uri.UnescapeDataString(codebase.AbsolutePath);
#elif NETCOREAPP
        return assembly.Location;
#endif
        throw new InvalidOperationException("Unsupported framework.");
    }
}
