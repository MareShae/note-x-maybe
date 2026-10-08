using System;
using System.Collections.Generic;
using System.Diagnostics;
using NoteXMaybe;

public class SystemNotificationService
{
    /**
     * ! [singleton]
     * ! only one instance permitted
    **/ 
    private static SystemNotificationService? _instance = null;
    public static SystemNotificationService Instance {

        private set {
            if (_instance is null) {
                _instance = value;
            }
        }

        get {
            _instance = (_instance != null)? _instance : new SystemNotificationService();
            return _instance;
        }

    }



    /**
     * ! [...strutor] 
     *
    **/

    private SystemNotificationService() {}

    ~SystemNotificationService() {}



    /**
     * ! [code-behind]
    **/

    public void Send(string title, string message)
    {
        #if LINUX
            Process.Start(new ProcessStartInfo(
                fileName: "notify-send",
                arguments: new List<string>{
                    "--urgency=normal",
                    $"--app-name={App.PackageName}",
                    title,
                    message
                }
            ));
        #endif
    }
}