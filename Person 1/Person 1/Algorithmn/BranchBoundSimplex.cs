using Person_1.Core;
using Person_1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;


namespace Person_1.Algorithmn
{
	public class BranchBoundSimplex
	{
		public PrimalSimplex.Result Solve(LinearProgrammingModel originalModel, IterationLog log)
		{
			log.Title("Branch & Bound Simplex Algorithm");

			Stack<LinearProgrammingModel> subProblems = new Stack<LinearProgrammingModel>();
			subProblems.Push(originalModel);

			var solver = new PrimalSimplex();

			bool isMax = originalModel.ObjectiveType == ObjectiveType.Maximize;
			double bestObjective = isMax ? double.NegativeInfinity : double.PositiveInfinity;
			double[] bestVariables = null;
			PrimalSimplex.Result bestResult = null;

			int nodeCount = 0;

			while (subProblems.Count > 0)
			{
				nodeCount++;
				var currentModel = subProblems.Pop();

				log.Title($"--- Evaluating Node {nodeCount} ---");
				var result = solver.Solve(currentModel, log);

				if (result.IsInfeasible || result.IsUnbounded)
				{
					log.Note($"Node {nodeCount} Fathomed: {(result.IsInfeasible ? "Infeasible" : "Unbounded")}");
					continue;
				}

				if (isMax && result.ObjectiveValue <= bestObjective)
				{
					log.Note($"Node {nodeCount} Fathomed: Bound (Objective {result.ObjectiveValue:F3} <= Best {bestObjective:F3})");
					continue;
				}
				if (!isMax && result.ObjectiveValue >= bestObjective)
				{
					log.Note($"Node {nodeCount} Fathomed: Bound (Objective {result.ObjectiveValue:F3} >= Best {bestObjective:F3})");
					continue;
				}

				int branchIndex = -1;
				double fractionalValue = 0;

				for (int i = 0; i < currentModel.Variables.Count; i++)
				{
					if (currentModel.Variables[i].IsIntegerType())
					{
						double val = result.X[i];
						if (Math.Abs(val - Math.Round(val)) > 1e-5)
						{
							branchIndex = i;
							fractionalValue = val;
							break;
						}
					}
				}

				if (branchIndex == -1)
				{
					bestObjective = result.ObjectiveValue;
					bestVariables = (double[])result.X.Clone();
					bestResult = result;
					log.Note($"*** Node {nodeCount} Fathomed: Integrality. New Best Candidate Found: {bestObjective:F3} ***");
					continue;
				}

				log.Note($"Branching on variable x{branchIndex + 1} with fractional value {fractionalValue:F3}");

				var lessThanModel = CloneAndAddConstraint(currentModel, branchIndex, Math.Floor(fractionalValue), "<=");
				var greaterThanModel = CloneAndAddConstraint(currentModel, branchIndex, Math.Ceiling(fractionalValue), ">=");

				subProblems.Push(greaterThanModel);
				subProblems.Push(lessThanModel);
			}

			log.Title("--- Branch & Bound Execution Complete ---");
			if (bestResult != null)
			{
				log.Note($"Optimal Integer Objective: {bestObjective:F3}");
				for (int i = 0; i < bestVariables.Length; i++)
				{
					log.Note($"x{i + 1} = {bestVariables[i]:F3}");
				}
				return bestResult;
			}

			log.Note("No feasible integer solution found.");
			return new PrimalSimplex.Result { IsInfeasible = true };
		}

		private LinearProgrammingModel CloneAndAddConstraint(LinearProgrammingModel source, int varIndex, double boundValue, string relation)
		{
			var clone = new LinearProgrammingModel
			{
				ObjectiveType = source.ObjectiveType,
				ObjectiveCoefficients = (double[])source.ObjectiveCoefficients.Clone(),
				SignRestrictions = new List<SignRestriction>(source.SignRestrictions),
				Variables = source.Variables.Select(v => new Variable(v.Name, v.SignRestriction, v.Index)).ToList(),
				Constraints = new List<Constraint>()
			};

			foreach (var c in source.Constraints)
			{
				clone.Constraints.Add(new Constraint((double[])c.Coefficients.Clone(), c.Relation, c.RightHandSide));
			}

			double[] newCoeffs = new double[clone.Variables.Count];
			newCoeffs[varIndex] = 1.0;
			clone.Constraints.Add(new Constraint(newCoeffs, relation, boundValue));

			return clone;
		}
	}
}