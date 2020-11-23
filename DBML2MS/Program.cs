using CommandLine;
using static SimpleExec.Command;
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
        }

        static Task Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(async options =>
                   {
                       await CheckForPrerequisites("prerequisites.json", options.Verbose);
                       Console.WriteLine("ALl ok");
                   });

            return Task.CompletedTask;
        }

        static async Task CheckForPrerequisites(string prerequisitesFile, bool verbose = true)
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
                    if (verbose)
                    {
                        Console.WriteLine($"Checking {prerequisite.Name}...");
                    }
                    commandOutput = await ReadAsync(prerequisite.Command, string.Join(' ', prerequisite.Arguments), noEcho: !verbose, createNoWindow: true);
                    if (verbose)
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
    }

    public class Prerequisite
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string[] Arguments { get; set; }
        public string ExpectedOutput { get; set; }
    }

}
