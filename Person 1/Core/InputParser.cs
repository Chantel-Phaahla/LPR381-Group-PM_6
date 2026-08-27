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
    // This is the comprehensive file parser for the Linear Programming models
    public class InputParser
    {
        public LinearProgrammingModel Parse(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    "Input file could not be found.",
                    filePath);
            }

            string[] lines = File.ReadAllLines(filePath);

            if (lines.Length < 3)
            {
                throw new Exception(
                    "Input file must contain an objective, at least one constraint, and sign restrictions.");
            }

            LinearProgrammingModel model = new LinearProgrammingModel();
            model.SourceFile = filePath;

            // Parse objective line
            ParseObjective(lines[0], model);

            // Parse constraint lines
            ParseConstraints(lines, model);

            // Parse sign restrictions (last line) - FIXED: Removed ^1 operator
            ParseSignRestrictions(lines[lines.Length - 1], model);

            // Validate the model
            ValidateModel(model);

            return model;
        }

        private void ParseObjective(string line, LinearProgrammingModel model)
        {
            // FIXED: Using split without StringSplitOptions for compatibility
            string[] tokens = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length < 2)
            {
                model.ParsingErrors.Add("Objective line must contain 'max/min' and at least one coefficient.");
                return;
            }

            // Parse objective type (max or min)
            string objectiveType = tokens[0].ToLower();
            if (objectiveType == "max")
            {
                model.ObjectiveType = ObjectiveType.Maximize;
            }
            else if (objectiveType == "min")
            {
                model.ObjectiveType = ObjectiveType.Minimize;
            }
            else
            {
                model.ParsingErrors.Add($"Invalid objective type: {tokens[0]}. Must be 'max' or 'min'.");
                return;
            }

            // Parse objective coefficients
            List<double> coefficients = new List<double>();
            for (int i = 1; i < tokens.Length; i++)
            {
                try
                {
                    double coeff = ParseSignedNumber(tokens[i]);
                    coefficients.Add(coeff);
                }
                catch (Exception ex)
                {
                    model.ParsingErrors.Add($"Error parsing coefficient '{tokens[i]}': {ex.Message}");
                }
            }

            model.ObjectiveCoefficients = coefficients.ToArray();

            // Create Variable objects for each coefficient
            model.Variables.Clear();
            for (int i = 0; i < coefficients.Count; i++)
            {
                model.Variables.Add(new Variable($"x{i + 1}", SignRestriction.NonNegative, i));
            }
        }

        private void ParseConstraints(string[] lines, LinearProgrammingModel model)
        {
            int variableCount = model.ObjectiveCoefficients.Length;

            if (variableCount == 0)
            {
                model.ParsingErrors.Add("Cannot parse constraints: No variables defined in objective.");
                return;
            }

            for (int lineIndex = 1; lineIndex < lines.Length - 1; lineIndex++)
            {
                string line = lines[lineIndex];
                string[] rawTokens = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // Handle relation stuck to RHS with no space, e.g. "<=40" -> "<=", "40"
                var tokens = new List<string>();
                foreach (var token in rawTokens)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(token, @"^(<=|>=|=)(-?\d+\.?\d*)$");
                    if (match.Success)
                    {
                        tokens.Add(match.Groups[1].Value);
                        tokens.Add(match.Groups[2].Value);
                    }
                    else
                    {
                        tokens.Add(token);
                    }
                }

                if (tokens.Count < variableCount + 2)
                {
                    model.ParsingErrors.Add($"Constraint line {lineIndex + 1} has insufficient tokens. " +
                        $"Expected {variableCount + 2}, got {tokens.Count}");
                    continue;
                }

                try
                {
                    double[] coefficients = new double[variableCount];
                    for (int j = 0; j < variableCount; j++)
                    {
                        coefficients[j] = ParseSignedNumber(tokens[j]);
                    }

                    string relation = tokens[variableCount];
                    double rhs = double.Parse(tokens[variableCount + 1]);

                    Constraint constraint = new Constraint(coefficients, relation, rhs);
                    model.Constraints.Add(constraint);
                }
                catch (Exception ex)
                {
                    model.ParsingErrors.Add($"Error parsing constraint line {lineIndex + 1}: {ex.Message}");
                }
            }
        }

        private void ParseSignRestrictions(string line, LinearProgrammingModel model)
        {
            // FIXED: Using split without StringSplitOptions for compatibility
            string[] tokens = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            model.SignRestrictions.Clear();

            foreach (string token in tokens)
            {
                string sign = token.ToLower();
                SignRestriction restriction;

                switch (sign)
                {
                    case "+":
                        restriction = SignRestriction.NonNegative;
                        break;
                    case "-":
                        restriction = SignRestriction.NonPositive;
                        break;
                    case "urs":
                        restriction = SignRestriction.Unrestricted;
                        break;
                    case "int":
                        restriction = SignRestriction.Integer;
                        break;
                    case "bin":
                        restriction = SignRestriction.Binary;
                        break;
                    default:
                        model.ParsingErrors.Add($"Invalid sign restriction: '{token}'. Valid values: +, -, urs, int, bin");
                        continue;
                }

                model.SignRestrictions.Add(restriction);
            }

            // Update Variables with sign restrictions
            int count = Math.Min(model.Variables.Count, model.SignRestrictions.Count);
            for (int i = 0; i < count; i++)
            {
                model.Variables[i].SignRestriction = model.SignRestrictions[i];
            }

            // Check if sign restrictions count matches variables count
            if (model.SignRestrictions.Count != model.Variables.Count)
            {
                model.ParsingErrors.Add($"Sign restrictions count ({model.SignRestrictions.Count}) " +
                    $"does not match variable count ({model.Variables.Count})");
            }
        }

        private double ParseSignedNumber(string value)
        {
            // Remove any leading + sign if present
            string cleanValue = value.TrimStart('+');
            return double.Parse(cleanValue, System.Globalization.CultureInfo.InvariantCulture);
        }

        private void ValidateModel(LinearProgrammingModel model)
        {
            // Check if there are any errors
            if (model.ParsingErrors.Any())
            {
                model.IsValid = false;
                return;
            }

            // Check if objective coefficients are set
            if (model.ObjectiveCoefficients.Length == 0)
            {
                model.ParsingErrors.Add("No objective coefficients defined.");
                model.IsValid = false;
                return;
            }

            // Check if constraints exist
            if (model.Constraints.Count == 0)
            {
                model.ParsingErrors.Add("No constraints defined.");
                model.IsValid = false;
                return;
            }

            // Check if all constraints have correct number of coefficients
            foreach (var constraint in model.Constraints)
            {
                if (constraint.Coefficients.Length != model.ObjectiveCoefficients.Length)
                {
                    model.ParsingErrors.Add($"Constraint '{constraint}' has {constraint.Coefficients.Length} coefficients, " +
                        $"but expected {model.ObjectiveCoefficients.Length}.");
                }
            }

            // Check if sign restrictions are set
            if (model.SignRestrictions.Count != model.Variables.Count)
            {
                model.ParsingErrors.Add($"Sign restriction mismatch: {model.SignRestrictions.Count} restrictions " +
                    $"for {model.Variables.Count} variables.");
            }

            model.IsValid = model.ParsingErrors.Count == 0;
            model.IsValidated = true;
        }
    }
}