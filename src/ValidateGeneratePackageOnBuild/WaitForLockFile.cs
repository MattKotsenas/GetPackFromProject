using Microsoft.Build.Framework;

namespace GetPackFromProject.MSBuild.ValidateGeneratePackageOnBuild;

public class WaitForLockFile : Microsoft.Build.Utilities.Task
{
    [Required]
    public string? LockFile { get; set; }

    public int SleepSeconds { get; set; } = 2;

    public int RetryCount { get; set; } = 100;

    public override bool Execute()
    {
        // TODO: Give up eventually

        if (LockFile is null) { throw new ArgumentNullException(nameof(LockFile)); }

        TimeSpan delay = TimeSpan.FromSeconds(SleepSeconds);
        string uniqueMarker = Guid.NewGuid().ToString();

        loop:
        TryWriteLock(LockFile, uniqueMarker);
        if (IsMyLock(LockFile, uniqueMarker))
        {
            return true;
        }

        Log.LogMessage(
            MessageImportance.Normal,
            $"Waiting for lock file '{LockFile}' to be deleted. Sleeping for '{delay}'...");

        Thread.Sleep(delay);

        goto loop;
    }

    private static bool IsMyLock(string path, string uniqueMarker)
    {
        try
        {
            string contents = File.ReadAllText(path);

            return contents == uniqueMarker;
        }
        catch
        {
            // Do nothing
        }

        return false;
    }

    private static void TryWriteLock(string path, string uniqueMarker)
    {
        try
        {
            using StreamWriter writer = new StreamWriter(File.Create(path));

            writer.Write(uniqueMarker);
        }
        catch
        {
            // Do nothing
        }
    }
}
