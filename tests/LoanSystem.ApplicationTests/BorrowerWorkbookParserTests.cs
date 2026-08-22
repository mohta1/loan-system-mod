using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using LoanSystem.Modules.Borrowers.Application;
using LoanSystem.Modules.Borrowers.Infrastructure;

namespace LoanSystem.ApplicationTests;

public sealed class BorrowerWorkbookParserTests
{
    [Fact]
    public void Parses_canonical_values_as_trimmed_identifiers_and_skips_blank_rows()
    {
        using var workbook = Workbook(
            BorrowerImportTemplate.Columns,
            [" 001234567890123456789 ", " Person ", " Omani ", " MOD ", " 00042 ", " 9000 ", " G7 ", " Active "],
            ["", "", "", ""]);
        var row = Assert.Single(new OpenXmlBorrowerWorkbookParser().Parse(workbook, 10));
        Assert.Equal("001234567890123456789", row.Input.CivilNumber);
        Assert.Equal("00042", row.Input.EmployeeNumber);
        Assert.Equal("Person", row.Input.FullName);
        Assert.Empty(row.Errors);
    }

    [Fact]
    public void Rejects_missing_header_empty_and_row_limit()
    {
        using var missing = Workbook(["Wrong", "Full Name", "Nationality", "Organization"], ["1", "Name", "Omani", "MOD"]);
        Assert.Equal("borrowerImports.invalidTemplate", Assert.Throws<BorrowerImportException>(() => new OpenXmlBorrowerWorkbookParser().Parse(missing, 10)).Code);
        using var empty = Workbook(BorrowerImportTemplate.Columns);
        Assert.Equal("borrowerImports.invalidTemplate", Assert.Throws<BorrowerImportException>(() => new OpenXmlBorrowerWorkbookParser().Parse(empty, 10)).Code);
        using var tooMany = Workbook(BorrowerImportTemplate.Columns, ["1", "A", "Omani", "MOD"], ["2", "B", "Omani", "MOD"]);
        Assert.Equal("borrowerImports.tooManyRows", Assert.Throws<BorrowerImportException>(() => new OpenXmlBorrowerWorkbookParser().Parse(tooMany, 1)).Code);
    }

    [Fact]
    public void Reports_invalid_domain_values_and_formula_cells()
    {
        using var workbook = Workbook(BorrowerImportTemplate.Columns, ["", "Name", "Omani", "MOD"]);
        var row = Assert.Single(new OpenXmlBorrowerWorkbookParser().Parse(workbook, 10));
        Assert.Contains("borrowers.validation", row.Errors);
        using var formula = Workbook(BorrowerImportTemplate.Columns, ["=1+1", "Name", "Omani", "MOD"]);
        var formulaRow = Assert.Single(new OpenXmlBorrowerWorkbookParser().Parse(formula, 10));
        Assert.Contains("borrowerImports.formulaNotSupported", formulaRow.Errors);
    }

    private static MemoryStream Workbook(string[] headers, params string[][] rows)
    {
        var stream = new MemoryStream(); using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart(); workbookPart.Workbook = new DocumentFormat.OpenXml.Spreadsheet.Workbook(); var worksheetPart = workbookPart.AddNewPart<WorksheetPart>(); var data = new SheetData(); worksheetPart.Worksheet = new Worksheet(data);
            var sheets = workbookPart.Workbook.AppendChild(new Sheets()); sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Borrowers" });
            data.Append(Row(headers, 1)); for (var i = 0; i < rows.Length; i++) data.Append(Row(rows[i], (uint)(i + 2))); workbookPart.Workbook.Save();
        }
        stream.Position = 0; return stream;
    }
    private static Row Row(string[] values, uint index)
    {
        var row = new Row { RowIndex = index }; for (var i = 0; i < values.Length; i++) { var cell = new Cell { CellReference = $"{(char)('A' + i)}{index}", DataType = CellValues.InlineString, InlineString = new InlineString(new Text(values[i])) }; if (values[i].StartsWith('=')) { cell.CellFormula = new CellFormula(values[i][1..]); cell.CellValue = new CellValue("2"); cell.DataType = CellValues.Number; cell.InlineString = null; } row.Append(cell); }
        return row;
    }
}
