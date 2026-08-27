using Person_1.Core;
using Person_1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Person_1.Algorithmn
{
    public class PrimalSimplex
    {
        private const double EPS = 1e-9;

        public sealed class Result
        {
            public bool IsOptimal;
            public bool IsUnbounded;
            public bool IsInfeasible;
            public double ObjectiveValue;
            public double[] X;
            public double[] ShadowPrices;     
            public double[,] BInverse;// The original variable order
            public string[] ColumnNames;         // columns without the RHS
            public string[] RowNames;
            public double[,] LastTableau;        // final tableau (m+1 x n+1)
        }

        private sealed class Canonical
        {
            public int m, n;                     // rows and columns
            public double[,] A;
            public double[] b;
            public double[] c;
            public List<string> ColNames;
            public string[] RowNames;
            public int[] Basic;
            public List<int> ArtificialCols;
            public int OrigVarCount;
            public double ObjSign;
        }

        public Result Solve(LinearProgrammingModel model, IterationLog log)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (model.Variables.Count == 0 || model.Constraints.Count == 0)
                throw new InvalidOperationException("Model has no variables or constraints.");

            // Build canonical form
            Canonical K = BuildCanonical(model);

            // Optional: quick print of canonical rows
            log.Title("Canonical form");
            log.Note("Columns: " + string.Join(", ", K.ColNames));
            for (int i = 0; i < K.m; i++)
            {
                var terms = new List<string>();
                for (int j = 0; j < K.n; j++)
                {
                    var v = K.A[i, j];
                    if (Math.Abs(v) < 1e-12) continue;
                    string s = (v >= 0 && terms.Count > 0 ? "+" : "") + v.ToString("0.###") + "*" + K.ColNames[j];
                    terms.Add(s);
                }
                log.Note($"{K.RowNames[i]}: {(terms.Count > 0 ? string.Join(" ", terms) : "0")} = {K.b[i]:0.###}");
            }

           
            Result phaseI = null;
            if (K.ArtificialCols.Count > 0)
            {
                double[] c1 = new double[K.n];
                foreach (int j in K.ArtificialCols) c1[j] = -1.0; // maximize -sum(a)
                phaseI = RunSimplex(K, c1, log, "Primal Simplex (Phase I)");

               
                if (!phaseI.IsOptimal || phaseI.ObjectiveValue < -1e-6)
                {
                    phaseI.IsInfeasible = true;
                    return phaseI;
                }

                
                K = BuildPhaseIIFromPhaseI(K);
            }

            // Phase II (original objective)
            Result res = RunSimplex(K, K.c, log, "Primal Simplex (Phase II)");
            return MapBackToOriginal(model, K, res);
        }

        // ===== Canonicalization =====
        private Canonical BuildCanonical(LinearProgrammingModel m)
        {
            int n0 = m.Variables.Count;     // original variable count
            int m0 = m.Constraints.Count;
            double objSign = (m.ObjectiveType == ObjectiveType.Minimize) ? -1.0 : 1.0;

            // names for original variables
            var baseNames = Enumerable.Range(1, n0).Select(k => "x" + k).ToList();

            var rows = new List<double[]>();         // each is length n0 (original x columns)
            var rhs = new List<double>();             // RHS per row

            var extraCols = new List<double[]>();     // each entry is a COLUMN vector built as rows grow
            var extraNames = new List<string>();      // names for extra columns (s/t/a)

            var basic = new List<int>();              // FINAL column index per row (after extras appended)
            var artificials = new List<int>();        // FINAL column indices for artificials

            int sCount = 0, tCount = 0, aCount = 0;

            // Build by constraint
            for (int i = 0; i < m0; i++)
            {
                var cons = m.Constraints[i];

                var row = new double[n0];
                for (int j = 0; j < Math.Min(n0, cons.Coefficients.Length); j++)
                    row[j] = cons.Coefficients[j];

                // transform via slack/surplus/artificial
                if (cons.Relation == "<=")
                {
                    int idxS;
                    AddNewColumn(extraCols, rows.Count, +1.0, out idxS);
                    extraNames.Add("s" + (++sCount));
                    basic.Add(n0 + idxS);             // slack is basic
                }
                else if (cons.Relation == ">=")
                {
                    int idxT;
                    AddNewColumn(extraCols, rows.Count, -1.0, out idxT);  // surplus is not basic
                    extraNames.Add("t" + (++tCount));

                    int idxA;
                    AddNewColumn(extraCols, rows.Count, +1.0, out idxA);  // artificial is basic
                    extraNames.Add("a" + (++aCount));
                    basic.Add(n0 + idxA);
                    artificials.Add(n0 + idxA);
                }
                else // "="
                {
                    int idxA;
                    AddNewColumn(extraCols, rows.Count, +1.0, out idxA);  // artificial is basic
                    extraNames.Add("a" + (++aCount));
                    basic.Add(n0 + idxA);
                    artificials.Add(n0 + idxA);
                }

                rows.Add(row);
                rhs.Add(cons.RightHandSide);
            }

            // Build final A by composing base plus the extra columns
            int m1 = rows.Count;
            int n1 = n0 + extraCols.Count;

            var A = new double[m1, n1];
            for (int i = 0; i < m1; i++)
            {
                var r = rows[i];
                for (int j = 0; j < n0; j++) A[i, j] = (j < r.Length) ? r[j] : 0.0;
                for (int k = 0; k < extraCols.Count; k++)
                {
                    var col = extraCols[k];
                    A[i, n0 + k] = (i < col.Length) ? col[i] : 0.0;
                }
            }

            var bvec = rhs.ToArray();

            // compose final column names
            var finalColNames = new List<string>(n1);
            finalColNames.AddRange(baseNames);
            finalColNames.AddRange(extraNames);

            // row (basic variable) names = the names of their basic columns
            var rowNames = new string[m1];
            for (int i = 0; i < m1; i++)
                rowNames[i] = finalColNames[basic[i]];

            // objective in final columns (only original variables keep coefficients)
            var c = new double[n1];
            for (int j = 0; j < n0; j++) c[j] = objSign * m.ObjectiveCoefficients[j];

            return new Canonical
            {
                m = m1,
                n = n1,
                A = A,
                b = bvec,
                c = c,
                ColNames = finalColNames,
                RowNames = rowNames,
                Basic = basic.ToArray(),
                ArtificialCols = artificials,
                OrigVarCount = n0,
                ObjSign = objSign
            };
        }

        // Remove artificial columns after Phase I and rebuild a basis if needed
        private Canonical BuildPhaseIIFromPhaseI(Canonical K)
        {
            bool[] drop = new bool[K.n];
            foreach (int j in K.ArtificialCols) drop[j] = true;

            int n2 = 0;
            var map = new int[K.n];
            for (int j = 0; j < K.n; j++)
            {
                if (!drop[j]) { map[j] = n2; n2++; }
                else map[j] = -1;
            }

            var A2 = new double[K.m, n2];
            var c2 = new double[n2];
            var names2 = new List<string>(n2);

            for (int j = 0; j < K.n; j++)
            {
                if (map[j] < 0) continue;
                names2.Add(K.ColNames[j]);
                c2[map[j]] = (j < K.c.Length) ? K.c[j] : 0.0;
                for (int i = 0; i < K.m; i++) A2[i, map[j]] = K.A[i, j];
            }

            // Rebuild a basic set by detecting unit columns per row
            var basic2 = new int[K.m];
            var rowNames2 = new string[K.m];
            for (int i = 0; i < K.m; i++)
            {
                int bj = FindUnitColumn(A2, i);
                basic2[i] = bj >= 0 ? bj : 0;
                rowNames2[i] = names2[basic2[i]];
            }

            return new Canonical
            {
                m = K.m,
                n = n2,
                A = A2,
                b = (double[])K.b.Clone(),
                c = c2,
                ColNames = names2,
                RowNames = rowNames2,
                Basic = basic2,
                ArtificialCols = new List<int>(),
                OrigVarCount = K.OrigVarCount,
                ObjSign = K.ObjSign
            };
        }

        // ===== Simplex core =====
        private Result RunSimplex(Canonical K, double[] cUse, IterationLog log, string header)
        {
            log.Title(header);

            int m = K.m, n = K.n;
            var T = new double[m + 1, n + 1];

            // Copy A|b
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++) T[i, j] = K.A[i, j];
                T[i, n] = K.b[i];
            }

            
            InitializeObjectiveRow(T, m, n, cUse, K.Basic);

            
            string[] colNames = SafeCols(K.ColNames.ToArray(), n);
            string[] rowNames = SafeRows(K.RowNames, m);

            int step = 0;
            log.PrintTableau(colNames, rowNames, T, step);

            while (true)
            {
                int enter = ChooseEntering(T, m, n);
                if (enter < 0)
                {
                    var r = ExtractResult(K, T, m, n, colNames, rowNames);
                    r.IsOptimal = true;
                    return r;
                }

                int leave = ChooseLeaving(T, m, n, enter);
                if (leave < 0)
                {
                    var r = ExtractResult(K, T, m, n, colNames, rowNames);
                    r.IsUnbounded = true;
                    return r;
                }

                string entName = colNames[enter];
                string levName = rowNames[leave];

                Pivot(T, m, n, leave, enter);
                rowNames[leave] = entName;
                K.Basic[leave] = enter;

                step++;
                log.PrintTableau(colNames, rowNames, T, step, "Entering " + entName + ", Leaving " + levName);
            }
        }

        private static void InitializeObjectiveRow(double[,] T, int m, int n, double[] c, int[] basic)
        {
            // set z row to -c
            for (int j = 0; j < n; j++) T[m, j] = -((j < c.Length) ? c[j] : 0.0);
            T[m, n] = 0.0;

           
            for (int i = 0; i < m; i++)
            {
                int bj = basic[i];
                double cb = (bj >= 0 && bj < c.Length) ? c[bj] : 0.0;
                if (Math.Abs(cb) < 1e-12) continue;
                for (int j = 0; j <= n; j++) T[m, j] += cb * T[i, j];
            }
        }

        private static int ChooseEntering(double[,] T, int m, int n)
        {
            int col = -1; double best = 0.0;
            for (int j = 0; j < n; j++)
            {
                double rc = T[m, j]; 
                if (rc < -1e-9)
                {
                    if (col < 0 || rc < best) { best = rc; col = j; }
                }
            }
            return col;
        }

        private static int ChooseLeaving(double[,] T, int m, int n, int enter)
        {
            int row = -1; double best = double.PositiveInfinity;
            for (int i = 0; i < m; i++)
            {
                double a = T[i, enter];
                if (a > 1e-9)
                {
                    double ratio = T[i, n] / a;
                    if (ratio < best - 1e-12) { best = ratio; row = i; }
                }
            }
            return row;
        }

        private static void Pivot(double[,] T, int m, int n, int pRow, int pCol)
        {
            double piv = T[pRow, pCol];
            
            for (int j = 0; j <= n; j++) T[pRow, j] /= piv;
           
            for (int i = 0; i <= m; i++)
            {
                if (i == pRow) continue;
                double factor = T[i, pCol];
                if (Math.Abs(factor) < 1e-12) continue;
                for (int j = 0; j <= n; j++) T[i, j] -= factor * T[pRow, j];
            }
        }

        private Result ExtractResult(Canonical K, double[,] T, int m, int n, string[] colNames, string[] rowNames)
        {
            var xCanon = new double[n];
            for (int i = 0; i < m; i++)
            {
                int col = Array.IndexOf(colNames, rowNames[i]);
                if (col >= 0) xCanon[col] = T[i, n];
            }

            var xOrig = new double[K.OrigVarCount];
            for (int j = 0; j < K.OrigVarCount; j++) xOrig[j] = (j < xCanon.Length) ? xCanon[j] : 0.0;

            // Extract shadow prices from slack (s) / surplus (t) reduced costs in Row m
            var shadowPrices = new double[m];
            for (int i = 0; i < m; i++)
            {
                string slackName = "s" + (i + 1);
                int slackIdx = Array.IndexOf(colNames, slackName);
                if (slackIdx >= 0)
                {
                    shadowPrices[i] = T[m, slackIdx];
                }
                else
                {
                    // Fallback for surplus variables (t)
                    string surplusName = "t" + (i + 1);
                    int surplusIdx = Array.IndexOf(colNames, surplusName);
                    if (surplusIdx >= 0)
                    {
                        shadowPrices[i] = Math.Abs(T[m, surplusIdx]);
                    }
                }
            }

            return new Result
            {
                ObjectiveValue = T[m, n],
                X = xOrig,
                ShadowPrices = shadowPrices,
                ColumnNames = colNames,
                RowNames = rowNames,
                LastTableau = T
            };
        }

        private Result MapBackToOriginal(LinearProgrammingModel model, Canonical K, Result r)
        {
            if (model.ObjectiveType == ObjectiveType.Minimize)
                r.ObjectiveValue *= -1.0;
            return r;
        }

     
        private static void AddNewColumn(List<double[]> cols, int currentRow, double valAtRow, out int idx)
        {
            idx = cols.Count;
            int r = currentRow + 1;
            var col = new double[r];
            for (int i = 0; i < r - 1; i++) col[i] = 0.0;
            col[r - 1] = valAtRow;
            cols.Add(col);
        }

        private static int FindUnitColumn(double[,] A, int row)
        {
            int m = A.GetLength(0);
            int n = A.GetLength(1);
            for (int j = 0; j < n; j++)
            {
                bool isUnit = true;
                for (int i = 0; i < m; i++)
                {
                    double v = Math.Abs(A[i, j]);
                    if (i == row) { if (Math.Abs(v - 1.0) > 1e-9) { isUnit = false; break; } }
                    else { if (v > 1e-9) { isUnit = false; break; } }
                }
                if (isUnit) return j;
            }
            return -1;
        }

        private static string[] SafeCols(string[] src, int need)
        {
            var dst = new string[need];
            for (int j = 0; j < need; j++)
                dst[j] = (src != null && j < src.Length && src[j] != null) ? src[j] : "x" + (j + 1);
            return dst;
        }

        private static string[] SafeRows(string[] src, int need)
        {
            var dst = new string[need];
            for (int i = 0; i < need; i++)
                dst[i] = (src != null && i < src.Length && src[i] != null) ? src[i] : "r" + (i + 1);
            return dst;
        }
    }
}
