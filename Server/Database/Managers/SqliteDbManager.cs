using System.Data.SQLite;
using System.Text.RegularExpressions;
using FileFlows.Shared.Models;
using NPoco;

namespace FileFlows.Server.Database.Managers;

/// <summary>
/// A database manager used to communicate with a Sqlite Database
/// </summary>
public class SqliteDbManager : DbManager
{
    private readonly string DbFilename;
    
    /// <summary>
    /// Constructs a new Sqlite Database Manager
    /// </summary>
    /// <param name="connectionString">The connection string to the database</param>
    public SqliteDbManager(string connectionString)
    {
        // Data Source={DbFilename};Version=3;
        ConnectionString = connectionString;
        DbFilename = GetFilenameFromConnectionString(connectionString);
    }

    /// <summary>
    /// Gets the filename from a connection string
    /// </summary>
    /// <param name="connectionString">the connection string</param>
    /// <returns>the filename</returns>
    private static string GetFilenameFromConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return string.Empty;
        return Regex.Match(connectionString, @"(?<=(Data Source=))[^;]+")?.Value ?? string.Empty;
    }

    /// <summary>
    /// Constructs a new Sqlite Database Manager for a sqlite db file
    /// </summary>
    /// <param name="dbFile">the filename of the sqlite db file</param>
    /// <returns>A new Sqlite Database Manager instance</returns>
    public static SqliteDbManager ForFile(string dbFile) => new SqliteDbManager(GetConnetionString(dbFile));

    /// <summary>
    /// Gets a sqlite connection string for a db file
    /// </summary>
    /// <param name="dbFile">the filename of the sqlite db file</param>
    /// <returns>a sqlite connection string</returns>
    public static string GetConnetionString(string dbFile) => $"Data Source={dbFile};Version=3;PRAGMA journal_mode=WAL;";

    /// <summary>
    /// Gets if the database manager should use a memory cache
    /// Sqlite uses a memory cache due to its limitation of concurrent reading/writing
    /// </summary>
    public override bool UseMemoryCache => true;

    /// <summary>
    /// Get an instance of the IDatabase
    /// </summary>
    /// <returns>an instance of the IDatabase</returns>
    protected override NPoco.Database GetDbInstance() => GetDb(this.ConnectionString);

    /// <summary>
    /// Gets a database instance
    /// </summary>
    /// <param name="connectionString">the connection of the database to open</param>
    /// <returns>a database instance</returns>
    internal static NPoco.Database GetDb(string connectionString)
    {
        try
        {
            using var db = new NPoco.Database(connectionString, null, SQLiteFactory.Instance);
            db.Mappers.Add(new GuidConverter());
            return db;
        }
        catch (Exception ex)
        {
            Logger.Instance.ELog("Error loading database: " + ex.Message);
            throw;
        }
        
    }

    #region setup code
    /// <summary>
    /// Creates the actual Database
    /// </summary>
    /// <param name="recreate">if the database should be recreated if already exists</param>
    /// <returns>true if successfully created</returns>
    protected override DbCreateResult CreateDatabase(bool recreate)
    {
        if (File.Exists(DbFilename) == false)
        {
            SQLiteConnection.CreateFile(DbFilename);
            return DbCreateResult.Created;
        }
        
        // create backup 
        File.Copy(DbFilename, DbFilename + ".backup", true);
        return DbCreateResult.AlreadyExisted;
    }

    /// <summary>
    /// Creates the tables etc in the database
    /// </summary>
    /// <returns>true if successfully created</returns>
    protected override bool CreateDatabaseStructure()
    {
        using var con = new SQLiteConnection(GetConnetionString(DbFilename));
        con.Open();
        try
        {
            using var cmdExists =
                new SQLiteCommand($"SELECT name FROM sqlite_master WHERE type='table' AND name='{nameof(DbObject)}'",
                    con);
            if (cmdExists.ExecuteScalar() != null)
                return true; // tables exist, all good

            using var cmd = new SQLiteCommand(CreateDbScript, con);
            cmd.ExecuteNonQuery();
        }
        finally
        {
            con.Close();
        }
            

        return true;// tables exist, all good
    }

    public override Task<IEnumerable<LibraryStatus>> GetLibraryFileOverview()
    {
        throw new NotImplementedException();
    }

    public override Task<IEnumerable<LibraryFile>> GetLibraryFiles(FileStatus status, int max, int start, int quarter, Guid? nodeUid)
    {
        throw new NotImplementedException();
    }

    public override Task<Flow> GetFailureFlow(Guid libraryUid)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Gets the processing time for each library file 
    /// </summary>
    /// <returns>the processing time for each library file</returns>
    public override Task<IEnumerable<LibraryFileProcessingTime>> GetLibraryProcessingTimes()
    {
        throw new NotImplementedException();
    }


    public override Task<IEnumerable<ShrinkageData>> GetShrinkageGroups()
    {
        throw new NotImplementedException();
    }

    #endregion

    /// <summary>
    /// Looks to see if the file in the specified connection string exists, and if so, moves it
    /// </summary>
    /// <param name="connectionString">The connection string</param>
    public static void MoveFileFromConnectionString(string connectionString)
    {
        string filename = GetFilenameFromConnectionString(connectionString);
        if (string.IsNullOrWhiteSpace(filename))
            return;
        
        if (File.Exists(filename) == false)
            return;
        
        string dest = filename + ".backup";
        File.Move(filename, dest, true);
    }
    
    
    /// <summary>
    /// Performance a search for library files
    /// </summary>
    /// <param name="filter">the search filter</param>
    /// <returns>a list of matching library files</returns>
    public override Task<IEnumerable<LibraryFile>> SearchLibraryFiles(LibraryFileSearchModel filter)
    {
        var notImplemented = new LibraryFile[] { };
        return Task.FromResult((IEnumerable<LibraryFile>)notImplemented);
    }
}