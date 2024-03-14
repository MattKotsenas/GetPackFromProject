﻿using Microsoft.Build.Utilities.ProjectCreation;
using System.Reflection;

namespace GetPackFromProject.IntegrationTests;

public abstract class TestBase : MSBuildTestBase, IDisposable
{
    private bool _disposed;

    protected DirectoryInfo Temp { get; init; }
    protected DirectoryInfo WorkingDirectory { get; } = GetWorkingDirectory();

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
        IOException? lastException = null;

        for (int i = 0; i < 3; i++)
        {
            try
            {
                directory.Delete(recursive: true);
                return;
            }
            catch (IOException ex)
            {
                lastException = ex;

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }

        if (lastException is not null)
        {
            throw new IOException($"Failed to delete temp directory '{directory.FullName}' after multiple retries.", lastException);
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
        var dirInfo = new FileInfo(assembly.Location).Directory ?? throw new Exception($"Unable to get directory from assembly location '{assembly.Location}'.");

        return dirInfo;
    }
}