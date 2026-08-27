using Person_1.Algorithmn;
using Person_1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Person_1.Core
{
    public class DualityAnalysis
    {
        public LinearProgrammingModel Primal { get; }
        public LinearProgrammingModel Dual { get; private set; }

        public DualityAnalysis(LinearProgrammingModel primal)
        {
            Primal = primal ?? throw new ArgumentNullException(nameof(primal));
        }

        // =====================================================================
        //  Apply Duality: construct the dual model
        // =====================================================================

        public LinearProgrammingModel BuildDual()
        {
            int n0 = Primal.Variables.Count;   // primal vars -> dual constraints
            int m0 = Primal.Constraints.Count; // primal constraints -> dual vars

            bool primalIsMax = Primal.ObjectiveType == ObjectiveType.Maximize;

            var dual = new LinearProgrammingModel
            {
                ObjectiveType = primalIsMax ? ObjectiveType.Minimize : ObjectiveType.Maximize,
                ObjectiveCoefficients = new double[m0],
                SourceFile = "(derived dual of " + (string.IsNullOrEmpty(Primal.SourceFile) ? "current model" : Primal.SourceFile) + ")",
                IsValid = true,
                IsValidated = true
            };

            // Dual objective = primal RHS vector
            for (int i = 0; i < m0; i++)
            {
                dual.ObjectiveCoefficients[i] = Primal.Constraints[i].RightHandSide;
                dual.Variables.Add(new Variable("y" + (i + 1), SignRestriction.NonNegative, i));
            }

            // Dual sign restrictions come from primal constraint relations
            for (int i = 0; i < m0; i++)
            {
                var sr = MapConstraintToDualVarRestriction(Primal.Constraints[i].ConstraintType, primalIsMax);
                dual.SignRestrictions.Add(sr);
                dual.Variables[i].SignRestriction = sr;
            }

            // Dual constraints come from primal variables (columns of A become rows of A^T)
            for (int j = 0; j < n0; j++)
            {
                var coeffs = new double[m0];
                for (int i = 0; i < m0; i++)
                    coeffs[i] = (j < Primal.Constraints[i].Coefficients.Length) ? Primal.Constraints[i].Coefficients[j] : 0.0;

                var primalRestriction = Primal.SignRestrictions.Count > j ? Primal.SignRestrictions[j] : SignRestriction.NonNegative;
                string relation = MapVarRestrictionToDualConstraintRelation(primalRestriction, primalIsMax);
                double rhs = Primal.ObjectiveCoefficients[j];

                dual.Constraints.Add(new Constraint(coeffs, relation, rhs));
            }

            Dual = dual;
            return dual;
        }

        private static SignRestriction MapConstraintToDualVarRestriction(ConstraintRelation rel, bool primalIsMax)
        {
            if (primalIsMax)
            {
                switch (rel)
                {
                    case ConstraintRelation.LessOrEqual: return SignRestriction.NonNegative;
                    case ConstraintRelation.GreaterOrEqual: return SignRestriction.NonPositive;
                    default: return SignRestriction.Unrestricted;
                }
            }
            else
            {
                switch (rel)
                {
                    case ConstraintRelation.GreaterOrEqual: return SignRestriction.NonNegative;
                    case ConstraintRelation.LessOrEqual: return SignRestriction.NonPositive;
                    default: return SignRestriction.Unrestricted;
                }
            }
        }

        private static string MapVarRestrictionToDualConstraintRelation(SignRestriction sr, bool primalIsMax)
        {
            // Integer/Binary primal variables are relaxed to NonNegative for duality purposes.
            bool nonNegative = sr == SignRestriction.NonNegative || sr == SignRestriction.Integer || sr == SignRestriction.Binary;
            bool nonPositive = sr == SignRestriction.NonPositive;

            if (primalIsMax)
            {
                if (nonNegative) return ">=";
                if (nonPositive) return "<=";
                return "="; // unrestricted
            }
            else
            {
                if (nonNegative) return "<=";
                if (nonPositive) return ">=";
                return "=";
            }
        }

        public string DescribeDual()
        {
            if (Dual == null) BuildDual();
            var sb = new StringBuilder();
            sb.AppendLine("DUAL MODEL");
            sb.AppendLine("==========");
            if (Primal.SignRestrictions.Any(s => s == SignRestriction.Integer || s == SignRestriction.Binary))
                sb.AppendLine("(Note: Integer/Binary primal variables were relaxed to continuous >= 0 to build the dual.)");

            var objTerms = Dual.ObjectiveCoefficients.Select((c, i) => $"{(c >= 0 && i > 0 ? "+" : "")}{c:0.000}y{i + 1}");
            sb.AppendLine($"{Dual.ObjectiveType.ToString().ToUpper()} w = {string.Join(" ", objTerms)}");
            sb.AppendLine("Subject to:");
            for (int i = 0; i < Dual.Constraints.Count; i++)
            {
                var c = Dual.Constraints[i];
                var terms = c.Coefficients.Select((v, k) => $"{(v >= 0 && k > 0 ? "+" : "")}{v:0.000}y{k + 1}");
                sb.AppendLine($"  {string.Join(" ", terms)} {c.Relation} {c.RightHandSide:0.000}");
            }
            sb.AppendLine("Sign Restrictions:");
            for (int i = 0; i < Dual.SignRestrictions.Count; i++)
            {
                string r = Dual.SignRestrictions[i] == SignRestriction.NonNegative ? ">= 0"
                         : Dual.SignRestrictions[i] == SignRestriction.NonPositive ? "<= 0"
                         : "unrestricted";
                sb.AppendLine($"  y{i + 1} {r}");
            }
            return sb.ToString();
        }

        // =====================================================================
        //  Solve the dual & verify strong/weak duality
        // =====================================================================

        public string VerifyDuality(PrimalSimplex.Result primalResult, PrimalSimplex.Result dualResult)
        {
            var sb = new StringBuilder();
            sb.AppendLine("DUALITY VERIFICATION");
            sb.AppendLine("=====================");

            if (primalResult.IsInfeasible && !dualResult.IsUnbounded)
                sb.AppendLine("Primal is infeasible. By weak duality, the dual is either infeasible or unbounded (check dual result).");
            else if (primalResult.IsUnbounded && !dualResult.IsInfeasible)
                sb.AppendLine("Primal is unbounded. By weak duality, the dual must be INFEASIBLE (check dual result).");

            if (primalResult.IsOptimal && dualResult.IsOptimal)
            {
                sb.AppendLine($"Primal optimal objective value: {primalResult.ObjectiveValue:0.000}");
                sb.AppendLine($"Dual optimal objective value:   {dualResult.ObjectiveValue:0.000}");

                double diff = Math.Abs(primalResult.ObjectiveValue - dualResult.ObjectiveValue);
                if (diff < 1e-4)
                {
                    sb.AppendLine("Result: primal and dual objective values are EQUAL.");
                    sb.AppendLine("=> STRONG DUALITY holds (as expected for a solvable LP at optimality).");
                }
                else
                {
                    sb.AppendLine($"Result: objective values differ by {diff:0.000}.");
                    sb.AppendLine("=> Only WEAK DUALITY is confirmed here; a strong-duality gap this large usually points to a");
                    sb.AppendLine("   modelling or relaxation issue (e.g. integer/binary variables were relaxed for the dual).");
                }
            }
            else
            {
                sb.AppendLine("Both primal and dual must reach an optimal solution to confirm strong duality numerically.");
            }

            return sb.ToString();
        }
    }
}
