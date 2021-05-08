using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBML2HTTP.Exceptions
{
    /// <summary>
    /// The exception that is thrown when a prerequisite is not met.
    /// </summary>
    /// <seealso cref="System.Exception" />
    public class PrerequisiteNotMetException : Exception
    {
        public string Name { get; private set; }

        public override string ToString()
        {
            return $"Prerequisite \"{Name}\" not met{Environment.NewLine}{base.ToString()}";
        }

        public PrerequisiteNotMetException(string name)
        {
            Name = name;
        }

        public PrerequisiteNotMetException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
