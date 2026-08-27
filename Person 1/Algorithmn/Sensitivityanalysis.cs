using Person_1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Person_1.Algorithmn
{
    public class SensitivityAnalysis
    {
        private const double EPS = 1e-7;

        private readonly LinearProgrammingModel model;
        private readonly PrimalSimplex.Result result;

        
        private List<string> allColNames;   
        private double[,] Afull;            
        private double[] cFull;             
        private double[] bVec;              
        private int m;                     
        private int n0;                    
        private int nFull;                
        private double objSign;             
        
        private double[,] Binv;             
        private double[] cB;                
        private HashSet<string> basicNames; 

        public SensitivityAnalysis(LinearProgrammingModel model, PrimalSimplex.Result result)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.result = result ?? throw new ArgumentNullException(nameof(result));

            if (!result.IsOptimal)
                throw new InvalidOperationException("Sensitivity analysis requires an optimal solution.");

            RebuildCanonical();
            BuildBasisInverse();
        }


        private void RebuildCanonical()
        {
            n0 = model.Variables.Count;
            m = model.Constraints.Count;
            objSign = (model.ObjectiveType == ObjectiveType.Minimize) ? -1.0 : 1.0;

            
            var extra = new List<(int row, string name, double coeff)>();
            int sCount = 0, tCount = 0;
            for (int i = 0; i < m; i++)
            {
                var c = model.Constraints[i];
                if (c.ConstraintType == ConstraintRelation.LessOrEqual)
                {
                    sCount++;
                    extra.Add((i, "s" + sCount, +1.0));
                }
                else if (c.ConstraintType == ConstraintRelation.GreaterOrEqual)
                {
                    tCount++;
                    extra.Add((i, "t" + tCount, -1.0));
                }
                
            }

            nFull = n0 + extra.Count;
            Afull = new double[m, nFull];
            bVec = new double[m];
            cFull = new double[nFull];

            for (int i = 0; i < m; i++)
            {
                var c = model.Constraints[i];
                for (int j = 0; j < n0; j++)
                    Afull[i, j] = (j < c.Coefficients.Length) ? c.Coefficients[j] : 0.0;
                bVec[i] = c.RightHandSide;
            }

            for (int j = 0; j < n0; j++)
                cFull[j] = objSign * model.ObjectiveCoefficients[j];

            allColNames = new List<string>();
            for (int k = 1; k <= n0; k++) allColNames.Add("x" + k);
            for (int e = 0; e < extra.Count; e++)
            {
                var (row, name, coeff) = extra[e];
                Afull[row, n0 + e] = coeff;
                allColNames.Add(name);
               
            }
        }

        private void BuildBasisInverse()
        {
            var Bmat = new double[m, m];
            cB = new double[m];
            basicNames = new HashSet<string>(result.RowNames);

            for (int i = 0; i < m; i++)
            {
                string bName = result.RowNames[i];
                int idx = allColNames.IndexOf(bName);
                if (idx < 0)
                    throw new InvalidOperationException(
                        $"Cannot locate basic variable '{bName}' while rebuilding the basis. " +
                        "The solution may be degenerate or based on an artificial variable.");

                cB[i] = cFull[idx];
                for (int r = 0; r < m; r++)
                    Bmat[r, i] = Afull[r, idx];
            }

            Binv = Invert(Bmat);
        }

        private static double[,] Invert(double[,] B)
        {
            int n = B.GetLength(0);
            var aug = new double[n, 2 * n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) aug[i, j] = B[i, j];
                aug[i, n + i] = 1.0;
            }

            for (int col = 0; col < n; col++)
            {
                int piv = -1; double best = 1e-9;
                for (int r = col; r < n; r++)
                    if (Math.Abs(aug[r, col]) > best) { best = Math.Abs(aug[r, col]); piv = r; }

                if (piv < 0)
                    throw new InvalidOperationException(
                        "The basis matrix is singular (degenerate basis) - sensitivity analysis is unreliable here.");

                if (piv != col)
                    for (int c = 0; c < 2 * n; c++)
                    {
                        var tmp = aug[piv, c]; aug[piv, c] = aug[col, c]; aug[col, c] = tmp;
                    }

                double pivVal = aug[col, col];
                for (int c = 0; c < 2 * n; c++) aug[col, c] /= pivVal;

                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double factor = aug[r, col];
                    if (Math.Abs(factor) < 1e-12) continue;
                    for (int c = 0; c < 2 * n; c++) aug[r, c] -= factor * aug[col, c];
                }
            }

            var inv = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    inv[i, j] = aug[i, n + j];
            return inv;
        }

        private double[] MatVec(double[,] Mat, double[] v)
        {
            int rows = Mat.GetLength(0), cols = Mat.GetLength(1);
            var outv = new double[rows];
            for (int i = 0; i < rows; i++)
            {
                double sum = 0;
                for (int j = 0; j < cols; j++) sum += Mat[i, j] * v[j];
                outv[i] = sum;
            }
            return outv;
        }

        
        private double[] InternalShadowPrices()
        {
            var y = new double[m];
            for (int i = 0; i < m; i++)
            {
                double sum = 0;
                for (int k = 0; k < m; k++) sum += cB[k] * Binv[k, i];
                y[i] = sum;
            }
            return y;
        }

        private int FullColIndex(string name) => allColNames.IndexOf(name);

        private double[,] T => result.LastTableau;
        private int TN => result.ColumnNames.Length; // number of tableau columns (excludes RHS)

        public RangeResult RangeNonBasicVariableCost(int varIndex)
        {
            string name = "x" + (varIndex + 1);
            int col = Array.IndexOf(result.ColumnNames, name);
            if (col < 0) throw new ArgumentException($"{name} not found in the solved tableau.");
            if (basicNames.Contains(name)) throw new InvalidOperationException($"{name} is currently BASIC. Use RangeBasicVariableCost instead.");

            double rc = T[m, col];              
            double cInternal = cFull[FullColIndex(name)];

           
            double upperInternal = cInternal + rc;

            return ConvertCostRange(double.NegativeInfinity, upperInternal, true, false);
        }

        public string ApplyNonBasicVariableCostChange(int varIndex, double newCoefficient)
        {
            var range = RangeNonBasicVariableCost(varIndex);
            string name = "x" + (varIndex + 1);
            var sb = new StringBuilder();
            sb.AppendLine($"Changing the objective coefficient of {name} to {newCoefficient:0.000}.");
            sb.AppendLine($"Allowable range to keep the current basis optimal: {range}");

            if (range.Contains(newCoefficient))
            {
                sb.AppendLine("Result: the current solution and basis remain OPTIMAL (unchanged).");
                sb.AppendLine($"Objective value is unchanged: {result.ObjectiveValue:0.000}");
            }
            else
            {
                sb.AppendLine("Result: the change falls OUTSIDE the allowable range.");
                sb.AppendLine($"{name} would become attractive to enter the basis - the current solution is no longer guaranteed optimal.");
                sb.AppendLine("Update the coefficient in your input file and re-run the algorithm to find the new optimum.");
            }
            return sb.ToString();
        }

       
        public RangeResult RangeBasicVariableCost(int varIndex)
        {
            string name = "x" + (varIndex + 1);
            int row = Array.IndexOf(result.RowNames, name);
            if (row < 0) throw new ArgumentException($"{name} is not currently basic. Use RangeNonBasicVariableCost instead.");

            double loDelta = double.NegativeInfinity, hiDelta = double.PositiveInfinity;

            for (int j = 0; j < TN; j++)
            {
                string colName = result.ColumnNames[j];
                if (basicNames.Contains(colName)) continue; 

                double alpha = T[row, j];
                if (Math.Abs(alpha) < EPS) continue;
                double rc = T[m, j];
                double bound = -rc / alpha;

                if (alpha > 0) loDelta = Math.Max(loDelta, bound);
                else hiDelta = Math.Min(hiDelta, bound);
            }

            double cInternal = cFull[FullColIndex(name)];
            double lowerInternal = double.IsNegativeInfinity(loDelta) ? double.NegativeInfinity : cInternal + loDelta;
            double upperInternal = double.IsPositiveInfinity(hiDelta) ? double.PositiveInfinity : cInternal + hiDelta;

            return ConvertCostRange(lowerInternal, upperInternal,
                double.IsNegativeInfinity(lowerInternal), double.IsPositiveInfinity(upperInternal));
        }

        public string ApplyBasicVariableCostChange(int varIndex, double newCoefficient)
        {
            var range = RangeBasicVariableCost(varIndex);
            string name = "x" + (varIndex + 1);
            var sb = new StringBuilder();
            sb.AppendLine($"Changing the objective coefficient of {name} to {newCoefficient:0.000}.");
            sb.AppendLine($"Allowable range to keep the current basis optimal: {range}");

            if (range.Contains(newCoefficient))
            {
                sb.AppendLine("Result: the current basis remains OPTIMAL.");
                double delta = objSign * newCoefficient - cFull[FullColIndex(name)];
                double newObjInternal = InternalObjectiveValue() + delta * XValueOf(name);
                sb.AppendLine($"Values of the decision variables are unchanged.");
                sb.AppendLine($"New objective value: {objSign * newObjInternal:0.000}");
            }
            else
            {
                sb.AppendLine("Result: the change falls OUTSIDE the allowable range.");
                sb.AppendLine("The current basis is no longer guaranteed optimal.");
                sb.AppendLine("Update the coefficient in your input file and re-run the algorithm to find the new optimum.");
            }
            return sb.ToString();
        }

        
        public RangeResult RangeRHS(int constraintIndex)
        {
            if (constraintIndex < 0 || constraintIndex >= m) throw new ArgumentOutOfRangeException(nameof(constraintIndex));

            double[] d = new double[m];
            for (int k = 0; k < m; k++) d[k] = Binv[k, constraintIndex];

            double loDelta = double.NegativeInfinity, hiDelta = double.PositiveInfinity;
            for (int k = 0; k < m; k++)
            {
                double xb = T[k, TN]; 
                if (d[k] > EPS) loDelta = Math.Max(loDelta, -xb / d[k]);
                else if (d[k] < -EPS) hiDelta = Math.Min(hiDelta, -xb / d[k]);
            }

            double b = bVec[constraintIndex];
            double lower = double.IsNegativeInfinity(loDelta) ? double.NegativeInfinity : b + loDelta;
            double upper = double.IsPositiveInfinity(hiDelta) ? double.PositiveInfinity : b + hiDelta;

            return new RangeResult
            {
                Lower = lower,
                Upper = upper,
                LowerUnbounded = double.IsNegativeInfinity(lower),
                UpperUnbounded = double.IsPositiveInfinity(upper)
            };
        }

        public string ApplyRHSChange(int constraintIndex, double newRHS)
        {
            var range = RangeRHS(constraintIndex);
            var sb = new StringBuilder();
            sb.AppendLine($"Changing RHS of constraint {constraintIndex + 1} to {newRHS:0.000}.");
            sb.AppendLine($"Allowable range to keep the current basis feasible: {range}");

            double delta = newRHS - bVec[constraintIndex];

            if (range.Contains(newRHS))
            {
                double[] d = new double[m];
                for (int k = 0; k < m; k++) d[k] = Binv[k, constraintIndex];

                var xOrig = new double[n0];
                sb.AppendLine("Result: basis remains OPTIMAL and FEASIBLE. New solution:");
                for (int k = 0; k < m; k++)
                {
                    string bName = result.RowNames[k];
                    double newVal = T[k, TN] + delta * d[k];
                    int fullIdx = FullColIndex(bName);
                    if (fullIdx >= 0 && fullIdx < n0) xOrig[fullIdx] = newVal;
                    sb.AppendLine($"  {bName} = {newVal:0.000}");
                }
                for (int j = 0; j < n0; j++)
                {
                    string name = "x" + (j + 1);
                    if (!basicNames.Contains(name)) sb.AppendLine($"  {name} = 0.000");
                }

                double[] yInternal = InternalShadowPrices();
                double newObjInternal = InternalObjectiveValue() + delta * yInternal[constraintIndex];
                sb.AppendLine($"New objective value: {objSign * newObjInternal:0.000}");
            }
            else
            {
                sb.AppendLine("Result: the change falls OUTSIDE the allowable range.");
                sb.AppendLine("The current basis becomes INFEASIBLE (a basic variable would go negative).");
                sb.AppendLine("Update the RHS in your input file and re-run the algorithm to find the new optimum.");
            }
            return sb.ToString();
        }

       

        public RangeResult RangeNonBasicColumnCoefficient(int constraintIndex, int varIndex)
        {
            string name = "x" + (varIndex + 1);
            int col = Array.IndexOf(result.ColumnNames, name);
            if (col < 0) throw new ArgumentException($"{name} not found in the solved tableau.");
            if (basicNames.Contains(name))
                throw new InvalidOperationException($"{name} is currently BASIC; this operation applies to non-basic columns only.");
            if (constraintIndex < 0 || constraintIndex >= m) throw new ArgumentOutOfRangeException(nameof(constraintIndex));

            double[] yInternal = InternalShadowPrices();
            double yi = yInternal[constraintIndex];
            double rc = T[m, col]; 
            double loDelta = double.NegativeInfinity, hiDelta = double.PositiveInfinity;
            if (yi > EPS) loDelta = -rc / yi;
            else if (yi < -EPS) hiDelta = -rc / yi;
          

            double current = model.Constraints[constraintIndex].Coefficients[varIndex];
            double lower = double.IsNegativeInfinity(loDelta) ? double.NegativeInfinity : current + loDelta;
            double upper = double.IsPositiveInfinity(hiDelta) ? double.PositiveInfinity : current + hiDelta;

            return new RangeResult
            {
                Lower = lower,
                Upper = upper,
                LowerUnbounded = double.IsNegativeInfinity(lower),
                UpperUnbounded = double.IsPositiveInfinity(upper)
            };
        }

        public string ApplyNonBasicColumnCoefficientChange(int constraintIndex, int varIndex, double newValue)
        {
            var range = RangeNonBasicColumnCoefficient(constraintIndex, varIndex);
            string name = "x" + (varIndex + 1);
            var sb = new StringBuilder();
            sb.AppendLine($"Changing coefficient of {name} in constraint {constraintIndex + 1} to {newValue:0.000}.");
            sb.AppendLine($"Allowable range to keep the current basis optimal: {range}");

            if (range.Contains(newValue))
            {
                sb.AppendLine("Result: the current solution and basis remain OPTIMAL and FEASIBLE (unchanged).");
                sb.AppendLine($"Objective value is unchanged: {result.ObjectiveValue:0.000}");
            }
            else
            {
                sb.AppendLine("Result: the change falls OUTSIDE the allowable range.");
                sb.AppendLine($"{name} would become attractive to enter the basis - the current solution is no longer guaranteed optimal.");
                sb.AppendLine("Update the coefficient in your input file and re-run the algorithm to find the new optimum.");
            }
            return sb.ToString();
        }

       
        public string AddNewActivity(string name, double objCoeffOriginal, double[] constraintCoeffs)
        {
            if (constraintCoeffs == null || constraintCoeffs.Length != m)
                throw new ArgumentException($"Expected {m} constraint coefficients (one per constraint).");

            double cNewInternal = objSign * objCoeffOriginal;
            double[] yInternal = InternalShadowPrices();

            double zNewInternal = 0;
            for (int i = 0; i < m; i++) zNewInternal += yInternal[i] * constraintCoeffs[i];

            double reducedCost = zNewInternal - cNewInternal;

            double[] Y = MatVec(Binv, constraintCoeffs);

            var sb = new StringBuilder();
            sb.AppendLine($"Evaluating new activity '{name}' with objective coefficient {objCoeffOriginal:0.000}");
            sb.AppendLine($"and constraint coefficients [{string.Join(", ", constraintCoeffs.Select(v => v.ToString("0.000")))}].");
            sb.AppendLine();
            sb.AppendLine("Column this activity would have in the current (final) tableau:");
            for (int i = 0; i < m; i++)
                sb.AppendLine($"  row {i + 1} ({result.RowNames[i]}): {Y[i]:0.000}");
            sb.AppendLine($"Reduced cost (z_new - c_new, internal form): {reducedCost:0.000}");
            sb.AppendLine();

            if (reducedCost >= -EPS)
            {
                sb.AppendLine("Result: the current solution remains OPTIMAL with the new activity at 0.");
                sb.AppendLine($"It is not profitable to bring '{name}' into the solution.");
                sb.AppendLine($"Objective value is unchanged: {result.ObjectiveValue:0.000}");
            }
            else
            {
                sb.AppendLine("Result: the new activity has a favourable reduced cost.");
                sb.AppendLine($"'{name}' should ENTER the basis - the current solution is no longer optimal.");
                sb.AppendLine("Add it to your input file as an extra decision variable and re-run the algorithm.");
            }
            return sb.ToString();
        }

        
        public string AddNewConstraint(double[] coeffsOriginal, string relation, double rhs)
        {
            if (coeffsOriginal == null || coeffsOriginal.Length != n0)
                throw new ArgumentException($"Expected {n0} coefficients (one per decision variable).");

            double lhs = 0;
            for (int j = 0; j < n0; j++) lhs += coeffsOriginal[j] * result.X[j];

            var sb = new StringBuilder();
            sb.AppendLine($"Checking new constraint against the current optimal solution:");
            sb.AppendLine($"  LHS at current solution = {lhs:0.000}   ({relation} {rhs:0.000})");

            bool satisfied;
            switch (relation.Trim())
            {
                case "<=": satisfied = lhs <= rhs + EPS; break;
                case ">=": satisfied = lhs >= rhs - EPS; break;
                case "=": satisfied = Math.Abs(lhs - rhs) <= EPS; break;
                default: throw new ArgumentException("Relation must be <=, >= or =.");
            }

            if (satisfied)
            {
                sb.AppendLine("Result: the current optimal solution already SATISFIES the new constraint.");
                sb.AppendLine("The solution and objective value are unchanged (the constraint is non-binding).");
                return sb.ToString();
            }

            sb.AppendLine("Result: the current optimal solution VIOLATES the new constraint.");

            if (relation.Trim() != "<=")
            {
                sb.AppendLine("This solver's automatic recovery only handles '<=' constraints.");
                sb.AppendLine("Add the constraint to your input file and re-run the chosen algorithm from scratch.");
                return sb.ToString();
            }

            
            var newRow = new double[TN];
            for (int j = 0; j < n0 && j < TN; j++) newRow[j] = coeffsOriginal[j];
            double newRhsVal = rhs;

            for (int i = 0; i < m; i++)
            {
                string bName = result.RowNames[i];
                int fullIdx = FullColIndex(bName);
                double coeffAtBasic = (fullIdx >= 0 && fullIdx < n0) ? coeffsOriginal[fullIdx] : 0.0;
                if (Math.Abs(coeffAtBasic) < EPS) continue;

                for (int j = 0; j < TN; j++) newRow[j] -= coeffAtBasic * T[i, j];
                newRhsVal -= coeffAtBasic * T[i, TN];
            }

            sb.AppendLine();
            sb.AppendLine("New constraint expressed in terms of the current basis (new slack row):");
            for (int j = 0; j < TN; j++)
                if (Math.Abs(newRow[j]) > EPS) sb.AppendLine($"  {result.ColumnNames[j]}: {newRow[j]:0.000}");
            sb.AppendLine($"  RHS (with new slack basic): {newRhsVal:0.000}");

            if (newRhsVal >= -EPS)
            {
                sb.AppendLine("Result: after projection the row is already feasible (numerically); no pivot required.");
                return sb.ToString();
            }

            // Dual simplex ratio test
            int enter = -1; double bestRatio = double.PositiveInfinity;
            for (int j = 0; j < TN; j++)
            {
                if (newRow[j] < -EPS)
                {
                    double ratio = T[m, j] / (-newRow[j]);
                    if (ratio < bestRatio - 1e-12) { bestRatio = ratio; enter = j; }
                }
            }

            if (enter < 0)
            {
                sb.AppendLine();
                sb.AppendLine("No entering variable satisfies the dual-simplex ratio test:");
                sb.AppendLine("the model becomes INFEASIBLE once this constraint is added.");
                return sb.ToString();
            }

            sb.AppendLine();
            sb.AppendLine($"Dual-simplex pivot: entering '{result.ColumnNames[enter]}', leaving the new constraint's slack.");

            // Build an extended tableau: existing m+1 rows/TN+1 cols, plus one new row.
            int newM = m + 1;
            var Tnew = new double[newM + 1, TN + 1];
            for (int i = 0; i < m; i++)
                for (int j = 0; j <= TN; j++)
                    Tnew[i, j] = (j < TN) ? T[i, j] : T[i, TN];
            for (int j = 0; j < TN; j++) Tnew[m, j] = newRow[j];
            Tnew[m, TN] = newRhsVal;
            for (int j = 0; j < TN; j++) Tnew[newM, j] = T[this.m, j]; // objective row copied
            Tnew[newM, TN] = T[this.m, TN];

            // Pivot on (row m, col enter)
            double piv = Tnew[m, enter];
            for (int j = 0; j <= TN; j++) Tnew[m, j] /= piv;
            for (int i = 0; i <= newM; i++)
            {
                if (i == m) continue;
                double factor = Tnew[i, enter];
                if (Math.Abs(factor) < 1e-12) continue;
                for (int j = 0; j <= TN; j++) Tnew[i, j] -= factor * Tnew[m, j];
            }

            sb.AppendLine();
            sb.AppendLine("Updated solution after restoring feasibility:");
            var newRowNames = result.RowNames.ToList();
            newRowNames[m < newRowNames.Count ? 0 : 0] = newRowNames.Count > 0 ? newRowNames[0] : ""; // no-op guard
            newRowNames = result.RowNames.ToList();
            newRowNames.Add(result.ColumnNames[enter]);
            // The row that used to be 'enter' variable's home doesn't exist for original rows;
            // we only track that the new row's basic variable is now 'enter'.
            for (int i = 0; i < m; i++)
                sb.AppendLine($"  {result.RowNames[i]} = {Tnew[i, TN]:0.000}");
            sb.AppendLine($"  {result.ColumnNames[enter]} (from new row) = {Tnew[m, TN]:0.000}");
            sb.AppendLine($"New objective value: {objSign * Tnew[newM, TN]:0.000}");
            sb.AppendLine();
            sb.AppendLine("NOTE: for a permanent change, add this constraint to your input file and re-run the algorithm;");
            sb.AppendLine("the figures above are provided as an immediate what-if answer.");

            return sb.ToString();
        }

        // =====================================================================
        //  11. Shadow prices
        // =====================================================================

        public double[] ShadowPrices()
        {
            double[] yInternal = InternalShadowPrices();
            var y = new double[m];
            for (int i = 0; i < m; i++) y[i] = objSign * yInternal[i];
            return y;
        }

        public string DisplayShadowPrices()
        {
            var y = ShadowPrices();
            var sb = new StringBuilder();
            sb.AppendLine("Shadow prices (marginal value of one more unit of RHS, original problem sense):");
            for (int i = 0; i < m; i++)
                sb.AppendLine($"  Constraint {i + 1} ({model.Constraints[i]}): {y[i]:0.000}");
            return sb.ToString();
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private double InternalObjectiveValue() => objSign * result.ObjectiveValue;

        private double XValueOf(string name)
        {
            int row = Array.IndexOf(result.RowNames, name);
            return row >= 0 ? T[row, TN] : 0.0;
        }

        /// <summary>Converts an internal-form [lower, upper] cost range to the original problem's sense.</summary>
        private RangeResult ConvertCostRange(double lowerInternal, double upperInternal, bool lowerUnbounded, bool upperUnbounded)
        {
            if (objSign > 0)
            {
                return new RangeResult { Lower = lowerInternal, Upper = upperInternal, LowerUnbounded = lowerUnbounded, UpperUnbounded = upperUnbounded };
            }
            // Minimize: original c = -internal c, so the range flips.
            return new RangeResult
            {
                Lower = upperUnbounded ? double.NegativeInfinity : -upperInternal,
                Upper = lowerUnbounded ? double.PositiveInfinity : -lowerInternal,
                LowerUnbounded = upperUnbounded,
                UpperUnbounded = lowerUnbounded
            };
        }
    }

    /// <summary>A simple allowable range, with support for unbounded ends.</summary>
    public struct RangeResult
    {
        public double Lower;
        public double Upper;
        public bool LowerUnbounded;
        public bool UpperUnbounded;

        public bool Contains(double value)
        {
            bool okLow = LowerUnbounded || value >= Lower - 1e-6;
            bool okHigh = UpperUnbounded || value <= Upper + 1e-6;
            return okLow && okHigh;
        }

        public override string ToString()
        {
            string lo = LowerUnbounded ? "-\u221e" : Lower.ToString("0.000");
            string hi = UpperUnbounded ? "+\u221e" : Upper.ToString("0.000");
            return $"[{lo}, {hi}]";
        }
    }
}