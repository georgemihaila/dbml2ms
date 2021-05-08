using CommandLine;
using Newtonsoft.Json;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using DBML2HTTP.Exceptions;
using System.Data.SqlClient;

namespace DBML2HTTP
{
    public class Program
    {
        public class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option("source", Required = true, HelpText = "The source dbml file")]
            public string DbmlFile { get; set; }

            [Option("dbname", Required = true, HelpText = "The output database name")]
            public string DatabaseName { get; set; }

            [Option("connection-string", Required = true, HelpText = "The MSSQL database connection string")]
            public string ConnectionString { get; set; }

            [Option('d', "verbose", Required = false, HelpText = "Drop database if already exists.")]
            public bool DropIfAlreadyExists { get; set; } = false;

            [Option("no-project", Required = false, HelpText = "Whether to only create a database and not also create a project.")]
            public bool DontCreateProject { get; set; } = false;

            [Option("context-name", Required = false, HelpText = "The name of the database context")]
            public string ContextName { get; set; } = string.Empty;

            [Option("project-name", Required = false, HelpText = "The name of the output project")]
            public string ProjectName { get; set; } = string.Empty;

            [Option("project-dir", Required = false, HelpText = "The directory where the project will be created")]
            public string ProjectLocation { get; set; } = string.Empty;

            [Option("run", Required = false, HelpText = "Run the project after creation")]
            public bool Run { get; set; } = false;

            public string ProjectDirectory => ProjectLocation + ProjectName;
        }

        static Options RunOptions { get; set; }

        static Task Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(async options =>
                   {
                       //Looks like .WithParsed( ... ) swallows exceptions
                       try
                       {
                           RunOptions = options;
                           if (string.IsNullOrWhiteSpace(RunOptions.ProjectLocation))
                           {
                               RunOptions.ProjectLocation = AppDomain.CurrentDomain.BaseDirectory;
                           }

                           Console.WriteLine("Checking for prerequisites...");
                           CheckForPrerequisites("prerequisites.json");
                           Console.WriteLine("Creating database script...");
                           await CreateAndWriteDatabaseScriptAsync(options.DbmlFile, options.DatabaseName);
                           Console.WriteLine("Creating database...");
                           CreateDatabase();
                           if (!options.DontCreateProject)
                           {
                               CreateProject();
                               AddDependencies();
                               ScaffoldDbContext();
                               AddConnectionStringToAppsettings();
                               SetupStartup();
                               if (options.Run)
                               {
                                   Build();
                                   Run();
                               }
                           }
                       }
                       catch (Exception e)
                       {
                           Console.WriteLine(e);
                       }
                   });

            return Task.CompletedTask;
        }

        static void AddConnectionStringToAppsettings()
        {
            var location = $"{RunOptions.ProjectDirectory}\\appsettings.json";
            File.Delete(location);
            File.WriteAllText(location, File.ReadAllText("templates/appsettings.json").Replace("[0]", $"{RunOptions.ConnectionString}Initial Catalog={RunOptions.DatabaseName};"));
        }

        static void SetupStartup()
        {
            var location = $"{RunOptions.ProjectDirectory}\\Startup.cs";
            File.Delete(location);
            File.WriteAllText(location, File.ReadAllText("templates/Startup.cs")
                .Replace("[ProjectName]", RunOptions.ProjectName)
                .Replace("[ContextName]", RunOptions.ContextName));
        }

        static void CreateProject()
        {
            if (string.IsNullOrWhiteSpace(RunOptions.ProjectName))
            {
                RunOptions.ProjectName = $"{RunOptions.DatabaseName}API";
            }
            Console.WriteLine($"Creating project {RunOptions.ProjectName}...");
            ExecuteCommandLineAndPrintIfVerbose("dotnet", $"new webapi --name {RunOptions.ProjectName}");
        }

        static void AddDependencies()
        {
            Console.WriteLine("Adding dependencies...");
            ExecuteCommandLineAndPrintIfVerbose("dotnet", $"add {RunOptions.ProjectName} package Microsoft.EntityFrameworkCore");
            ExecuteCommandLineAndPrintIfVerbose("dotnet", $"add {RunOptions.ProjectName} package Microsoft.EntityFrameworkCore.Design");
            ExecuteCommandLineAndPrintIfVerbose("dotnet", $"add {RunOptions.ProjectName} package Microsoft.EntityFrameworkCore.SqlServer");
            ExecuteCommandLineAndPrintIfVerbose("dotnet", $"add {RunOptions.ProjectName} package Swashbuckle.AspNetCore");
            ExecuteCommandLineAndPrintIfVerbose("dotnet", $"add {RunOptions.ProjectName} package Swashbuckle.AspNetCore.Swagger");
            ExecuteCommandLineAndPrintIfVerbose("dotnet", $"add {RunOptions.ProjectName} package Swashbuckle.AspNetCore.SwaggerUI");
        }

        static void Build()
        {
            Console.WriteLine("Building...");
            ExecuteCommandLineAndPrintIfVerbose("dotnet", "build", RunOptions.ProjectDirectory);
        }

        static void Run()
        {
            Console.WriteLine("Running...");
            ExecuteCommandLineAndPrintIfVerbose("dotnet", "run", RunOptions.ProjectDirectory);
        }

        static void ScaffoldDbContext()
        {
            if (string.IsNullOrWhiteSpace(RunOptions.ContextName))
            {
                RunOptions.ContextName = $"{RunOptions.DatabaseName}Context";
            }
            Console.WriteLine($"Creating context {RunOptions.ContextName}...");
            ExecuteCommandLineAndPrintIfVerbose("dotnet", $"ef dbcontext scaffold \"{RunOptions.ConnectionString}Initial Catalog={RunOptions.DatabaseName};\" Microsoft.EntityFrameworkCore.SqlServer -o {"Contexts"} --project={RunOptions.ProjectName}");
        }

        static void RegisterContext()
        {
            Console.WriteLine("Registering context...");
        }

        static async Task CreateAndWriteDatabaseScriptAsync(string file, string databaseName)
        {
            if (!File.Exists(file))
            {
                throw new FileNotFoundException($"DBML file \"{file}\" does not exist.");
            }
            var x = ExecuteCommandLineAndPrintIfVerbose("dbml2sql.cmd", $"{file} --mssql").Replace("GO", ";");

            var filename = databaseName + ".sql";
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            if (RunOptions.DropIfAlreadyExists)
            {
                await File.WriteAllTextAsync(filename, AppendAndPrependNewLine(File.ReadAllText("scripts/drop-database.sql").Replace("[0]", $"[{RunOptions.DatabaseName}]")));
            }
            await File.AppendAllTextAsync(filename, AppendAndPrependNewLine(File.ReadAllText("scripts/create-and-use-database.sql").Replace("[0]", $"[{RunOptions.DatabaseName}]")));
            await File.AppendAllTextAsync(filename, AppendAndPrependNewLine(x));
        }

        static void CheckForPrerequisites(string prerequisitesFile)
        {
            if (!File.Exists(prerequisitesFile))
            {
                throw new FileNotFoundException($"Prerequisite file \"{prerequisitesFile}\" not found");
            }
            
            var prerequisites = JsonConvert.DeserializeObject<IEnumerable<Prerequisite>>(File.ReadAllText(prerequisitesFile));
            foreach(var prerequisite in prerequisites)
            {
                var commandOutput = string.Empty;
                try
                {
                    if (RunOptions.Verbose)
                    {
                        Console.WriteLine($"Checking {prerequisite.Name}...");
                    }
                    commandOutput = ExecuteCommandLineAndPrintIfVerbose(prerequisite.Command, string.Join(' ', prerequisite.Arguments));
                }
                catch (Exception e)
                {
                    throw new PrerequisiteNotMetException(prerequisite.Name, e);
                }
                var regex = new Regex(prerequisite.ExpectedOutput);
                if (regex.Matches(commandOutput).Count > 0)
                {
                    throw new PrerequisiteNotMetException(prerequisite.Name);
                }
                if (RunOptions.Verbose)
                {
                    Console.WriteLine($"{prerequisite.Name} ok");
                }
            }
        }

        static string ExecuteCommandLineAndPrintIfVerbose(string command, string parameters, string workingDirectory = "")
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                    workingDirectory = RunOptions.ProjectLocation;
            }

            if (RunOptions.Verbose)
            {
                Console.Write($"{command} {parameters}: ");
            }
            //Create process
            using (var process = new System.Diagnostics.Process())
            {

                //strCommand is path and file name of command to run
                process.StartInfo.FileName = command;

                //strCommandParameters are parameters to pass to program
                process.StartInfo.Arguments = parameters;

                process.StartInfo.UseShellExecute = false;

                //Set output of program to be written to process output stream
                process.StartInfo.RedirectStandardOutput = true;

                //Optional
                process.StartInfo.WorkingDirectory = workingDirectory;

                //Start the process
                process.Start();

                //Get program output
                string strOutput = process.StandardOutput.ReadToEnd();

                if (RunOptions.Verbose)
                {
                    Console.WriteLine($"{strOutput}");
                }

                //Wait for process to finish
                process.WaitForExit();

                return strOutput;
            }
        }

        static string AppendAndPrependNewLine(string source) 
        {
            if (!source.StartsWith(Environment.NewLine))
            {
                source = Environment.NewLine + source;
            }
            if (!source.EndsWith(Environment.NewLine))
            {
                source += Environment.NewLine;
            }
            return source;
        }

        static void CreateDatabase()
        {
            var options = RunOptions;
            if (options.Verbose)
            {
                Console.WriteLine("Opening database connection...");
            }
            try
            {
                using (var connection = new SqlConnection(options.ConnectionString))
                {
                    connection.Open();
#pragma warning disable CA2100
                    using (var command = new SqlCommand(File.ReadAllText($"{options.DatabaseName}.sql"))
                    {
                        Connection = connection
                    })
#pragma warning restore CA2100
                    {
                        if (options.Verbose)
                        {
                            Console.WriteLine("Writing changes...");
                        }

                        command.ExecuteNonQuery();
                    }

                    connection.Close();
                }

                if (options.Verbose)
                {
                    Console.WriteLine("Done creating database");
                }
            }
            catch (Exception e)
            {
                if (options.Verbose)
                {
                    Console.WriteLine(e);
                }
            }

        }
    }

    public class Prerequisite
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string[] Arguments { get; set; }
        public string ExpectedOutput { get; set; }
    }

}
