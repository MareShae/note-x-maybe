/**
    * Add the Microsoft SQL Client package, under the project:
    * > dotnet add package Microsoft.Data.Sqlite
**/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using Microsoft.Data.Sqlite;


/**
 * a singleton class for the relational database
 * loads all db used by the system
 * services use this rather than opening db in their space
 * on db modification, events are sent out
 * 
 * [glossary]
 * ! [singleton]
 * ! [...strutor]
 * ! [code-behind]
 *      ! {programme}
 *      ! {activity}
 *      ! {schedule}
 * ! [event handlers]
 *      ! {programme}
 *      ! {activity}
 *      ! {schedule}
 * ! [records]
 * Programme: Label, Notes
 * Activity: Label, Notes, Start, Period, Repeat
**/
class DatabaseService
{
    /**
     * ! [singleton]
     * ! only one instance permitted
    **/ 
    
    private static DatabaseService? _instance = null;
    public static DatabaseService Instance {

        private set {
            _instance = value;
        }

        get {
            _instance = (_instance != null)? _instance : new DatabaseService();
            return _instance;
        }

    }


    /**
     * ! [...strutor] 
     *
    **/

    // constructor
    private DatabaseService()
    {
        Console.WriteLine("Establishing a connection to serverless SQL");
        activities = new SqliteConnection (
            $"Data Source={ResService.Instance.activities_db_path}; Foreign Keys=True"
        );
        activities.Open();

        // Create Tables if they do not exist yet
        CreateTableProgramme();
        CreateTableActivity();
        CreateTableSchedule();

        // ? Schedule would contain {start, period, repeat}

    }

    // destructor

    ~DatabaseService()
    {
        activities?.Close();
    }



    /**
     * ! [code-behind]
    **/ 

    public SqliteConnection? activities {get; private set;} = null;

    /**
     * ! {code-behind : programme}
    **/

    public void CreateTableProgramme()
    {
        if (activities == null) throw new Exception("DB not open");

        using var cmd = activities.CreateCommand();
        cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS
        Programme (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Label TEXT NOT NULL,
            Notes TEXT NOT NULL
        )
        """;
        cmd.ExecuteNonQuery();
    }

    public (bool, string?) InsertProgramme(string label, string notes)
    {
        // verify the new entry
        if (string.IsNullOrEmpty(label))
        {
            return (false, "label cannot be null");
        }
        if (string.IsNullOrEmpty(notes))
        {
            return (false, "notes cannot be null");
        }

        // run the SQL Insert
        try {

            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = """
            INSERT INTO Programme (Label, Notes)
            VALUES ($label, $notes)
            """;

            cmd?.Parameters.AddWithValue("$label", label);
            cmd?.Parameters.AddWithValue("$notes", notes);

            cmd?.ExecuteNonQuery();

            Console.WriteLine($"Insert into Programme:  with Label:{label} and Notes:{notes}");

            NotifyProgrammeInsert?.Invoke(this, EventArgs.Empty);

            return (true, null);

        }
        catch (Exception e) {

            return (false, e.Message);

        }

    }

    public (bool, string?, List<(int, string, string)>?) SelectProgrammes()
    {
        try {
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = """
            SELECT Id, Label, Notes
            FROM Programme
            """;

            using var reader = cmd?.ExecuteReader();
            if (reader == null) throw new Exception("no reader");

            List<(int, string, string)> result = new();

            while (reader.Read()) {
                var idd = reader.GetInt64(0);
                var label = reader.GetString(1);
                var notes = reader.GetString(2);

                result.Add(((int)idd, label, notes));
            }

            return (true, null, result);
        }
        catch (Exception e)
        {
            return (false, e.Message, null);
        }
    }

    public (bool, string?, (int, string, string)?) SelectProgramme(int Id)
    {
        try
        {
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = """
            SELECT Id, Label, Notes
            FROM Programme
            WHERE Id=$Id
            """;
            cmd?.Parameters.AddWithValue("$Id", Id);

            using var reader = cmd?.ExecuteReader();
            if (reader == null) throw new Exception("no reader");

            while (reader.Read()) {
                var idd = reader.GetInt64(0);
                var label = reader.GetString(1);
                var notes = reader.GetString(2);

                return (true, null, ((int)idd, label, notes));
            }

            return (true, "empty", null);
        }
        catch (Exception e)
        {
            return (false, e.Message, null);
        }
    }

    public (bool, string?) UpdateProgramme(int Id, string? label, string? notes)
    {
        try
        {   
            // construct update param
            string paramformat = "";
            if (label != null) paramformat = $"{paramformat} Label=$label";
            if (notes != null) paramformat = $"{paramformat} Notes=$notes";

            if (string.IsNullOrEmpty(paramformat)) return (true, null);
            
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = $"""
            UPDATE Programme
            SET {paramformat}
            WHERE Id=$Id
            """;

            cmd?.Parameters.AddWithValue("$Id", Id);
            cmd?.Parameters.AddWithValue("$label", label);
            cmd?.Parameters.AddWithValue("$notes", notes);

            cmd?.ExecuteNonQuery();

            
            Console.WriteLine($"Update Programme with Id: {Id}");
            NotifyProgrammeUpdate?.Invoke(this, EventArgs.Empty);
            return (true, null);
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
    }

    public (bool, string?) DeleteProgramme(int Id)
    {
        /**
         * Since other Tables reference Programme,
         * ON CASCADE DELETE handles deleting them
        **/

        // delete just the item
        try
        {
            {
                using var cmd = activities?.CreateCommand();
                cmd?.CommandText = """
                DELETE
                FROM Programme
                WHERE Id=$Id
                """;

                cmd?.Parameters.AddWithValue("$Id", Id);

                cmd?.ExecuteNonQuery();

                Console.WriteLine($"Delete from Programme with Id: {Id}");
                NotifyProgrammeDelete?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }

        // verify that no activities reference this
        try
        {
            {
                using var cmd = activities?.CreateCommand();
                cmd?.CommandText = """
                SELECT
                COUNT(*)
                FROM Activities
                WHERE ProgrammeId=$Id
                """;
                cmd?.Parameters.AddWithValue("$Id", Id);
                Console.WriteLine($"Counted {Convert.ToInt64(cmd?.ExecuteScalar())} Activites referencers");
            }

            return (true, null);
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
    }



    /**
     * ! {code-behind : activity}
    **/

    public void CreateTableActivity()
    {
        if (activities == null) throw new Exception("DB not open");


        // Define the Table Info
        // status 0: stay, +1 new (default), -1 drop
        OrderedDictionary<string, (string? _type, string? _param, int _status)> table_struct = new();
        table_struct.Add("Id", ("INTEGER", "PRIMARY KEY AUTOINCREMENT", 1));
        table_struct.Add("ProgrammeId", ("INTEGER", "NOT NULL", 1));
        table_struct.Add("Label", ("TEXT", "NOT NULL DEFAULT 'Unlabeled'", 1));
        table_struct.Add("Notes", ("TEXT", "NOT NULL DEFAULT 'Undescribed'", 1));

        {
            using var cmd = activities.CreateCommand();
            cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS
            Activities (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProgrammeId INTEGER NOT NULL,

                FOREIGN KEY (ProgrammeId)
                    REFERENCES Programme(Id)
                    ON DELETE CASCADE
            )
            """;
            cmd.ExecuteNonQuery();
        }

        // Verify Table Columns
        {
            using var cmd = activities.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(Activities)";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string key = reader["name"].ToString()!;
                if (! table_struct.ContainsKey(key))
                {
                    // mark for DROP
                    table_struct[key] = (null, null, -1);
                } else 
                if (table_struct[key]._status == +1)
                {
                    // unmark for CREATE
                    table_struct[key] = (
                        table_struct[key]._type,
                        table_struct[key]._param,
                        0
                    );
                }
            }
        }

        {
            foreach (var item in table_struct)
            {
                if (item.Value._status == -1)
                {
                    Console.WriteLine($"Dropping {item.Key}");
                    using var cmd = activities.CreateCommand();
                    cmd.CommandText = $"""
                    ALTER TABLE Activities
                    DROP COLUMN {item.Key}
                    """;
                    cmd.ExecuteNonQuery();
                } else
                if (item.Value._status == 0)
                {
                    Console.WriteLine($"Keeping {item.Key}");
                } else
                if (item.Value._status == +1)
                {
                    Console.WriteLine($"Creating {item.Key}");
                    using var cmd = activities.CreateCommand();
                    cmd.CommandText = $"""
                    ALTER TABLE Activities
                    ADD COLUMN {item.Key} {item.Value._type} {item.Value._param}
                    """;
                    cmd.ExecuteNonQuery();
                }
                else
                {
                    throw new Exception ("unknown status");
                }
            }
        }

        // Enforce foreign keys
        {
            using var cmd = activities.CreateCommand();
            cmd.CommandText="""
            PRAGMA foreign_keys=ON
            """;
            cmd.ExecuteNonQuery();
        }
    }

    public (bool, string?) InsertActivity(int programmeId, string label, string notes) {

        // verify the new entry
        if (string.IsNullOrEmpty(label))
        {
            return (false, "label cannot be null");
        }
        if (string.IsNullOrEmpty(notes))
        {
            return (false, "notes cannot be null");
        }

        try {
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = $"""
            INSERT INTO Activities (ProgrammeId, Label, Notes)
            VALUES ($programmeId, $label, $notes)
            """;

            cmd?.Parameters.AddWithValue("$programmeId", programmeId);
            cmd?.Parameters.AddWithValue("$label", label);
            cmd?.Parameters.AddWithValue("$notes", notes);

            cmd?.ExecuteNonQuery();

            Console.WriteLine($"Insert into Activity with PId: {programmeId}, Label:{label}, and Notes:{notes}");

            NotifyActivityInsert?.Invoke(this, EventArgs.Empty);

            return (true, null);
        }
        catch (Exception e) {

            return (false, e.Message);

        }
    }

    public (bool, string?, List<(int, int, string, string)>?) SelectActivities(int programmeId)
    {
        try {
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = """
            SELECT Id, ProgrammeId, Label, Notes
            FROM Activities
            WHERE ProgrammeId=$programmeId
            """;

            cmd?.Parameters.AddWithValue("$programmeId", programmeId);

            using var reader = cmd?.ExecuteReader();
            if (reader == null) throw new Exception("no reader");

            List<(int, int, string, string)> result = new();

            while (reader.Read()) {
                var idd = reader.GetInt64(0);
                var pid = reader.GetInt64(1);
                var label = reader.GetString(2);
                var notes = reader.GetString(3);

                result.Add(((int)idd, (int) pid, label, notes));
            }

            return (true, null, result);
        }
        catch (Exception e)
        {
            return (false, e.Message, null);
        }
    }

    public (bool, string?, List<(int, int, string, string)>?) SelectActivities()
    {
        try {
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = """
            SELECT Id, ProgrammeId, Label, Notes
            FROM Activities
            """;

            using var reader = cmd?.ExecuteReader();
            if (reader == null) throw new Exception("no reader");

            List<(int, int, string, string)> result = new();

            while (reader.Read()) {
                var idd = reader.GetInt64(0);
                var pid = reader.GetInt64(1);
                var label = reader.GetString(2);
                var notes = reader.GetString(3);

                result.Add(((int)idd, (int) pid, label, notes));
            }

            return (true, null, result);
        }
        catch (Exception e)
        {
            return (false, e.Message, null);
        }
    }



    public (bool, string?, (int, int, string, string)?) SelectActivity(int Id)
    {
        try
        {
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = """
            SELECT Id, ProgrammeId, Label, Notes
            FROM Activities
            WHERE Id=$Id
            """;
            cmd?.Parameters.AddWithValue("$Id", Id);

            using var reader = cmd?.ExecuteReader();
            if (reader == null) throw new Exception("no reader");

            while (reader.Read()) {
                var idd = reader.GetInt64(0);
                var pid = reader.GetInt64(1);
                var label = reader.GetString(2);
                var notes = reader.GetString(3);

                return (true, null, ((int)idd, (int)pid, label, notes));
            }

            return (true, "empty", null);
        }
        catch (Exception e)
        {
            return (false, e.Message, null);
        }
    }

    public (bool, string?) UpdateActivity(int Id, int? pId, string? label, string? notes)
    {
        try
        {   
            // construct update param
            string paramformat = "";
            if (label is not null) paramformat = $"{paramformat} Label=$label";
            if (notes is not null) paramformat = $"{paramformat} Notes=$notes";

            if (string.IsNullOrEmpty(paramformat)) return (true, null);
            
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = $"""
            UPDATE Activities
            SET {paramformat}
            WHERE Id=$Id
            """;

            cmd?.Parameters.AddWithValue("$Id", Id);
            cmd?.Parameters.AddWithValue("$label", label);
            cmd?.Parameters.AddWithValue("$notes", notes);

            cmd?.ExecuteNonQuery();

            
            Console.WriteLine($"Update Activity with Id: {Id}");
            NotifyActivityUpdate?.Invoke(this, EventArgs.Empty);
            return (true, null);
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
    }

    public (bool, string?) DeleteActivity(int Id)
    {
        try
        {   
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = """
            DELETE
            FROM Activities
            WHERE Id=$Id
            """;

            cmd?.Parameters.AddWithValue("$Id", Id);

            cmd?.ExecuteNonQuery();

            Console.WriteLine($"Delete from Activity with Id: {Id}");

            NotifyActivityDelete?.Invoke(this, EventArgs.Empty);

            return (true, null);
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
         
    }



    /**
     * ! {code-behind : schedule}
    **/

    public void CreateTableSchedule()
    {
        if (activities == null) throw new Exception("DB not open");

        // DATE: YYYY-MM-DD
        // TIME: HH:MM:SS

        using var cmd = activities.CreateCommand();
        cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS
        Schedule (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ActivityId INT NOT NULL,
            Time TEXT NOT NULL,
            Duration INTEGER NOT NULL,
            Date TEXT NOT NULL,
            DailyInterval INTEGER NOT NULL,
            WeeklyInterval INTEGER NOT NULL,
            MonthlyInterval INTEGER NOT NULL,
            AnnualInterval INTEGER NOT NULL
        )
        """;
        cmd.ExecuteNonQuery();
    }

    public (bool, string?) InsertSchedule(
        int activityId,
        string time,
        int duration,
        string date,
        int dailyinterval,
        int weeklyinterval,
        int monthlyinterval,
        int annualinterval
    )
    {
        /** verify the new entry **/
        // time
        if (string.IsNullOrEmpty(time))
        {
            return (false, "time cannot be null");
        } else
        if (time.Split(':').Length != 3)
        {
            return (false, "incorrect time format, expected HH:MM:SS");
        }
        // date
        if (string.IsNullOrEmpty(date))
        {
            return (false, "date cannot be null");
        } else
        if (date.Split('-').Length != 3)
        {
            return (false, "incorrect date format, expected YYYY-MM-DD");
        }

        // run the SQL Insert
        try {

            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = """
            INSERT INTO Schedule (ActivityId, Time, Duration, Date, DailyInterval, WeeklyInterval, MonthlyInterval, AnnualInterval)
            VALUES ($activityId, $time, $duration, $date, $dailyinterval, $weeklyinterval, $monthlyinterval, $annualinterval)
            """;

            cmd?.Parameters.AddWithValue("$activityId", activityId);
            cmd?.Parameters.AddWithValue("$time", time);
            cmd?.Parameters.AddWithValue("$duration", duration);
            cmd?.Parameters.AddWithValue("$date", date);
            cmd?.Parameters.AddWithValue("$dailyinterval", dailyinterval);
            cmd?.Parameters.AddWithValue("$weeklyinterval", weeklyinterval);
            cmd?.Parameters.AddWithValue("$monthlyinterval", monthlyinterval);
            cmd?.Parameters.AddWithValue("$annualinterval", annualinterval);

            cmd?.ExecuteNonQuery();

            Console.WriteLine($"Insert into Schedule for Activity {activityId}");

            NotifyScheduleInsert?.Invoke(this, EventArgs.Empty);

            return (true, null);

        }
        catch (Exception e) {

            return (false, e.Message);

        }
        
    }

    public (bool, string?, List<(int, int, string, int, string, int, int, int, int)>?) SelectSchedules()
    {
        try
        {
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = """
            SELECT Id, ActivityId, Time, Duration, Date, DailyInterval, WeeklyInterval, MonthlyInterval, AnnualInterval
            FROM Schedule
            """;

            using var reader = cmd?.ExecuteReader();
            if (reader == null) throw new Exception("no reader");

            List<(int, int, string, int, string, int, int, int, int)> result = new();

            while (reader.Read()) {
                var iid = reader.GetInt64(0);
                var aid = reader.GetInt64(1);
                var time = reader.GetString(2);
                var duration = reader.GetInt64(3);
                var date = reader.GetString(4);
                var dailyinterval = reader.GetInt64(5);
                var weeklyinterval = reader.GetInt64(6);
                var monthlyinterval = reader.GetInt64(7);
                var annualinterval = reader.GetInt64(8);

                result.Add((
                    (int)iid,
                    (int)aid,
                    time,
                    (int)duration,
                    date,
                    (int)dailyinterval,
                    (int)weeklyinterval,
                    (int)monthlyinterval,
                    (int)annualinterval
                    ));
            }

            return (true, null, result);
        }
        catch (Exception e) {

            return (false, e.Message, null);

        }
    }

    public (bool, string?) UpdateSchedule(
        int Id,
        string? time,
        int? duration,
        string? date,
        int? dailyinterval,
        int? weeklyinterval,
        int? monthlyinterval,
        int? annualinterval
        )
    {
        try
        {
            // construct update param
            string paramformat = "";
            if (time is not null) paramformat = $"{paramformat} Time=$time";
            if (duration is not null) paramformat = $"{paramformat} Duration=$duration";
            if (date is not null) paramformat = $"{paramformat} Date=$date";
            if (dailyinterval is not null) paramformat = $"{paramformat} Dailyinterval=$dailyinterval";
            if (weeklyinterval is not null) paramformat = $"{paramformat} Weeklyinterval=$weeklyinterval";
            if (monthlyinterval is not null) paramformat = $"{paramformat} Monthlyinterval=$monthlyinterval";
            if (annualinterval is not null) paramformat = $"{paramformat} Annualinterval=$annualinterval";

            if (string.IsNullOrEmpty(paramformat)) return (true, null);
            
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = $"""
            UPDATE Schedule
            SET {paramformat}
            WHERE Id=$Id
            """;

            cmd?.Parameters.AddWithValue("$Id", Id);
            cmd?.Parameters.AddWithValue("$time", time);
            cmd?.Parameters.AddWithValue("$duration", duration);
            cmd?.Parameters.AddWithValue("$date", date);
            cmd?.Parameters.AddWithValue("$dailyinterval", dailyinterval);
            cmd?.Parameters.AddWithValue("$weeklyinterval", weeklyinterval);
            cmd?.Parameters.AddWithValue("$monthlyinterval", monthlyinterval);
            cmd?.Parameters.AddWithValue("$annualinterval", annualinterval);

            cmd?.ExecuteNonQuery();

            NotifyScheduleUpdate?.Invoke(this, EventArgs.Empty);
            return (true, null);
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
    }

    public (bool, string?) DeleteSchedule(int Id)
    {
        try
        {   
            using var cmd = activities?.CreateCommand();
            cmd?.CommandText = """
            DELETE
            FROM Schedule
            WHERE Id=$Id
            """;

            cmd?.Parameters.AddWithValue("$Id", Id);

            cmd?.ExecuteNonQuery();

            Console.WriteLine($"Delete from Schedule with Id: {Id}");

            NotifyScheduleDelete?.Invoke(this, EventArgs.Empty);

            return (true, null);
        }
        catch (Exception e)
        {
            return (false, e.Message);
        }
    }



    /**
     * ! [event handlers]
     * ! events that the service invokes
    **/

    /**
     * ! {event handlers : programme}
    **/

    public event EventHandler? NotifyProgrammeInsert;
    public event EventHandler? NotifyProgrammeUpdate;
    public event EventHandler? NotifyProgrammeDelete;


    /**
     * ! {event handlers : activity}
    **/

    public event EventHandler? NotifyActivityInsert;
    public event EventHandler? NotifyActivityUpdate;
    public event EventHandler? NotifyActivityDelete;

    /**
     * ! {event handlers : schedule}
    **/

    public event EventHandler? NotifyScheduleInsert;
    public event EventHandler? NotifyScheduleUpdate;
    public event EventHandler? NotifyScheduleDelete;



    /**
     * ! [records]
     * ! where they are meant to be
    **/

    
}