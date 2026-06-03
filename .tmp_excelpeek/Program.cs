using ClosedXML.Excel;

var path = @"C:\TMT\Circle K — Quality Audit System\Opportunitiess & quality report.xlsx";
var wb = new XLWorkbook(path);
var ws = wb.Worksheet("Report");
var used = ws.RangeUsed();
var rows = used.RowCount();
var cols = used.ColumnCount();
Console.WriteLine($"Report rows {rows} cols {cols}");
for (int r = 1; r <= rows; r++)
{
    var row = ws.Row(r);
    var values = new List<string>();
    for (int c = 1; c <= cols; c++)
    {
        var val = row.Cell(c).GetString();
        if (!string.IsNullOrWhiteSpace(val))
        {
            values.Add($"{c}:{val}");
        }
    }
    if (values.Count > 0)
    {
        Console.WriteLine($"R{r}: " + string.Join(" | ", values));
    }
}

var ws2 = wb.Worksheet("Top Opportunities ");
var used2 = ws2.RangeUsed();
Console.WriteLine($"Top rows {used2.RowCount()} cols {used2.ColumnCount()}");
for (int r = 1; r <= used2.RowCount(); r++)
{
    var row = ws2.Row(r);
    var values = new List<string>();
    for (int c = 1; c <= used2.ColumnCount(); c++)
    {
        var val = row.Cell(c).GetString();
        if (!string.IsNullOrWhiteSpace(val))
        {
            values.Add($"{c}:{val}");
        }
    }
    if (values.Count > 0)
    {
        Console.WriteLine($"T{r}: " + string.Join(" | ", values));
    }
}
