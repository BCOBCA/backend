namespace BCOBCA.DAL.Context;

public static class EnvironmentBootstrap
{
    // Works for dotnet run, IDE launch and EF CLI without a machine-specific path.
    // Deployment environment variables always take precedence over the local file.
    public static void Load()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = directory.Name == "backend"
                    ? Path.Combine(directory.FullName, ".env")
                    : Path.Combine(directory.FullName, "backend", ".env");
                if (!File.Exists(candidate)) continue;
                DotNetEnv.Env.NoClobber().Load(candidate);
                return;
            }
        }
    }
}
