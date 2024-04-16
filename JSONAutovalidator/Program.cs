using Newtonsoft.Json;

namespace JSONAutovalidator;

internal class Program
{
    /// <summary>
    /// The path to the file that will be watched for changes.
    /// </summary>
    private readonly string _filePath;

    /// <summary>
    /// The <see cref="FileSystemWatcher"/> instance used to monitor changes in a file.
    /// </summary>
    private readonly FileSystemWatcher _watcher;

    /// <summary>
    /// Represents the last time a fix was performed on a file.
    /// </summary>
    private DateTime _lastFix = DateTime.MinValue;

    /// <summary>
    /// Flag indicating whether the program should be terminated.
    /// </summary
    private bool _kill;

    /// <summary>
    /// Represents a program that watches a file for changes.
    /// </summary>
    private Program(string filePath)
    {
        Console.WriteLine($"Watching file {filePath} for changes...");
        _filePath = Path.GetFullPath(filePath.Trim('"'));
        if (!File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"File {filePath} does not exist");
            Environment.Exit(1);
            return;
        }

        _watcher = new FileSystemWatcher(Path.GetDirectoryName(filePath)!, Path.GetFileName(filePath)!)
        {
            NotifyFilter = NotifyFilters.LastWrite,
            IncludeSubdirectories = false
        };
        _watcher.Changed += WatcherOnChanged;
        _watcher.EnableRaisingEvents = true;
        WatcherOnChanged(this, new FileSystemEventArgs(WatcherChangeTypes.Changed, Path.GetDirectoryName(filePath)!, _filePath));
        Console.CancelKeyPress += (_, _) =>
        {
            Console.WriteLine("Exiting...");
            _kill = true;
            _watcher.Dispose();
        };
        // Keep the program running
        while (!_kill)
        {
            Thread.Sleep(1000);
        }
    }

    /// <summary>
    /// Event handler for the FileSystemWatcher.Changed event. This method is called when a change
    /// occurs in the watched file.
    /// </summary>
    /// <param name="sender">The object that raised this event.</param>
    /// <param name="e">The event arguments containing information about the file change.</param>
    /// <returns>void</returns>
    private void WatcherOnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed) return;
        if ((DateTime.Now - _lastFix).TotalSeconds < 5) return;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"File: {e.FullPath} {e.ChangeType}");
        Console.ResetColor();

        Thread.Sleep(1000);
        Fix();
    }

    /// <summary>
    /// Fixes the content of the file by performing the following steps:
    /// 1. Read the content of the file from the specified file path or use the provided content.
    /// 2. Deserialize the content using JSON.NET.
    /// 3. Serialize the deserialized content back into JSON format.
    /// 4. Replace any occurrence of the string "undefined," in the serialized content.
    /// 5. Write the fixed content back to the file.
    /// 6. If an error occurs during the process, handle it accordingly.
    /// </summary>
    /// <param name="content">Optional content to fix. If not provided, the content will be read from the specified file path.</param>
    /// <param name="attempts">The number of attempts made to fix the file. Defaults to 0.</param>
    private void Fix(string content = "", int attempts = 0)
    {
        try
        {
            _lastFix = DateTime.Now;
            if (string.IsNullOrWhiteSpace(content))
                content = File.ReadAllText(_filePath);
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(JsonConvert.DeserializeObject(content)).Replace("undefined,", ""));
            _lastFix = DateTime.Now;
        }
        catch (JsonReaderException e)
        {
            if (e.Message.StartsWith("Unexpected character"))
            {
                string[] lines = content.Split('\n');
                string line = lines[e.LineNumber - 1];
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error parsing JSON: {e.Message}");
                Console.ResetColor();
                Console.WriteLine($"Line {e.LineNumber}: {line}");
                Console.WriteLine(new string(' ', $"Line {e.LineNumber}: ".Length + e.LinePosition) + "^");
                string fixedContent = line.Remove(e.LinePosition, 1);
                // rebuild the content with the replaced line
                content = string.Join('\n', lines[..(e.LineNumber - 1)]) + fixedContent + string.Join('\n', lines[(e.LineNumber)..]);
                Fix(content, attempts + 1);
                return;
            }
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error writing to file: {e.Message}");
            Console.ResetColor();
            if (attempts < 5)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Retrying...");
                Console.ResetColor();
                Thread.Sleep(5000);
                Fix(attempts: attempts + 1);
                return;
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Failed to write to file after 3 attempts");
            Console.ResetColor();
            Environment.Exit(2);
        }

        Console.WriteLine("Fixed file");
    }

    /// <summary>
    /// The entry point of the application.
    /// </summary>
    /// <param name="args">
    /// The command-line arguments.
    /// </param>
    private static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"usage: {AppDomain.CurrentDomain.FriendlyName} <path>");
            Environment.Exit(1);
            return;
        }

        _ = new Program(args[0]);
    }
}