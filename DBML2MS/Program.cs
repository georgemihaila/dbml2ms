using CommandLine;
using Newtonsoft.Json;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using DBML2MS.Exceptions;

namespace DBML2MS
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
        }

        static Options RunOptions { get; set; }

        static Task Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(async options =>
                   {
                       RunOptions = options;

                       CheckForPrerequisites("prerequisites.json");
                       await CreateDatabaseScriptAsync(options.DbmlFile, options.DatabaseName);
                   });

            return Task.CompletedTask;
        }

        static async Task CreateDatabaseScriptAsync(string file, string databaseName)
        {
            if (!File.Exists(file))
            {
                throw new FileNotFoundException($"DBML file \"{file}\" does not exist.");
            }
            var x = ExecuteCommandLine("dbml2sql.cmd", $"{file} --mssql", AppDomain.CurrentDomain.BaseDirectory);

            var filename = databaseName + ".sql";
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
                    commandOutput = ExecuteCommandLine(prerequisite.Command, string.Join(' ', prerequisite.Arguments));
                    if (RunOptions.Verbose)
                    { 
                        Console.WriteLine($"{prerequisite.Name} ok");
                    }
                }
                catch (Exception e)
                {
                    throw new PrerequisiteNotMetException(prerequisite.Name, e);
                }
                var regex = new Regex(prerequisite.ExpectedOutput);
                if (!regex.IsMatch(commandOutput))
                {
                    throw new PrerequisiteNotMetException(prerequisite.Name);
                }
            }
        }

        static string ExecuteCommandLine(string command, string parameters, string workingDirectory = "")
        {
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
    }

    public class Prerequisite
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string[] Arguments { get; set; }
        public string ExpectedOutput { get; set; }
    }

}
