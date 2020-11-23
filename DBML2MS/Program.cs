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

        static async Task Main(string[] args)
        {
            await CheckForPrerequisites("prerequisites.json");
            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed<Options>(o =>
                   {
                       if (o.Verbose)
                       {
                           Console.WriteLine($"Verbose output enabled. Current Arguments: -v {o.Verbose}");
                           Console.WriteLine("Quick Start Example! App is in Verbose mode!");
                       }
                       else
                       {
                           Console.WriteLine($"Current Arguments: -v {o.Verbose}");
                           Console.WriteLine("Quick Start Example!");
                       }
                   });
        }

        static async Task CheckForPrerequisites(string prerequisitesFile)
        {
            if (!File.Exists(prerequisitesFile))
            {
                throw new FileNotFoundException($"Prerequisite file \"{prerequisitesFile}\" not found");
            }
            
            var prerequisites = JsonConvert.DeserializeObject<IEnumerable<Prerequisite>>(File.ReadAllText(prerequisitesFile));
            foreach(var prerequisite in prerequisites)
            {
                var commandOutput = await ReadAsync(prerequisite.Command, string.Join(' ', prerequisite.Arguments));
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
