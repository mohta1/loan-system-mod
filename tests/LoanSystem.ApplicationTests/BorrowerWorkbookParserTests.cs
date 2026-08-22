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

    [Fact]
    public void Maps_reordered_optional_headers_by_name_and_allows_missing_optional_headers()
    {
        using var reordered = Workbook(["Civil Number", "Full Name", "Nationality", "Organization", "Phone Number", "Employee Number"], ["001", "Name", "Omani", "MOD", "9000", "E-01"]);
        var row = Assert.Single(new OpenXmlBorrowerWorkbookParser().Parse(reordered, 10));
        Assert.Equal("E-01", row.Input.EmployeeNumber); Assert.Equal("9000", row.Input.PhoneNumber);
        using var requiredOnly = Workbook(BorrowerImportTemplate.Columns[..4], ["002", "Name", "Omani", "MOD"]);
        var requiredRow = Assert.Single(new OpenXmlBorrowerWorkbookParser().Parse(requiredOnly, 10)); Assert.Null(requiredRow.Input.EmployeeNumber); Assert.Null(requiredRow.Input.PhoneNumber);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    public void Rejects_unknown_duplicate_or_missing_required_headers(string kind)
    {
        string[] headers = kind switch
        {
            "unknown" => ["Civil Number", "Full Name", "Nationality", "Organization", "Unknown"],
            "duplicate" => ["Civil Number", "Full Name", "Nationality", "Organization", "Civil Number"],
            _ => ["Civil Number", "Full Name", "Nationality"]
        };
        using var workbook = Workbook(headers, ["001", "Name", "Omani", "MOD"]);
        Assert.Equal("borrowerImports.invalidTemplate", Assert.Throws<BorrowerImportException>(() => new OpenXmlBorrowerWorkbookParser().Parse(workbook, 10)).Code);
    }

    [Fact]
    public void Rejects_numeric_identifier_cells_without_converting_them()
    {
        using var workbook = Workbook(BorrowerImportTemplate.Columns, ["123", "Name", "Omani", "MOD", "0042"]);
        var document = SpreadsheetDocument.Open(workbook, true); var cells = document.WorkbookPart!.WorksheetParts.Single().Worksheet.GetFirstChild<SheetData>()!.Elements<Row>().Skip(1).Single().Elements<Cell>().ToArray();
        cells[0].DataType = CellValues.Number; cells[0].InlineString = null; cells[0].CellValue = new CellValue("123"); document.Dispose(); workbook.Position = 0;
        var row = Assert.Single(new OpenXmlBorrowerWorkbookParser().Parse(workbook, 10));
        Assert.Contains("borrowerImports.numericIdentifierNotSupported", row.Errors); Assert.Equal("123", row.Input.CivilNumber); Assert.Equal("0042", row.Input.EmployeeNumber);
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
