using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBML2MS.Exceptions
{
    /// <summary>
    /// The exception that is thrown when a prerequisite is not met.
    /// </summary>
    /// <seealso cref="System.Exception" />
    public class PrerequisiteNotMetException : Exception
    {
        public string Name { get; private set; }

        public PrerequisiteNotMetException(string name)
        {
            Name = name;
        }
    }
}
