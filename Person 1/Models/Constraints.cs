using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Person_1.Models
{
    public class Constraint
    {
        public double[] Coefficients { get; set; }
        public string Relation { get; set; }
        public double RightHandSide { get; set; }
        public ConstraintRelation ConstraintType { get; set; }

        public Constraint(double[] coefficients, string relation, double rhs)
        {
            Coefficients = coefficients ?? throw new ArgumentNullException(nameof(coefficients));
            Relation = relation ?? throw new ArgumentNullException(nameof(relation));
            RightHandSide = rhs;
            ConstraintType = ParseRelation(relation);
        }

        /// Parse string relation to enum type
        private ConstraintRelation ParseRelation(string relation)
        {
            if (relation == null)
                throw new ArgumentException("Relation cannot be null", nameof(relation));

            switch (relation.Trim())
            {
                case "<=":
                    return ConstraintRelation.LessOrEqual;
                case ">=":
                    return ConstraintRelation.GreaterOrEqual;
                case "=":
                    return ConstraintRelation.Equal;
                default:
                    throw new ArgumentException($"Invalid constraint relation: {relation}", nameof(relation));
            }
        }

        /// Get human-readable string representation of the constraint
        public override string ToString()
        {
            var coeffStr = string.Join(" ", Coefficients.Select((c, i) =>
                $"{(c >= 0 && i > 0 ? "+" : "")}{c:F3}x{i + 1}"));
            return $"{coeffStr} {Relation} {RightHandSide:F3}";
        }
    }
}


