using System;
using System.IO;
using Microsoft.Data.Sqlite;
using NoteXMaybe;

class ResService
{
    // ! SINGLETON
    private static ResService? _instance = null;
    public static ResService Instance {

        private set {
            _instance = value;
        }

        get {
            _instance = (_instance != null)? _instance : new ResService();
            return _instance;
        }
    }

    // ! VARIABLES
    public string? root { get; private set; } = null;

    public string? activities_db_path {get; private set;} = null;

    /**
     * PRIVATE CONSTRUCTOR
     * 
     *  
     *
    **/
    private ResService()
    {
        root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );

        if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
        {
            root = $"{root}/{App.PackageName}";
        }

        // Create paths if they do not exist
        if (! Directory.Exists(root))
        {
            Console.WriteLine($"Attempting to create {root}");
            Directory.CreateDirectory(root);
        } else
        {
            Console.WriteLine($"{root} already exists");
        }

        // database
        activities_db_path = $"{root}/activities.db";
    }
}