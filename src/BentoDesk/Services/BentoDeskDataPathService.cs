namespace BentoDesk.Services;

public sealed class BentoDeskDataPathService
{
    public static BentoDeskDataPathService Current { get; } = new();

    public BentoDeskDataPathService(string? rootPath = null)
    {
        RootPath = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BentoDesk")
            : rootPath;
    }

    public string RootPath { get; }
    public string DataDirectory => Path.Combine(RootPath, "data");
    public string UpdatesDirectory => Path.Combine(RootPath, "updates");
    public string LogFilePath => Path.Combine(RootPath, "BentoDesk.log");
}
