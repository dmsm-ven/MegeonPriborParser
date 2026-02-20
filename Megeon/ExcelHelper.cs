using Microsoft.Office.Interop.Excel;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Megeon.ParserLib
{
    public class ExcelFileLinesReader
    {
        public List<string> ReadLines(string filePath, int startRow, int startColumn, int rowsCount, int columnsCount)
        {
            Application excel = new Application();
            Workbook wb = excel.Workbooks.Open(filePath);
            var oSheet = (_Worksheet)wb.ActiveSheet;

            try
            {
                var list = new List<string>();

                int currentRow = startRow;

                for (int row = startRow; row < (startRow + rowsCount + 1); row++)
                {
                    var sb = new StringBuilder();

                    for (int col = startColumn; col < (startColumn + columnsCount); col++)
                    {
                        string value = oSheet.Cells[row, col]?.Value?.ToString();

                        sb.Append(value + '\t');
                    }
                    list.Add(sb.ToString().Trim('\t'));
                }

                return list;
            }
            finally
            {
                excel.Visible = false;
                excel.UserControl = false;
                oSheet.Application.Quit();
            }
        }

        public void WriteLines(string filePath, List<string> lines, int startRow, int startColumn, int columnsCount)
        {
            Application excel = new Application();
            Workbook wb = excel.Workbooks.Open(filePath);
            var oSheet = (_Worksheet)wb.ActiveSheet;

            try
            {
                var list = new List<string>();

                int currentRow = startRow;
                foreach (var row in lines)
                {
                    var data = row.Split('\t');

                    for (int i = 0; i < columnsCount; i++)
                    {
                        if (i != (columnsCount - 1))
                        {
                            oSheet.Cells[currentRow, i + startColumn].Value2 = data[i];
                        }
                        else
                        {
                            string allText = string.Join("\t", data.Skip(i));
                            oSheet.Cells[currentRow, i + startColumn].Value2 = allText;
                        }
                    }

                    currentRow++;
                }

            }
            finally
            {
                excel.Visible = false;
                excel.UserControl = false;
                wb.SaveAs(Filename: filePath);
                oSheet.Application.Quit();
            }
        }
    }
}
