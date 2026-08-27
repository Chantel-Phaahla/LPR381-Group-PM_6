using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Person_1.Models
{
    public enum ObjectiveType
    {
        Maximize,
        Minimize
    }

    public enum SignRestriction
    {
        NonNegative,
        NonPositive,
        Unrestricted,
        Integer,
        Binary
    }

    public enum ConstraintRelation
    {
        LessOrEqual,
        GreaterOrEqual,
        Equal
    }
}

