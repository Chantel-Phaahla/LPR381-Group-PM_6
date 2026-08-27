using Person_1.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Person_1.Core
{
    public class OutputWriter
    {
        public string GenerateOutput(LinearProgrammingModel model, string sourceFile)
        {
            var output = new StringBuilder();

            // Header information
            output.AppendLine("INPUT FILE ANALYSIS");
            output.AppendLine("==================");
            output.AppendLine($"Source File: {sourceFile}");
            output.AppendLine($"Parsed on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            // Fix: Convert TimeSpan to total seconds before formatting
            var duration = DateTime.Now - model.CreatedAt;
            output.AppendLine($"Analysis Duration: {duration.TotalSeconds:F2} seconds");
            output.AppendLine();

            // Model statistics
            output.AppendLine(GenerateModelStatistics(model));
            output.AppendLine();

            // Canonical form
            output.AppendLine(GenerateCanonicalForm(model));
            output.AppendLine();

            // Parsing errors and warnings
            if (model.ParsingErrors.Count > 0)
            {
                output.AppendLine("PARSING ERRORS AND WARNINGS");
                output.AppendLine("===========================");
                foreach (var error in model.ParsingErrors)
                {
                    output.AppendLine($"• {error}");
                }
                output.AppendLine();
            }

            // Model analysis
            output.AppendLine(GenerateModelAnalysis(model));

            return output.ToString();
        }
        /// Generate model statistics section
        private string GenerateModelStatistics(LinearProgrammingModel model)
        {
            var stats = model.GetStatistics();
            var output = new StringBuilder();

            output.AppendLine("MODEL STATISTICS");
            output.AppendLine("================");
            output.AppendLine($"Variables: {stats.VariableCount}");
            output.AppendLine($"Constraints: {stats.ConstraintCount}");
            output.AppendLine($"Integer Variables: {stats.IntegerVariableCount}");
            output.AppendLine($"Binary Variables: {stats.BinaryVariableCount}");
            output.AppendLine($"Unrestricted Variables: {stats.UnrestrictedVariableCount}");
            output.AppendLine($"Parsing Errors: {stats.ErrorCount}");
            output.AppendLine($"Model Valid: {(stats.IsValid ? "Yes" : "No")}");

            return output.ToString();
        }

        /// Generate canonical form representation
        private string GenerateCanonicalForm(LinearProgrammingModel model)
        {
            var output = new StringBuilder();
            output.AppendLine("CANONICAL FORM");
            output.AppendLine("==============");

            try
            {
                // Objective function
                output.Append($"{model.ObjectiveType.ToString().ToUpper()} z = ");
                var objTerms = model.ObjectiveCoefficients.Select((c, i) =>
                    $"{(c >= 0 && i > 0 ? "+" : "")}{c.ToString("F6", CultureInfo.InvariantCulture)}x{i + 1}");
                output.AppendLine(string.Join(" ", objTerms));

                output.AppendLine("\nSubject to:");

                // Constraints
                for (int i = 0; i < model.Constraints.Count; i++)
                {
                    var constraint = model.Constraints[i];
                    var terms = constraint.Coefficients.Select((c, j) =>
                        $"{(c >= 0 && j > 0 ? "+" : "")}{c.ToString("F6", CultureInfo.InvariantCulture)}x{j + 1}");
                    output.AppendLine($"  {string.Join(" ", terms)} {constraint.Relation} {constraint.RightHandSide.ToString("F6", CultureInfo.InvariantCulture)}");
                }

                // Sign restrictions
                output.AppendLine("\nSign Restrictions:");
                for (int i = 0; i < model.SignRestrictions.Count; i++)
                {
                    string restriction = GetSignRestrictionString(model.SignRestrictions[i]);
                    output.AppendLine($"  x{i + 1} {restriction}");
                }
            }
            catch (Exception ex)
            {
                output.AppendLine($"\nError generating canonical form: {ex.Message}");
            }

            return output.ToString();
        }

        /// Generate comprehensive model analysis
        private string GenerateModelAnalysis(LinearProgrammingModel model)
        {
            var output = new StringBuilder();
            output.AppendLine("MODEL ANALYSIS");
            output.AppendLine("==============");

            // Problem classification
            output.AppendLine("Problem Classification:");
            if (model.IsBinaryProgramming())
            {
                output.AppendLine("  Type: Binary Integer Programming (BIP)");
            }
            else if (model.IsIntegerProgramming())
            {
                output.AppendLine("  Type: Mixed Integer Programming (MIP)");
            }
            else
            {
                output.AppendLine("  Type: Linear Programming (LP)");
            }

            output.AppendLine($"  Objective: {model.ObjectiveType}");
            output.AppendLine($"  Variables: {model.Variables.Count}");
            output.AppendLine($"  Constraints: {model.Constraints.Count}");

            // Variable type distribution
            var varTypes = model.GetVariableTypeDistribution();
            output.AppendLine("\nVariable Distribution:");
            foreach (var kvp in varTypes)
            {
                output.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }

            // Constraint type distribution
            var constraintTypes = model.GetConstraintTypeDistribution();
            output.AppendLine("\nConstraint Distribution:");
            foreach (var kvp in constraintTypes)
            {
                output.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }

            // Recommendations
            output.AppendLine(GenerateRecommendations(model));

            return output.ToString();
        }

       
        private string GenerateRecommendations(LinearProgrammingModel model)
        {
            var output = new StringBuilder();
            output.AppendLine("\nALGORITHM RECOMMENDATIONS");
            output.AppendLine("=========================");

            if (model.IsBinaryProgramming())
            {
                output.AppendLine("Recommended Algorithms:");
                output.AppendLine("  • Branch and Bound for Binary Problems");
                output.AppendLine("  • Dynamic Programming (for knapsack-type problems)");
                output.AppendLine("  • Cutting Plane Methods");
            }
            else if (model.IsIntegerProgramming())
            {
                output.AppendLine("Recommended Algorithms:");
                output.AppendLine("  • Branch and Bound");
                output.AppendLine("  • Cutting Plane Algorithm");
                output.AppendLine("  • Branch and Cut");
            }
            else
            {
                output.AppendLine("Recommended Algorithms:");
                output.AppendLine("  • Simplex Method (standard choice)");
                output.AppendLine("  • Revised Simplex Method (for larger problems)");
                output.AppendLine("  • Interior Point Methods (for very large problems)");
            }

            // The Special considerations
            if (model.SignRestrictions.Any(s => s == SignRestriction.Unrestricted))
            {
                output.AppendLine("\nSpecial Considerations:");
                output.AppendLine("  • Unrestricted variables require substitution (x = x+ - x-)");
            }

            if (model.Constraints.Count == 0)
            {
                output.AppendLine("\nWarning:");
                output.AppendLine("  • No constraints detected - problem may be unbounded");
            }

            return output.ToString();
        }

        /// Converting the sign restriction enum to readable string
        private string GetSignRestrictionString(SignRestriction restriction)
        {
            switch (restriction)
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
    }
}