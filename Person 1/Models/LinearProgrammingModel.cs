using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Person_1.Models
{
    public class LinearProgrammingModel
    {
        public bool IsValidated { get; set; } = false;
        public ObjectiveType ObjectiveType { get; set; }
        public double[] ObjectiveCoefficients { get; set; }
        public List<Variable> Variables { get; set; }
        public List<Constraint> Constraints { get; set; }
        public List<SignRestriction> SignRestrictions { get; set; }
        public List<string> ParsingErrors { get; set; }
        public DateTime CreatedAt { get; set; }
        public string SourceFile { get; set; }

        /// Indicates if the model contains any parsing errors
        public bool HasErrors => ParsingErrors.Count > 0;

        public bool IsValid { get; set; }

        public LinearProgrammingModel()
        {
            Variables = new List<Variable>();
            Constraints = new List<Constraint>();
            SignRestrictions = new List<SignRestriction>();
            ParsingErrors = new List<string>();
            ObjectiveCoefficients = new double[0];
            CreatedAt = DateTime.Now;
            SourceFile = string.Empty;
            IsValid = false;
        }

        /// Check if model contains integer programming variables
        public bool IsIntegerProgramming()
        {
            return SignRestrictions.Any(sr => sr == SignRestriction.Integer || sr == SignRestriction.Binary);
        }

        /// Check if model is purely binary programming
        public bool IsBinaryProgramming()
        {
            return SignRestrictions.Count > 0 && SignRestrictions.All(sr => sr == SignRestriction.Binary);
        }

        /// Check if model has mixed variable types
        public bool HasMixedVariables()
        {
            return SignRestrictions.Distinct().Count() > 1;
        }

        /// Validate if model is ready for solving algorithms
        public bool IsValidForSolving()
        {
            return !ParsingErrors.Any(e => !e.StartsWith("Info:")) &&
                   Variables.Count > 0 &&
                   Constraints.Count > 0 &&
                   ObjectiveCoefficients.Length == Variables.Count &&
                   SignRestrictions.Count == Variables.Count;
        }

        /// Get count of variables by restriction type
        public Dictionary<SignRestriction, int> GetVariableTypeDistribution()
        {
            return SignRestrictions.GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());
        }

        /// Get count of constraints by relation type
        public Dictionary<string, int> GetConstraintTypeDistribution()
        {
            return Constraints.GroupBy(c => c.Relation).ToDictionary(g => g.Key, g => g.Count());
        }

        /// Get basic model statistics
        public ModelStatistics GetStatistics()
        {
            return new ModelStatistics
            {
                VariableCount = Variables.Count,
                ConstraintCount = Constraints.Count,
                IntegerVariableCount = SignRestrictions.Count(s => s == SignRestriction.Integer),
                BinaryVariableCount = SignRestrictions.Count(s => s == SignRestriction.Binary),
                UnrestrictedVariableCount = SignRestrictions.Count(s => s == SignRestriction.Unrestricted),
                ErrorCount = ParsingErrors.Count,
                IsValid = IsValidForSolving()
            };
        }
    }

    /// Contains statistical information about a linear programming model>
    public class ModelStatistics
    {
        public int VariableCount { get; set; }
        public int ConstraintCount { get; set; }
        public int IntegerVariableCount { get; set; }
        public int BinaryVariableCount { get; set; }
        public int UnrestrictedVariableCount { get; set; }
        public int ErrorCount { get; set; }
        public bool ReadyForSolving { get; set; } = false;
        public bool IsValid { get; set; } = false;

        public override string ToString()
        {
            return $"Variables: {VariableCount}, Constraints: {ConstraintCount}, " +
                   $"Integer: {IntegerVariableCount}, Binary: {BinaryVariableCount}, " +
                   $"Errors: {ErrorCount}, Valid: {IsValid}";
        }
    }
}