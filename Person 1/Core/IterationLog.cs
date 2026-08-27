using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Person_1.Core
{
    public class IterationLog
    {
      
            private readonly StringBuilder buffer;

            public IterationLog(StringBuilder sharedBuffer)
            {
                buffer = sharedBuffer;
            }

            public void WriteLine(string text) => buffer.AppendLine(text);
            public void Write(string text) => buffer.Append(text);

            public void Title(string text)
            {
                buffer.AppendLine();
                buffer.AppendLine($"=== {text} ===");
            }

            public void Note(string text) => buffer.AppendLine(text);

            public void PrintTableau(string[] colNames, string[] rowNames, double[,] T, int step, string annotation = null)
            {
                buffer.AppendLine();
                buffer.AppendLine($"--- Tableau (Step {step}) ---" + (annotation != null ? $" [{annotation}]" : ""));

                var header = new StringBuilder("".PadRight(10));
                foreach (var c in colNames) header.Append(c.PadRight(10));
                buffer.AppendLine(header.ToString());

                int rows = T.GetLength(0);
                int cols = T.GetLength(1);
                for (int r = 0; r < rows; r++)
                {
                    var line = new StringBuilder((r < rowNames.Length ? rowNames[r] : "").PadRight(10));
                    for (int c = 0; c < cols; c++)
                        line.Append(Math.Round(T[r, c], 3).ToString("0.000").PadRight(10));
                    buffer.AppendLine(line.ToString());
                }
            }
        }
    }