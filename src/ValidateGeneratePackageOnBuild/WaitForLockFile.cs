using Microsoft.Build.Framework;

namespace GetPackFromProject.MSBuild.ValidateGeneratePackageOnBuild;

public class WaitForLockFile : Microsoft.Build.Utilities.Task
{
    // TODO: Racy; use a file with custom text + retry

    [Required]
    public string? LockFile { get; set; }

    public override bool Execute()
    {
        TimeSpan delay = TimeSpan.FromSeconds(5);
        string uniqueMarker = Guid.NewGuid().ToString();

        loop:
        while (File.Exists(LockFile))
        {
            if (!IsMyLock(LockFile, uniqueMarker))
            {
                Log.LogMessage(
                    MessageImportance.Normal,
                    $"Waiting for lock file '{LockFile}' to be deleted. Sleeping for '{delay}'...");

                Thread.Sleep(delay);
            }
        }

        return true;
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
}
