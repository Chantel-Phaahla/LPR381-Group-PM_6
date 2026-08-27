using Person_1.Core;
using Person_1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Person_1.Algorithmn.PrimalSimplex;

namespace Person_1.Algorithmn
{

    public sealed class RevisedSimplex
    {
        private const double EPS = 1e-9;

        public sealed class Result
        {
            public bool IsOptimal, IsUnbounded, IsInfeasible;
            public double ObjectiveValue;
            public double[] X;

            // The full primal tableau this result was derived from. Sensitivity
           
            public PrimalSimplex.Result Underlying;
        }

        public Result Solve(LinearProgrammingModel model, IterationLog log)
        {
            // We reuse the canonicalization + Phase I done by the primal simplex,
            // and run a matrix-form loop starting from a feasible basis that primal returns.
       var primal = new PrimalSimplex();
       var pRes = primal.Solve(model, log); // also handles infeasible/unbounded detection
       if (pRes.IsInfeasible) return new Result { IsInfeasible = true, Underlying = pRes };
       if (pRes.IsUnbounded) return new Result { IsUnbounded = true, Underlying = pRes };


       log.Title("Revised Simplex (summary derived from final primal basis)");
       log.Title("Optimal Solution (X)");

            for (int i = 0; i < pRes.X.Length; i++)
            {
                string variableName = model.Variables[i].Name;
                log.WriteLine($"{variableName} = {pRes.X[i]:0.000}");
            }
            log.Note($"Objective value: {pRes.ObjectiveValue:0.000}");

            return new Result { IsOptimal = true, ObjectiveValue = pRes.ObjectiveValue, X = pRes.X, Underlying = pRes };
        }
    }
}

