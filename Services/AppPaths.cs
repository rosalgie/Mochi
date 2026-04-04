using System;
using System.IO;

namespace Mochi.Services;

public static class AppPaths
{
    private const string AppFolderName = "Mochi";
    private const string ConfigFileName = "config.json";
    private const string SaveFileName = "save.json";
    private const string ReportsFolderName = "Reports";

    private static string LocalAppDataDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);

    private static string DocumentsDir
    {
        get
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrWhiteSpace(documents) ? LocalAppDataDir : documents;
        }
    }

    public static string ConfigPath => Path.Combine(LocalAppDataDir, ConfigFileName);

    public static string SavePath => Path.Combine(LocalAppDataDir, SaveFileName);

    public static string ReportsPath => Path.Combine(DocumentsDir, AppFolderName, ReportsFolderName);

    public static void EnsureAppFolderExists()
    {
        if (!Directory.Exists(LocalAppDataDir)) Directory.CreateDirectory(LocalAppDataDir);
    }

    public static void EnsureReportsFolderExists()
    {
        if (!Directory.Exists(ReportsPath)) Directory.CreateDirectory(ReportsPath);
    }
}
