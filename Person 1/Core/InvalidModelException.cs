using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Person_1.Core
{

    public class InvalidModelException : Exception
    {
        /// Gets the line number where the error occurred (if applicable)
        public int? LineNumber { get; }

        /// Gets the type of validation error
        public string ErrorType { get; }

        public InvalidModelException(string message) : base(message)
        {
        }

        public InvalidModelException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public InvalidModelException(string message, int lineNumber, string errorType = "ParseError") : base(message)
        {
            LineNumber = lineNumber;
            ErrorType = errorType;
        }

        public override string ToString()
        {
            var result = base.ToString();
            if (LineNumber.HasValue)
            {
                result = $"Line {LineNumber}: {result}";
            }
            if (!string.IsNullOrEmpty(ErrorType))
            {
                result = $"[{ErrorType}] {result}";
            }
            return result;
        }
    }
}