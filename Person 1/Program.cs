using Person_1.Algorithmn;
using Person_1.Core;
using Person_1.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Person_1
{
    class Program
    {
        enum Mainmenu
        {
            LoadInputFile = 1,
            ViewCurrentModel,
            ShowAlgorithmnsMenu,
            ShowDualituMenu,
            ShowSensitivityAnalysisMenu,
            ExportResults,
            ShowAbout,
            Exit
        }

        private static LinearProgrammingModel currentModel;
        private static dynamic lastSolution;
        private static PrimalSimplex.Result lastOptimalResult; // Store the optimal primal solution
        private static PrimalSimplex.Result lastDualResult;    // Store the optimal dual solution
        private static DualityAnalysis dualityAnalysis;
        private static string outputFilePath = "output.txt";
        private static string tableauOutputFilePath = "tableau.txt";
        private static string initialModelReport;
        private static readonly StringBuilder lastAlgorithmLog = new StringBuilder();

        public static void Display()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                       MAIN MENU                               ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");
            Console.WriteLine("1. Load Input File");
            Console.WriteLine("2. View Current Model");
            Console.WriteLine("3. Show Algorithms Menu");
            Console.WriteLine("4. Show Duality Menu");
            Console.WriteLine("5. Show Sensitivity Analysis Menu");
            Console.WriteLine("6. Export Results");
            Console.WriteLine("7. Show About");
            Console.WriteLine("8. Exit");
        }

        static void Main(string[] args)
        {
            Console.Title = "Welcome";
            bool start = true;

            while (start)
            {
                Display();
                Console.Write("\nChoose an option: ");
                string input = Console.ReadLine()?.Trim();

                if (!int.TryParse(input, out int choice))
                {
                    Console.WriteLine("Please enter a valid number.");
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                    continue;
                }

                Mainmenu menu = (Mainmenu)choice;

                switch (menu)
                {
                    case Mainmenu.LoadInputFile:
                        LoadInputFile();
                        break;
                    case Mainmenu.ViewCurrentModel:
                        ViewCurrentModel();
                        break;
                    case Mainmenu.ShowAlgorithmnsMenu:
                        ShowAlgorithmnsMenu();
                        break;
                    case Mainmenu.ShowDualituMenu:
                        ShowDualituMenu();
                        break;
                    case Mainmenu.ShowSensitivityAnalysisMenu:
                        ShowSensitivityAnalysisMenu();
                        break;
                    case Mainmenu.ExportResults:
                        Console.WriteLine("Export results feature is not yet connected.");
                        break;
                    case Mainmenu.ShowAbout:
                        Console.WriteLine("Linear & Integer Programming Solver — PRJ381 Group Project.");
                        break;
                    case Mainmenu.Exit:
                        start = false;
                        break;

                    default:
                        Console.WriteLine("You entered an invalid choice. Please try again.");
                        break;
                }

                if (start)
                {
                    Console.WriteLine("\nPress any key to continue...");
                    Console.ReadKey();
                }
            }
        }

        private static void LoadInputFile()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                       LOAD INPUT FILE                         ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");
            Console.Write("Enter input file path: ");
            string filePath = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(filePath))
            {
                Console.WriteLine("No file path provided!");
                return;
            }

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return;
            }

            try
            {
                var parser = new InputParser();
                currentModel = parser.Parse(filePath);

                var generator = new OutputWriter();
                initialModelReport = generator.GenerateOutput(currentModel, filePath);

                lastAlgorithmLog.Clear();
                lastSolution = null;
                lastOptimalResult = null;
                lastDualResult = null;
                dualityAnalysis = new DualityAnalysis(currentModel);

                Console.WriteLine("File loaded successfully!");
                DisplayModelSummary(currentModel);

                if (currentModel.ParsingErrors.Any(e => !e.StartsWith("Info:")))
                {
                    Console.WriteLine($"\n{currentModel.ParsingErrors.Count(e => !e.StartsWith("Info:"))} parsing warnings detected:");
                    foreach (var error in currentModel.ParsingErrors.Where(e => !e.StartsWith("Info:")))
                    {
                        Console.WriteLine($"• {error}");
                    }
                }
                else
                {
                    Console.WriteLine("\nModel parsed successfully with no warnings.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading file: {ex.Message}");
                initialModelReport = $"ERROR\n=====\nError: {ex.Message}";
                lastAlgorithmLog.Clear();
            }
        }

        private static void ViewCurrentModel()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                       CURRENT MODEL                           ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

            if (currentModel == null)
            {
                Console.WriteLine("No model loaded. Please load an input file first.");
                return;
            }

            DisplayModelSummary(currentModel);

            if (!string.IsNullOrEmpty(initialModelReport))
            {
                Console.WriteLine("\n--- Full Report ---");
                Console.WriteLine(initialModelReport);
            }
        }

        private static void DisplayModelSummary(LinearProgrammingModel model)
        {
            Console.WriteLine($"\nModel type: {model.ObjectiveType}");
            Console.WriteLine($"Variables: {model.Variables.Count}");
            Console.WriteLine($"Constraints: {model.Constraints.Count}");

            if (model.ObjectiveCoefficients != null && model.ObjectiveCoefficients.Length > 0)
            {
                var objTerms = model.ObjectiveCoefficients
                    .Select((c, i) => $"{(c >= 0 && i > 0 ? "+" : "")}{c:F3}x{i + 1}")
                    .ToArray();
                Console.WriteLine($"Objective: {model.ObjectiveType} {string.Join(" ", objTerms)}");
            }
        }

        private static void ShowAlgorithmnsMenu()
        {
            if (currentModel == null)
            {
                Console.WriteLine("No model loaded. Please load a model first.");
                return;
            }

            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                       SELECT ALGORITHM                        ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");
            Console.WriteLine("1. Primal Simplex Algorithm");
            Console.WriteLine("2. Revised Primal Simplex Algorithm");
            Console.WriteLine("3. Back to main menu");
            Console.Write("\nChoose an option: ");

            string choice = Console.ReadLine()?.Trim();
            bool algorithmWasRun = false;

            if (choice == "1" || choice == "2")
            {
                lastAlgorithmLog.Clear();
                algorithmWasRun = true;
            }

            var log = new IterationLog(lastAlgorithmLog);

            if (choice == "1")
            {
                var primalSimplex = new PrimalSimplex();
                lastSolution = primalSimplex.Solve(currentModel, log);
                if (lastSolution != null && lastSolution.IsOptimal)
                {
                    lastOptimalResult = lastSolution;
                }
            }
            else if (choice == "2")
            {
                var revisedPrimalSimplex = new RevisedSimplex();
                var res = revisedPrimalSimplex.Solve(currentModel, log);
                lastSolution = res;

                if (res != null)
                {
                    var underlyingProp = res.GetType().GetProperty("Underlying");
                    if (underlyingProp != null)
                    {
                        if (underlyingProp.GetValue(res) is PrimalSimplex.Result underlyingVal && underlyingVal.IsOptimal)
                        {
                            lastOptimalResult = underlyingVal;
                        }
                    }
                }
            }
            else if (choice == "3")
            {
                return;
            }
            else
            {
                Console.WriteLine("Invalid choice. Returning to main menu.");
                return;
            }

            if (algorithmWasRun)
            {
                lastAlgorithmLog.Insert(0, "*********************************\n    ALGORITHM EXECUTION LOG    \n*********************************\n");
                lastAlgorithmLog.Append("\n*******************************\n     ALGORITHM EXECUTION END     \n*******************************\n");

                Console.WriteLine(lastAlgorithmLog.ToString());
                Console.WriteLine("\nAlgorithm finished. Output is logged.");
                Console.WriteLine("Press any key to return...");
                Console.ReadKey();
            }
        }

        private static void ShowDualituMenu()
        {
            if (currentModel == null)
            {
                Console.WriteLine("No model loaded. Please load an input file first.");
                return;
            }

            if (dualityAnalysis == null)
            {
                dualityAnalysis = new DualityAnalysis(currentModel);
            }

            while (true)
            {
                Console.Clear();
                Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                        DUALITY MENU                           ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");
                Console.WriteLine("1. Apply Duality (Construct & View Dual Model)");
                Console.WriteLine("2. Solve Dual Programming Model");
                Console.WriteLine("3. Verify Strong/Weak Duality");
                Console.WriteLine("4. Return to Main Menu");
                Console.Write("\nChoose an option: ");

                string choice = Console.ReadLine()?.Trim();

                switch (choice)
                {
                    case "1":
                        dualityAnalysis.BuildDual();
                        Console.WriteLine("\n" + dualityAnalysis.DescribeDual());
                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();
                        break;

                    case "2":
                        if (dualityAnalysis.Dual == null)
                        {
                            dualityAnalysis.BuildDual();
                        }

                        Console.WriteLine("\nSolving Dual Model using Primal Simplex...");
                        var dualLogSb = new StringBuilder();
                        var dualLog = new IterationLog(dualLogSb);

                        var solver = new PrimalSimplex();
                        lastDualResult = solver.Solve(dualityAnalysis.Dual, dualLog);

                        if (lastDualResult != null && lastDualResult.IsOptimal)
                        {
                            Console.WriteLine($"\nDual solved successfully!");
                            Console.WriteLine($"Dual Optimal Objective Value: {lastDualResult.ObjectiveValue:0.000}");
                        }
                        else
                        {
                            Console.WriteLine("\nDual solution attempted. Solution is infeasible or unbounded.");
                        }

                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();
                        break;

                    case "3":
                        if (lastOptimalResult == null)
                        {
                            Console.WriteLine("\nPlease solve the Primal model (Option 3 on Main Menu) first.");
                        }
                        else if (lastDualResult == null)
                        {
                            Console.WriteLine("\nPlease solve the Dual model (Option 2 on Duality Menu) first.");
                        }
                        else
                        {
                            Console.WriteLine("\n" + dualityAnalysis.VerifyDuality(lastOptimalResult, lastDualResult));
                        }

                        Console.WriteLine("\nPress any key to continue...");
                        Console.ReadKey();
                        break;

                    case "4":
                        return;

                    default:
                        Console.WriteLine("\nInvalid choice. Press any key to try again...");
                        Console.ReadKey();
                        break;
                }
            }
        }

        private static void ShowSensitivityAnalysisMenu()
        {
            if (currentModel == null)
            {
                Console.WriteLine("No model loaded. Please load an input file first.");
                return;
            }

            if (lastOptimalResult == null || !lastOptimalResult.IsOptimal)
            {
                Console.WriteLine("Please solve the model to optimality using Option 3 before running sensitivity analysis.");
                return;
            }

            try
            {
                var sa = new SensitivityAnalysis(currentModel, lastOptimalResult);

                while (true)
                {
                    Console.Clear();
                    Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
                    Console.WriteLine("║                 SENSITIVITY ANALYSIS MENU                     ║");
                    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");
                    Console.WriteLine("1. Display Shadow Prices");
                    Console.WriteLine("2. Objective Coefficient Range & Change (Basic / Non-Basic)");
                    Console.WriteLine("3. Right-Hand Side (RHS) Range & Change");
                    Console.WriteLine("4. Constraint Coefficient Range & Change");
                    Console.WriteLine("5. Add New Activity (Variable)");
                    Console.WriteLine("6. Add New Constraint");
                    Console.WriteLine("7. Return to Main Menu");
                    Console.Write("\nChoose an option: ");

                    string input = Console.ReadLine()?.Trim();

                    switch (input)
                    {
                        case "1":
                            Console.WriteLine("\n" + sa.DisplayShadowPrices());
                            Console.WriteLine("\nPress any key to continue...");
                            Console.ReadKey();
                            break;

                        case "2":
                            Console.Write($"\nEnter variable index (0 to {currentModel.Variables.Count - 1} for x1..x{currentModel.Variables.Count}): ");
                            if (int.TryParse(Console.ReadLine(), out int varIdx) && varIdx >= 0 && varIdx < currentModel.Variables.Count)
                            {
                                string varName = "x" + (varIdx + 1);
                                bool isBasic = lastOptimalResult.RowNames != null && lastOptimalResult.RowNames.Contains(varName);

                                Console.Write("Enter new objective coefficient value: ");
                                if (double.TryParse(Console.ReadLine(), out double newC))
                                {
                                    Console.WriteLine();
                                    string report = isBasic
                                        ? sa.ApplyBasicVariableCostChange(varIdx, newC)
                                        : sa.ApplyNonBasicVariableCostChange(varIdx, newC);
                                    Console.WriteLine(report);
                                }
                            }
                            Console.WriteLine("\nPress any key to continue...");
                            Console.ReadKey();
                            break;

                        case "3":
                            Console.Write($"\nEnter constraint index (0 to {currentModel.Constraints.Count - 1}): ");
                            if (int.TryParse(Console.ReadLine(), out int cIdx) && cIdx >= 0 && cIdx < currentModel.Constraints.Count)
                            {
                                Console.Write("Enter new RHS value: ");
                                if (double.TryParse(Console.ReadLine(), out double newRHS))
                                {
                                    Console.WriteLine("\n" + sa.ApplyRHSChange(cIdx, newRHS));
                                }
                            }
                            Console.WriteLine("\nPress any key to continue...");
                            Console.ReadKey();
                            break;

                        case "4":
                            Console.Write($"Enter constraint index (0 to {currentModel.Constraints.Count - 1}): ");
                            int.TryParse(Console.ReadLine(), out int rowIdx);
                            Console.Write($"Enter variable index (0 to {currentModel.Variables.Count - 1}): ");
                            int.TryParse(Console.ReadLine(), out int colIdx);
                            Console.Write("Enter new constraint coefficient value: ");
                            if (double.TryParse(Console.ReadLine(), out double newCoeff))
                            {
                                Console.WriteLine("\n" + sa.ApplyNonBasicColumnCoefficientChange(rowIdx, colIdx, newCoeff));
                            }
                            Console.WriteLine("\nPress any key to continue...");
                            Console.ReadKey();
                            break;

                        case "5":
                            Console.Write("\nEnter activity name: ");
                            string name = Console.ReadLine();
                            Console.Write("Enter original objective coefficient: ");
                            double.TryParse(Console.ReadLine(), out double objCoeff);

                            double[] aCoeffs = new double[currentModel.Constraints.Count];
                            for (int i = 0; i < currentModel.Constraints.Count; i++)
                            {
                                Console.Write($"Enter coefficient for constraint {i + 1}: ");
                                double.TryParse(Console.ReadLine(), out aCoeffs[i]);
                            }

                            Console.WriteLine("\n" + sa.AddNewActivity(name, objCoeff, aCoeffs));
                            Console.WriteLine("\nPress any key to continue...");
                            Console.ReadKey();
                            break;

                        case "6":
                            double[] cCoeffs = new double[currentModel.Variables.Count];
                            for (int j = 0; j < currentModel.Variables.Count; j++)
                            {
                                Console.Write($"Enter coefficient for x{j + 1}: ");
                                double.TryParse(Console.ReadLine(), out cCoeffs[j]);
                            }
                            Console.Write("Enter relation (<=, >=, =): ");
                            string rel = Console.ReadLine();
                            Console.Write("Enter RHS value: ");
                            double.TryParse(Console.ReadLine(), out double rhsVal);

                            Console.WriteLine("\n" + sa.AddNewConstraint(cCoeffs, rel, rhsVal));
                            Console.WriteLine("\nPress any key to continue...");
                            Console.ReadKey();
                            break;

                        case "7":
                            return;

                        default:
                            Console.WriteLine("\nInvalid choice. Press any key to try again...");
                            Console.ReadKey();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during sensitivity analysis: {ex.Message}");
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }
        }
    }
}