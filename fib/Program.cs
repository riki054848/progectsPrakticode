using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand
        {
            CreateBundleCommand(),
            CreateRspCommand()
        };

        return await rootCommand.InvokeAsync(args);
    }

    static Command CreateBundleCommand()
    {
        var bundleCommand = new Command("bundle", "איחוד קבצי קוד לקובץ אחד")
        {
            new Option<List<string>>(
                "--language",
                description: "רשימת שפות תכנות. אם נבחר 'all', כל קבצי הקוד ייכללו.") { IsRequired = true },
            new Option<string>(
                "--output",
                description: "נתיב הקובץ המיוצא") { IsRequired = true },
            new Option<bool>(
                "--note",
                description: "האם לכלול מידע על מקור הקובץ כהערה"),
            new Option<List<string>>(
                "--sort",
                description: "למיין קבצים לפי שם או סוג") { Arity = ArgumentArity.ZeroOrOne },
            new Option<bool>(
                "--remove-empty-lines",
                description: "למחוק שורות ריקות מקוד המקור"),
            new Option<string>(
                "--author",
                description: "שם יוצר הקובץ") { Arity = ArgumentArity.ZeroOrOne }
        };

        bundleCommand.Handler = CommandHandler.Create<List<string>, string, bool, List<string>, bool, string>(
            async (language, output, note, sort, removeEmptyLines, author) =>
            {
                if (string.IsNullOrEmpty(output))
                {
                    Console.WriteLine("שגיאה: חובה לציין את נתיב הקובץ באמצעות --output.");
                    return;
                }
                await BundleFiles(language, output, note, sort, removeEmptyLines, author);
            });

        return bundleCommand;
    }

    static Command CreateRspCommand()
    {
        var command = new Command("create-rsp", "צור קובץ תגובה עבור פקודת האיחוד.")
        {
            new Option<List<string>>(
                "--language",
                description: "רשימת שפות תכנות. אם נבחר 'all', כל קבצי הקוד ייכללו.") { IsRequired = true },
            new Option<string>(
                "--output",
                description: "נתיב הקובץ המיוצא") { IsRequired = true },
            new Option<bool>(
                "--note",
                description: "האם לכלול מידע על מקור הקובץ כהערה"),
            new Option<List<string>>(
                "--sort",
                description: "למיין קבצים לפי שם או סוג") { Arity = ArgumentArity.ZeroOrOne },
            new Option<bool>(
                "--remove-empty-lines",
                description: "למחוק שורות ריקות מקוד המקור"),
            new Option<string>(
                "--author",
                description: "שם יוצר הקובץ") { Arity = ArgumentArity.ZeroOrOne }
        };

        command.Handler = CommandHandler.Create<List<string>, string, bool, List<string>, bool, string>(
            async (language, output, note, sort, removeEmptyLines, author) =>
            {
                if (string.IsNullOrEmpty(output))
                {
                    Console.WriteLine("שגיאה: חובה לציין את נתיב הקובץ באמצעות --output.");
                    return;
                }
                await CreateRspFile(language, output, note, sort, removeEmptyLines, author);
            });

        return command;
    }

    static async Task BundleFiles(List<string> language, string output, bool note, List<string> sort, bool removeEmptyLines, string author)
    {
        if (!ValidateOutputPath(output))
        {
            Console.WriteLine($"נתיב לא חוקי: {output}");
            return;
        }

        var extensions = language.Contains("all")
            ? new[] { ".cs", ".java", ".py", ".js", ".cpp" }
            : language.Select(GetExtension).ToArray();

        var filesToBundle = GetFilesToBundle(extensions);

        filesToBundle = sort != null && sort.Contains("type")
            ? filesToBundle.OrderBy(f => Path.GetExtension(f)).ToList()
            : filesToBundle.OrderBy(f => f).ToList();

        try
        {
            using var bundleFile = new StreamWriter(output);
            if (!string.IsNullOrEmpty(author))
            {
                await bundleFile.WriteLineAsync($"# שם יוצר: {author}");
            }

            foreach (var file in filesToBundle)
            {
                if (note)
                {
                    await bundleFile.WriteLineAsync($"# מקור: {file}");
                }

                var lines = await File.ReadAllLinesAsync(file);
                if (removeEmptyLines)
                {
                    lines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
                }
                await bundleFile.WriteLineAsync(string.Join(Environment.NewLine, lines));
            }

            Console.WriteLine($"אוחדו {filesToBundle.Count} קבצים לתוך {output}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"שגיאה בשמירה ל-{output}: {e.Message}");
        }
    }

    static async Task CreateRspFile(List<string> language, string output, bool note, List<string> sort, bool removeEmptyLines, string author)
    {
        var rspFileName = Path.ChangeExtension(output, ".rsp");
        try
        {
            using var rspFile = new StreamWriter(rspFileName);
            await rspFile.WriteLineAsync($"bundle --language {string.Join(",", language)} --output {output} --note {note} --sort {string.Join(",", sort)} --remove-empty-lines {removeEmptyLines} --author {author}");
            Console.WriteLine($"נוצר קובץ תגובה: {rspFileName}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"שגיאה ביצירת קובץ התגובה: {e.Message}");
        }
    }

    static bool ValidateOutputPath(string output)
    {
        try
        {
            var fullPath = Path.GetFullPath(output);
            return !fullPath.Contains("bin") && !fullPath.Contains("debug");
        }
        catch
        {
            return false;
        }
    }

    static string GetExtension(string language)
    {
        return language.ToLower() switch
        {
            "csharp" => ".cs",
            "java" => ".java",
            "python" => ".py",
            "javascript" => ".js",
            "cpp" => ".cpp",
            _ => throw new ArgumentException($"שפה לא נתמכת: {language}")
        };
    }

    static List<string> GetFilesToBundle(string[] extensions)
    {
        var files = new List<string>();
        var excludedDirs = new[] { "bin", "debug" };

        foreach (var ext in extensions)
        {
            files.AddRange(Directory.EnumerateFiles(Directory.GetCurrentDirectory(), $"*{ext}", SearchOption.AllDirectories)
                .Where(file => !excludedDirs.Any(dir => file.Contains(Path.Combine(Directory.GetCurrentDirectory(), dir)))));
        }
        return files;
    }
}
