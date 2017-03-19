namespace HWP_VM
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;


    public class AssemblerException : Exception
    {
        public int LineNumber { get; private set; }
        public string Line { get; private set; }

        public AssemblerException(string message, Exception innerException, int lineNumber, string line)
            : base(message, innerException)
        {
            this.LineNumber = lineNumber;
            this.Line = line;
        }
        
    }
}
