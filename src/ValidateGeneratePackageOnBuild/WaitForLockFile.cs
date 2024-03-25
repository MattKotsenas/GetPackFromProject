using Microsoft.Build.Framework;

namespace GetPackFromProject.MSBuild.ValidateGeneratePackageOnBuild;

public class WaitForLockFile : Microsoft.Build.Utilities.Task
{
    [Required]
    public string? LockFile { get; set; }

    [Required]
    public int SleepSeconds { get; set; }

    [Required]
    public int MaxRetries { get; set; }

    public override bool Execute()
    {
        if (LockFile is null) { throw new ArgumentNullException(nameof(LockFile)); }

        TimeSpan delay = TimeSpan.FromSeconds(SleepSeconds);
        string uniqueMarker = Guid.NewGuid().ToString();
        int retries = 0;

        while (retries < MaxRetries)
        {
            TryWriteLock(LockFile, uniqueMarker);
            if (IsMyLock(LockFile, uniqueMarker))
            {
                return true;
            }

            Log.LogMessage(
                MessageImportance.Normal,
                $"Waiting for lock file '{LockFile}' to be deleted. Sleeping for '{delay}' (retry {retries} of {MaxRetries})...");

            Thread.Sleep(delay);
            retries += 1;
        }

        LogDiagnostic("GPP002", null!, $"Unable to acquire lock file '{LockFile}' after '{MaxRetries}' tries.");
        return false;
    }

    private void LogDiagnostic(string diagnosicCode, string file, string message)
    {
        Log.LogError(
            subcategory: null,
            errorCode: diagnosicCode,
            helpKeyword: null,
            file: file,
            lineNumber: 0,
            columnNumber: 0,
            endLineNumber: 0,
            endColumnNumber: 0,
            message: message);
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
