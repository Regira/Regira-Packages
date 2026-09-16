using NUnit.Framework.Legacy;
using Office.Word.testing.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Models;
using Regira.Office.Word.Models;
using Regira.Office.Word.Spire;
using Regira.Utilities;
using System.Text.Json;

namespace Office.Word.testing;

/// <summary>
/// Shared scenarios come from <see cref="WordTestsBase"/>; everything below the overrides is
/// specific to Spire or exercises an end-to-end document it is the only backend fixture to cover.
/// </summary>
[TestFixture]
public class SpireTests() : WordTestsBase(new WordService(), "Spire")
{
    [TestCase("template.dot")]
    [TestCase("template.doc")]
    [TestCase("template.odt")]
    [TestCase("multipage.docx")]
    //[TestCase("converted.html")] // works only when html was generated from word...
    public override Task From_File(string filename) => base.From_File(filename);

    [Test]
    public override Task Bookmarks() => base.Bookmarks();

    [Test]
    public override Task Merge() => base.Merge();

    [Test]
    public override Task Add_Header_And_Footer() => base.Add_Header_And_Footer();

    [Test]
    public override Task Add_FirstPage_Header_And_Footer() => base.Add_FirstPage_Header_And_Footer();

    [Test]
    public override Task Replace_Parameters() => base.Replace_Parameters();

    [Test]
    public override Task Replace_Image() => base.Replace_Image();

    [Test]
    public override Task Template_Row() => base.Template_Row();

    [Test]
    public override Task Nested_Documents() => base.Nested_Documents();

    [Test]
    public override Task Nested_Documents_Do_Not_Wear_Out_The_Service() => base.Nested_Documents_Do_Not_Wear_Out_The_Service();

    [Test]
    public override void A_Template_That_Includes_Itself_Fails() => base.A_Template_That_Includes_Itself_Fails();

    [Test]
    public override Task A_Missing_Collection_Table_Leaves_The_Others() => base.A_Missing_Collection_Table_Leaves_The_Others();

    [Test]
    public override Task A_Null_Or_Unused_Parameter_Is_Harmless() => base.A_Null_Or_Unused_Parameter_Is_Harmless();

    [Test]
    public override Task A_Parameter_Key_Is_Matched_Literally() => base.A_Parameter_Key_Is_Matched_Literally();

    [TestCase(HeaderFooterType.Even, true)]
    [TestCase(HeaderFooterType.Even, false)]
    [TestCase(HeaderFooterType.FirstPage, true)]
    [TestCase(HeaderFooterType.FirstPage, false)]
    public override Task A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them(HeaderFooterType type, bool specialHeader)
        => base.A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them(type, specialHeader);

    [TestCase(FileFormat.Pdf, "converted.pdf")]
    [TestCase(FileFormat.Html, "converted.html")]
    [TestCase(FileFormat.Rtf, "converted.rtf")]
    [TestCase(FileFormat.Odt, "converted.odt")]
    [TestCase(FileFormat.EPub, "converted.epub")]
    public override Task Convert_To(FileFormat format, string outputName) => base.Convert_To(format, outputName);

    [Test]
    public override Task From_A3_To_Pdf() => base.From_A3_To_Pdf();

    [Test]
    public override Task From_A4_To_Pdf_A3() => base.From_A4_To_Pdf_A3();

    [Test]
    public override Task To_Images() => base.To_Images();

    [Test]
    public override Task GetImages() => base.GetImages();

    [Test]
    public override Task GetText() => base.GetText();


    [Test]
    public async Task Extern_Document_Inherit_Font()
    {
        var srcPath = InputPath("parent-template.docx");
        var outputPath = OutputPath("parent-template.docx");

        // doc1
        var doc1Src = InputPath("extern-small-font.docx");
        var doc1Template = File.OpenRead(doc1Src).ToBinaryFile();
        var doc1Input = new WordTemplateInput
        {
            Template = doc1Template,
            Options = new() { InheritFont = true, HorizontalAlignment = HorizontalAlignment.Justify }
        };

        // create
        var docParams = new Dictionary<string, WordTemplateInput>();
        docParams.Add("ExternDoc", doc1Input);
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile,
            DocumentParameters = docParams
        };
        using var outputFile = (await Service.Create(input));

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }

    [Test]
    public async Task From_Json()
    {
        var srcPath = InputPath("template_row.docx");
        var outputPath = OutputPath("from_json.docx");

        var json = @"{
    ""collectionParameters"": {
        ""Template_Table"": [
        { ""id"": ""10"", ""title"": ""Item #10"", ""price"": 100 },
        { ""id"": ""12"", ""title"":""Item #12"", ""price"": 200 },
        { ""id"": ""31"", ""title"": ""Item #31"", ""price"": 350 }
        ]
    }
}";
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = JsonSerializer.Deserialize<WordTemplateInput>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        input.Template = inputFile;
        using var outputFile = (await Service.Create(input))
            .ToBinaryFile();

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        var hasContent = await HasContent(outputFile, "Item #31");
        Assert.That(hasContent, Is.True);
        var hasTitleParameter = await HasContent(outputFile, "{{ title }}");
        ClassicAssert.IsFalse(hasTitleParameter);
    }

    class WordDocumentModel
    {
        public byte[] TemplateBytes { get; init; } = null!;
        public IDictionary<string, object?> GlobalParameters { get; init; } = null!;

    }
    [Test]
    public async Task From_JsonFile()
    {
        var srcPath = InputPath("json-input.json");
        var outputPath = OutputPath("from-json-file.docx");

        var json = await File.ReadAllTextAsync(srcPath);
        var model = JsonSerializer.Deserialize<WordDocumentModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var input = new WordTemplateInput
        {
            Template = model.TemplateBytes.ToBinaryFile(),
            GlobalParameters = model.GlobalParameters!
        };

        using var outputFile = (await Service.Create(input))
            .ToBinaryFile();

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }

    [Test]
    public async Task Factuur()
    {
        var srcPath = InputPath(Path.Combine("Factuur", "factuur.docx"));
        var outputPath = OutputPath(Path.Combine("Factuur", "factuur.docx"));
        // ReSharper disable once AssignNullToNotNullAttribute
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var invoice = new
        {
            invoiceTitle = "Nieuwe server: installatie en configuratie",
            invoiceNumber = "2019.014",
            ogmCode = "000/0019/01402",
            issueDate = "30-06-2019",
            dueDate = "31-07-2019",
            customer = new
            {
                title = "Ambulancecentrum Antwerpen BVBA",
                street = "Heiligstraat 139",
                address = "B-2620 Hemiksem",
            },
            priceExclTotal = "1.572,50",
            priceInclTotal = "1.902,73",
            taxTotal = "330,23"
        };
        var list = new List<object>();
        list.Add(new { quantity = "1", unitCategory = "st.", invoiceLineTitle = "Installatie besturingssystemen: inbegrepen in prijs van server", pricePerUnit = "0", taxTariff = "21,00", priceExcl = "0,00", tax = "0,00" });
        list.Add(new { quantity = "11", unitCategory = "u.", invoiceLineTitle = "Overdracht gegevens - opstellen en bijwerken testomgeving", pricePerUnit = "85,00", taxTariff = "21,00", priceExcl = "935,00", tax = "196,35" });
        list.Add(new { quantity = "4,5", unitCategory = "u.", invoiceLineTitle = "Effectieve overdracht gegevens en controle", pricePerUnit = "85,00", taxTariff = "21,00", priceExcl = "382,5", tax = "80,33" });
        list.Add(new { quantity = "3", unitCategory = "u.", invoiceLineTitle = "Opstellen en testen backupschema's voor de verschillende servers en virtuele computers", pricePerUnit = "85,00", taxTariff = "21,00", priceExcl = "255,00", tax = "53,55" });
        var invoiceLines = list.Select(x => DictionaryUtility.ToDictionary(x))
            .ToList();

        var json = JsonSerializer.Serialize(new
        {
            globalParameters = DictionaryUtility.ToDictionary(invoice),
            collectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>> {
                {"invoiceLines", invoiceLines!}
            }
        }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(OutputPath(Path.Combine("Factuur", "factuur-parameters.json")), json);

        // use a copy so I can keep the original open for editing
        var tmpFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(srcPath));
        File.Copy(srcPath, tmpFile, true);
        using var inputFile = File.OpenRead(tmpFile).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile,
            GlobalParameters = DictionaryUtility.Flatten(DictionaryUtility.ToDictionary(invoice))!,
            CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>()
        };
        input.CollectionParameters.Add("invoiceLines", invoiceLines!);
        using var outputFile = (await Service.Create(input))
            .ToBinaryFile();
        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        // pdf
        using var pdfFile = await Service.Convert(new WordTemplateInput { Template = outputFile }, FileFormat.Pdf);
        var pdfPath = OutputPath(Path.Combine("Factuur", "factuur.pdf"));
        await pdfFile.SaveAs(pdfPath);

        var hasCustomerTitleValue = await HasContent(outputFile, invoice.customer.title);
        Assert.That(hasCustomerTitleValue, Is.True);
        var hasCustomerTitleParameter = await HasContent(outputFile, "{{ customer.title }}");
        ClassicAssert.IsFalse(hasCustomerTitleParameter);

        var hasInvoiceLineTitleValue = await HasContent(outputFile, invoiceLines[1]["invoiceLineTitle"]!.ToString()!);
        Assert.That(hasInvoiceLineTitleValue, Is.True);
        var hasInvoiceLineTitleParameter = await HasContent(outputFile, "{{invoiceLineTitle}}");
        ClassicAssert.IsFalse(hasInvoiceLineTitleParameter);
    }
    [Test]
    public async Task Invoice_Advanced()
    {
        var srcPath = InputPath(Path.Combine("Factuur", "invoice.dotx"));
        var outputPath = OutputPath(Path.Combine("Factuur", "invoice.docx"));
        // ReSharper disable once AssignNullToNotNullAttribute
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var invoice = new
        {
            invoiceTitle = "Nieuwe server: installatie en configuratie",
            invoiceNumber = "2019.014",
            ogmCode = "000/0019/01402",
            issueDate = "30-06-2019",
            dueDate = "31-07-2019",
            customer = new
            {
                title = "Ambulancecentrum Antwerpen BVBA",
                street = "Heiligstraat 139",
                address = "B-2620 Hemiksem",
            },
            priceExclTotal = "1.572,50",
            priceInclTotal = "1.902,73",
            taxTotal = "330,23"
        };
        var list = new List<object>();
        list.Add(new { quantity = "1", unitCategory = "st.", invoiceLineTitle = "Installatie besturingssystemen: inbegrepen in prijs van server", pricePerUnit = "0", taxTariff = "21,00", priceExcl = "0,00", tax = "0,00" });
        list.Add(new { quantity = "11", unitCategory = "u.", invoiceLineTitle = "Overdracht gegevens - opstellen en bijwerken testomgeving", pricePerUnit = "85,00", taxTariff = "21,00", priceExcl = "935,00", tax = "196,35" });
        list.Add(new { quantity = "4,5", unitCategory = "u.", invoiceLineTitle = "Effectieve overdracht gegevens en controle", pricePerUnit = "85,00", taxTariff = "21,00", priceExcl = "382,5", tax = "80,33" });
        list.Add(new { quantity = "3", unitCategory = "u.", invoiceLineTitle = "Opstellen en testen backupschema's voor de verschillende servers en virtuele computers", pricePerUnit = "85,00", taxTariff = "21,00", priceExcl = "255,00", tax = "53,55" });
        var invoiceLines = list.Select(x => DictionaryUtility.ToDictionary(x))
            .ToList();

        // use a copy so I can keep the original open for editing
        var tmpFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(srcPath));
        File.Copy(srcPath, tmpFile, true);
        using var inputFile = File.OpenRead(tmpFile).ToBinaryFile();

        // customer
        var customerFile = InputPath(Path.Combine("Factuur", "customer.dotx"));
        using var customerTemplate = File.OpenRead(customerFile).ToBinaryFile();
        var customerInput = new WordTemplateInput
        {
            Template = customerTemplate
        };
        // invoice-details
        var invoiceDetailsFile = InputPath(Path.Combine("Factuur", "invoice-details.dotx"));
        using var invoiceDetailsTemplate = File.OpenRead(invoiceDetailsFile).ToBinaryFile();
        var invoiceDetailsInput = new WordTemplateInput
        {
            Template = invoiceDetailsTemplate
        };
        // invoice-lines
        var invoiceLinesFile = InputPath(Path.Combine("Factuur", "invoice-lines.dotx"));
        var tmpLinesFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(invoiceLinesFile));
        File.Copy(invoiceLinesFile, tmpLinesFile, true);
        using var invoiceLinesTemplate = File.OpenRead(tmpLinesFile).ToBinaryFile();
        var invoiceLinesInput = new WordTemplateInput
        {
            Template = invoiceLinesTemplate
        };
        // invoice-summary
        var invoiceSummaryFile = InputPath(Path.Combine("Factuur", "invoice-summary.dotx"));
        using var invoiceSummaryTemplate = File.OpenRead(invoiceSummaryFile).ToBinaryFile();
        var invoiceSummaryInput = new WordTemplateInput
        {
            Template = invoiceSummaryTemplate
        };
        // header
        var headerFile = InputPath(Path.Combine("Factuur", "header.dotx"));
        using var headerTemplate = File.OpenRead(headerFile).ToBinaryFile();
        var headerInput = new WordTemplateInput
        {
            Template = headerTemplate
        };
        // footer
        var footerFile = InputPath(Path.Combine("Factuur", "footer-inline.dotx"));
        using var footerTemplate = File.OpenRead(footerFile).ToBinaryFile();
        var footerInput = new WordTemplateInput
        {
            Template = footerTemplate
        };

        var input = new WordTemplateInput
        {
            Template = inputFile,
            GlobalParameters = DictionaryUtility.Flatten(DictionaryUtility.ToDictionary(invoice))!,
            CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>(),
            DocumentParameters = new Dictionary<string, WordTemplateInput>(),
            //Header = new Header { Template = headerStream },
            //Footer = new Footer { Template = footerStream }
        };
        input.DocumentParameters.Add("header.dotx", headerInput);
        input.DocumentParameters.Add("footer.dotx", footerInput);
        input.DocumentParameters.Add("customer.dotx", customerInput);
        input.DocumentParameters.Add("invoice-details.dotx", invoiceDetailsInput);
        input.DocumentParameters.Add("invoice-lines.dotx", invoiceLinesInput);
        input.DocumentParameters.Add("invoice-summary.dotx", invoiceSummaryInput);
        input.CollectionParameters.Add("invoiceLines", invoiceLines!);
        using var outputFile = (await Service.Create(input))
            .ToBinaryFile();
        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        // pdf
        using var pdfFile = await Service.Convert(new WordTemplateInput { Template = outputFile }, FileFormat.Pdf);
        var pdfPath = OutputPath(Path.Combine("Factuur", "invoice.pdf"));
        await pdfFile.SaveAs(pdfPath);
    }
}
