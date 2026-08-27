using Person_1.Algorithmn;
using Person_1.Core;
using Person_1.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

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
        private static string outputFilePath = "output.txt";
        private static string tableauOutputFilePath = "tableau.txt";

        private static string initialModelReport;
        private static readonly StringBuilder lastAlgorithmLog = new StringBuilder();

        public static void Display()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      MAIN MENU                                ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");
            Console.WriteLine("1.LoadInputFile");
            Console.WriteLine("2.View Current Model");
            Console.WriteLine("3.Show Algorithmns Menu");
            Console.WriteLine("4.Show duality menu");
            Console.WriteLine("5.Show sensitivity Analysis menu");
            Console.WriteLine("6.Export Results");
            Console.WriteLine("7.Show About");
            Console.WriteLine("8.Exit");
        }
        static void Main(string[] args)
        {
            Console.Title = "Welcome ";
            bool start = true;
            int choice;

            while (start == true)
            {
                Display();
                choice = int.Parse(Console.ReadLine());

                Mainmenu menu = (Mainmenu)choice;

                switch (menu)
                {
                    case Mainmenu.LoadInputFile:
                        LoadInputFile();
                        break;
                    case Mainmenu.ViewCurrentModel:
                        //ViewCurrentModel()
                        break;
                    case Mainmenu.ShowAlgorithmnsMenu:
                        ShowAlgorithmnsMenu();
                        break;
                    case Mainmenu.ShowDualituMenu:
                        break;
                    case Mainmenu.ShowSensitivityAnalysisMenu:
                        break;
                    case Mainmenu.ExportResults:
                        break;
                    case Mainmenu.ShowAbout:
                        break;
                    case Mainmenu.Exit:
                        break;

                    default:
                        Console.WriteLine("You entered an invalid choice. Please try again");
                        break;
                }
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
            }


        }
        private static void LoadInputFile()
        {
            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      LOAD INPUT FILE                          ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");
            Console.Write("Enter input file path:");
            string filePath = Console.ReadLine();

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
                // Store the generated analysis report once.
                initialModelReport = generator.GenerateOutput(currentModel, filePath);


                lastAlgorithmLog.Clear();


                Console.WriteLine("File loaded successfully!");
                DisplayModelSummary(currentModel);

                if (currentModel.ParsingErrors.Any(e => !e.StartsWith("Info:")))
                {
                    Console.WriteLine($"\n{currentModel.ParsingErrors.Count(e => !e.StartsWith("Info:"))} parsing warnings detected:");
                    foreach (var error in currentModel.ParsingErrors.Where(e => !e.StartsWith("Info:")))
                    {
                        Console.WriteLine($"• {error}");
                    }
                    Console.WriteLine("Check the output file for detailed error information");
                }
                else
                {
                    Console.WriteLine("\nModel parsed successfully with no non-info warnings.");
                }
                Console.WriteLine($"Model valid for solving: {currentModel.IsValidForSolving()}");
            }
            catch (FormatException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n--- PARSING FAILED ---");
                Console.WriteLine("A number in your input file is incorrectly formatted.");
                Console.WriteLine($"\nDetailed Error: {ex.Message}");
                Console.ResetColor();


                initialModelReport = $"MODEL PARSING FAILED\n====================\nError: {ex.Message}";
                lastAlgorithmLog.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");


                initialModelReport = $"UNEXPECTED ERROR\n================\nError: {ex.Message}";
                lastAlgorithmLog.Clear();
            }
        }
        private static void DisplayModelSummary(LinearProgrammingModel model)
        {
            Console.WriteLine($"Model type: {model.ObjectiveType}");
            Console.WriteLine($"Variables: {model.Variables.Count}");
            Console.WriteLine($"Constraints: {model.Constraints.Count}");

            Console.WriteLine("\nModel Summary:");

            // Display objective function
            var objTerms = model.ObjectiveCoefficients
                .Select((c, i) => $"{(c >= 0 && i > 0 ? "+" : "")}{c:F3}x{i + 1}")
                .ToArray();
            Console.WriteLine($"Objective: {model.ObjectiveType} {string.Join(" ", objTerms)}");

            // Display constraints
            Console.WriteLine("Constraints:");
            for (int i = 0; i < model.Constraints.Count; i++)
            {
                var constraint = model.Constraints[i];
                var coeffs = constraint.Coefficients
                    .Select((c, j) => $"{(c >= 0 && j > 0 ? "+" : "")}{c:F3}x{j + 1}")
                    .ToArray();
                Console.WriteLine($"  {string.Join(" ", coeffs)} {constraint.Relation} {constraint.RightHandSide:F3}");
            }

            // Display sign restrictions
            var restrictions = model.SignRestrictions
                .Select((s, i) => $"x{i + 1}: {s}")
                .ToArray();
            Console.WriteLine($"Sign Restrictions: {string.Join(", ", restrictions)}");

            // Display model analysis
            Console.WriteLine($"\nModel Analysis:");
            Console.WriteLine($"• Integer Programming: {(model.IsIntegerProgramming() ? "Yes" : "No")}");
            Console.WriteLine($"• Binary Programming: {(model.IsBinaryProgramming() ? "Yes" : "No")}");
            Console.WriteLine($"• Mixed Variables: {(model.HasMixedVariables() ? "Yes" : "No")}");
        }
        private static object lastSolution;
        private static void ShowAlgorithmnsMenu()
        {
            if (currentModel == null)
            {
                Console.WriteLine("No model loaded. Please load a model first.");
                return;
            }

            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      SELECT ALGORITHM                         ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");
            Console.WriteLine("1. Primal Simplex Algorithm");
            Console.WriteLine("2. Revised Primal Simplex Algorithm");
            Console.WriteLine("3. Branch & Bound Simplex Algorithm");
            Console.WriteLine("4. Branch & Bound Knapsack Algorithm");
            Console.WriteLine("5. Back to main menu");
            Console.WriteLine("Choose an option:");

            string choice = Console.ReadLine();
            bool algorithmWasRun = false;

            if (choice == "1" || choice == "2" || choice == "3" || choice == "4")
            {
                lastAlgorithmLog.Clear();
                algorithmWasRun = true;
            }

            var log = new IterationLog(lastAlgorithmLog);

            if (choice == "1")
            {
                var primalSimplex = new PrimalSimplex();
                lastSolution = primalSimplex.Solve(currentModel, log);
            }
            else if (choice == "2")
            {
                var revisedPrimalSimplex = new RevisedSimplex();
                lastSolution = revisedPrimalSimplex.Solve(currentModel, log);
            }
            else if (choice == "3")
            {
                var bbSimplex = new BranchBoundSimplex();
                lastSolution = bbSimplex.Solve(currentModel, log);
            }
            else if (choice == "4")
            {
                var bbKnapsack = new BranchBoundKnapsack();
                lastSolution = bbKnapsack.Solve(currentModel, log);
            }
            else if (choice == "5")
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

                Console.WriteLine("\nAlgorithm finished. Its output is ready for export.");
                Console.WriteLine("Press any key to return to the algorithms menu...");
                Console.ReadKey();
            }
        }
        private static void ViewCurrentModel()
        {
            if (currentModel == null)
            {
                Console.WriteLine("No model loaded.");
                return;
            }

            Console.Clear();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                     CURRENT MODEL                             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

            DisplayModelSummary(currentModel);


            var actualErrors = currentModel.ParsingErrors
                .Where(e => !e.StartsWith("Info:"))
                .ToList();


            if (actualErrors.Any())
            {
                Console.WriteLine("\nParsing Errors and Warnings:");
                foreach (var error in actualErrors)
                {
                    Console.WriteLine($"• {error}");
                }
            }

            // Show model validation status
            Console.WriteLine($"\nModel Status:");
            Console.WriteLine($"• Valid Format: {(!currentModel.ParsingErrors.Any(e => e.StartsWith("Warning:") || e.StartsWith("Error:")) ? "Yes" : "No")}");
            Console.WriteLine($"• Ready for Solving: {(currentModel.IsValidForSolving() ? "Yes" : "No")}");
        }

       /* private static void ShowSensitivityMenu()
        {
            if (currentModel == null)
            {
                Console.WriteLine("No model loaded. Please load a model first.");
                Console.ReadKey();
                return;
            }

            
            if (lastSolution == null || !lastSolution.IsOptimal)
            {
                Console.WriteLine("No optimal solution is available for analysis.");
                Console.WriteLine("Please run the Primal Simplex algorithm (Option 1 in the Algorithms Menu) first.");
                Console.ReadKey();
                return;
            }

            try
            {
               
                var sensitivity = new SensitivityAnalysis(lastSolution, currentModel);

                // 2. Call the single method that runs the internal sensitivity menu.
                sensitivity.PerformAnalysis();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during sensitivity analysis: {ex.Message}");
                Console.ReadKey();
            }
        }*/
        private static void ExportResults()
        {
            if (string.IsNullOrEmpty(initialModelReport))
            {
                Console.WriteLine("No results to export. Please load a model first.");
                return;
            }

            // --- 1. EXPORT MAIN REPORT (Model Analysis Only) ---
            Console.Write($"Enter main output file path (default: {outputFilePath}): ");
            string mainFilePath = Console.ReadLine();
            if (string.IsNullOrEmpty(mainFilePath))
                mainFilePath = outputFilePath;

            try
            {
                System.IO.File.WriteAllText(mainFilePath, initialModelReport);
                Console.WriteLine($"Main model analysis exported to {mainFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting main report: {ex.Message}");
            }

            // --- 2. EXPORT FULL ALGORITHM LOG (if it exists) ---
            if (lastAlgorithmLog.Length > 0)
            {
                Console.Write($"Enter full algorithm log file path (default: {tableauOutputFilePath}): ");
                string logFilePath = Console.ReadLine();
                if (string.IsNullOrEmpty(logFilePath))
                    logFilePath = tableauOutputFilePath;

                try
                {
                    System.IO.File.WriteAllText(logFilePath, lastAlgorithmLog.ToString());
                    Console.WriteLine($"Full algorithm log exported to {logFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting algorithm log: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Note: No algorithm log was generated to export.");
            }


        }

    }
}




   








