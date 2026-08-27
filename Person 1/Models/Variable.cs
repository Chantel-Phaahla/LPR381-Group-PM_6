using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Person_1.Models
{
    /// Represents a variable in the linear programming model
    public class Variable
    {
        public string Name { get; set; }
        public SignRestriction SignRestriction { get; set; }
        public double Value { get; set; }
        public int Index { get; set; }

        public Variable(string name, SignRestriction signRestriction, int index = 0)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            SignRestriction = signRestriction;
            Value = 0.0;
            Index = index;
        }

        /// Get human-readable string representation of sign restriction
        public string GetSignRestrictionString()
        {
            switch (SignRestriction)
            {
                case SignRestriction.NonNegative:
                    return "≥ 0";
                case SignRestriction.NonPositive:
                    return "≤ 0";
                case SignRestriction.Unrestricted:
                    return "unrestricted";
                case SignRestriction.Integer:
                    return "integer, ≥ 0";
                case SignRestriction.Binary:
                    return "binary {0,1}";
                default:
                    return "unknown";
            }
        }

        /// Check if variable is integer type (integer or binary)
        public bool IsIntegerType()
        {
            return SignRestriction == SignRestriction.Integer || SignRestriction == SignRestriction.Binary;
        }

        /// Get string representation of the variable
        public override string ToString()
        {
            return $"{Name}: {GetSignRestrictionString()}";
        }
    }
}