using NUnit.Framework.Legacy;
using Regira.Collections;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Storage.FileSystem;
using Regira.Office.Models;
using Regira.Office.Word.Models;
using Regira.Office.Word.Spire;
using Regira.Utilities;
using System.Globalization;
using System.Text.Json;

[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Office.Word.testing;

[TestFixture]
public class SpireTests
{
    private readonly string _assetsDir;
    public SpireTests()
    {
        var assemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
        _assetsDir = Path.Combine(assemblyDir, "../../../", "Assets");
        Directory.CreateDirectory(Path.Combine(_assetsDir, "Output"));
    }

    [TestCase("template.dot")]
    [TestCase("template.doc")]
    [TestCase("template.odt")]
    [TestCase("multipage.docx")]
    //[TestCase("converted.html")] // works only when html was generated from word...
    public async Task From_File(string filename)
    {
        var srcPath = Path.Combine(_assetsDir, "Input", filename);
        var outputPath = Path.Combine(_assetsDir, "Output", $"from_{Path.GetExtension(filename).TrimStart('.')}.docx");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile.ToBinaryFile()
        };
        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input));

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }

    [Test]
    public async Task Bookmarks()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "bookmarks.dot");
        var outputPath = Path.Combine(_assetsDir, "Output", "bookmarks.docx");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var pi = Math.Round(Math.PI, 10).ToString(CultureInfo.InvariantCulture);
        var input = new WordTemplateInput
        {
            Template = inputFile.ToBinaryFile(),
            GlobalParameters = new Dictionary<string, object>()
        };
        input.GlobalParameters.Add("rs_Pi", pi);
        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input));

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        var hasPi = await HasContent(outputFile.ToBinaryFile(), pi);
        Assert.That(hasPi, Is.True);
    }
    [Test]
    public async Task Merge()
    {
        var srcPath1 = Path.Combine(_assetsDir, "Input", "doc-1.docx");
        var srcPath2 = Path.Combine(_assetsDir, "Input", "doc-2.docx");
        var srcHeaderFooter = Path.Combine(_assetsDir, "Input", "header_footer.docx");

        var outputPath = Path.Combine(_assetsDir, "Output", "merged.docx");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var mergedInputs = new[] { srcPath1, srcPath2 }
            .Select(srcPath => new WordTemplateInput
            {
                Template = File.OpenRead(srcPath).ToBinaryFile(),
                Options = new() { InheritFont = true, EnforceEvenAmountOfPages = true }
            })
            .ToArray();
        var mgr = new WordManager();
        using var mergedFile = await mgr.Merge(mergedInputs);
        var headerFooterInput = new WordTemplateInput
        {
            Template = mergedFile.ToBinaryFile()
        };
        headerFooterInput.Footers!.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = File.OpenRead(srcHeaderFooter).ToBinaryFile() } });
        headerFooterInput.Headers!.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = File.OpenRead(srcHeaderFooter).ToBinaryFile() } });
        using var outputFile = (await mgr.Create(headerFooterInput))
            .ToBinaryFile();

        var file = await outputFile.SaveAs(outputPath);

        // cleaning up
        mergedInputs.Select(x => x.Template).Dispose();
        headerFooterInput.Headers?.Concat(headerFooterInput.Footers).Select(x => x.Template.Template).Dispose();

        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        var hasDoc1Content = await HasContent(outputFile, "Vertical images");
        Assert.That(hasDoc1Content, Is.True);
        var hasDoc2Content = await HasContent(outputFile, "Some text in Arial");
        Assert.That(hasDoc2Content, Is.True);
    }
    [Test]
    public async Task Add_Header_And_Footer()
    {
        var headerFile = Path.Combine(_assetsDir, "Input", "add_header.docx");
        var footerFile = Path.Combine(_assetsDir, "Input", "add_footer.docx");
        var srcPath = Path.Combine(_assetsDir, "Input", "lorem_ipsum.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "added_header_and_footer.docx");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        using var headerTemplate = File.OpenRead(headerFile).ToBinaryFile();
        using var footerTemplate = File.OpenRead(footerFile).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile
        };
        input.Footers!.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = footerTemplate } });
        input.Headers!.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = headerTemplate } });

        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input))
            .ToBinaryFile();

        input.Headers.Concat(input.Footers).Select(x => x.Template.Template).Dispose();

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        var hasHeader = await HasContent(outputFile, "Header");
        Assert.That(hasHeader, Is.True);
        var hasFooter = await HasContent(outputFile, "Testing by");
        Assert.That(hasFooter, Is.True);
        var hasJibberisch = await HasContent(outputFile, "Jibberisch");
        ClassicAssert.IsFalse(hasJibberisch);
    }
    [Test]
    public async Task Add_FirstPage_Header_And_Footer()
    {
        var headerFile = Path.Combine(_assetsDir, "Input", "add_header.docx");
        var footerFile = Path.Combine(_assetsDir, "Input", "add_footer.docx");
        var srcPath = Path.Combine(_assetsDir, "Input", "lorem_ipsum.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "added_firstpage_header_and_footer.docx");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        using var headerTemplate = File.OpenRead(headerFile).ToBinaryFile();
        using var footerTemplate = File.OpenRead(footerFile).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile,
        };
        input.Footers!.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = footerTemplate }, Type = HeaderFooterType.FirstPage });
        input.Headers!.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = headerTemplate }, Type = HeaderFooterType.FirstPage });
        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input))
            .ToBinaryFile();
        input.Headers.Concat(input.Footers).Select(x => x.Template.Template).Dispose();

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        var hasHeader = await HasContent(outputFile, "Header");
        Assert.That(hasHeader, Is.True);
        var hasFooter = await HasContent(outputFile, "Testing by");
        Assert.That(hasFooter, Is.True);
        var hasJibberisch = await HasContent(outputFile, "Jibberisch");
        ClassicAssert.IsFalse(hasJibberisch);
    }
    [Test]
    public async Task Replace_Parameters()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "parameters.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "parameters.docx");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var parameters = new Dictionary<string, object>();
        parameters.Add("title", "A title for this word document");
        parameters.Add("date", DateTime.Today.ToShortDateString());
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile,
            GlobalParameters = parameters
        };
        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input))
            .ToBinaryFile();

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        foreach (var parameterValue in parameters.Values)
        {
            var hasParameter = await HasContent(outputFile, parameterValue.ToString()!);
            Assert.That(hasParameter, Is.True);
        }
    }
    // skipping Insert_Image() ...
    [Test]
    public async Task Replace_Image()
    {
        var imgFile = Path.Combine(_assetsDir, "Input", "sample1.jpg");
        var srcPath = Path.Combine(_assetsDir, "Input", "template_image.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "template_image.docx");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile,
            Images = new List<WordImage>()
        };
        input.Images.Add(new()
        {
            Name = "placeholder",// Alt Text
            File = await FileSystemUtility.Parse(imgFile)
        });
        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input))
            .ToBinaryFile();

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }
    // skipping Insert_Table() ...
    [Test]
    public async Task Template_Row()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "template_row.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "template_row.docx");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        var list = new List<object>();
        list.Add(new { Id = "10", Title = "Item #10", Price = 100 });
        list.Add(new { Id = "12", Title = "Item #12", Price = 200 });
        list.Add(new { Id = "31", Title = "Item #31", Price = 350 });
        var data = list.Select(x => DictionaryUtility.ToDictionary(x))
            .ToList();
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile,
            CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>()
        };
        input.CollectionParameters.Add("Template_Table", data!);
        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input))
            .ToBinaryFile();

        var file = await outputFile.SaveAs(outputPath);

        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        var hasContent = await HasContent(outputFile, "Item #12");
        Assert.That(hasContent, Is.True);
        var hasTitleParameter = await HasContent(outputFile, "{{ title }}");
        ClassicAssert.IsFalse(hasTitleParameter);
    }
    [Test]
    public async Task To_Pdf()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "template.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "converted.pdf");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile
        };
        var mgr = new WordManager();
        using var outputFile = await mgr.Convert(input, FileFormat.Pdf);

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }
    [Test]
    public async Task From_A3_To_Pdf()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "template_a3.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "converted_a3.pdf");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile
        };
        var mgr = new WordManager();
        using var outputFile = await mgr.Convert(input, FileFormat.Pdf);

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }
    [Test]
    public async Task From_A4_To_Pdf_A3()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "template.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "from_a4_to_a3.pdf");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile
        };
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new() { PageSize = PageSize.A3 }
        };
        var mgr = new WordManager();
        using var outputFile = await mgr.Convert(input, options);

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }
    [Test]
    public async Task To_HTML()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "template.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "converted.html");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile
        };
        var mgr = new WordManager();
        using var outputFile = await mgr.Convert(input, FileFormat.Html);

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }
    [Test]
    public async Task To_Rtf()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "template.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "converted.rtf");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile
        };
        var mgr = new WordManager();
        using var outputFile = await mgr.Convert(input, FileFormat.Rtf);

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }
    [Test]
    public async Task To_EPub()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "template.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "converted.epub");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile
        };
        var mgr = new WordManager();
        using var outputFile = await mgr.Convert(input, FileFormat.EPub);

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }
    [Test]
    public async Task To_Odt()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "template.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "converted.odt");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile
        };
        var mgr = new WordManager();
        using var outputFile = await mgr.Convert(input, FileFormat.Odt);

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }
    [Test]
    public async Task To_Images()
    {
        //Assert.Ignore("Not supported for now...");
        var srcPath = Path.Combine(_assetsDir, "Input", "template.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "image-{0}.jpg");
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile
        };
        var mgr = new WordManager();
        var images = (await mgr.ToImages(input)).ToArray();

        for (var i = 0; i < images.Length; i++)
        {
            var file = await images[i].SaveAs(string.Format(outputPath, i + 1));
            Assert.That(file.Exists, Is.True);
            ClassicAssert.Greater(file.Length, 0);
        }
        images.Dispose();
    }
    [Test]
    public async Task Nested_Documents()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "nested_templates.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "nested_templates.docx");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        // doc1
        var doc1Src = Path.Combine(_assetsDir, "Input", "template_row.docx");
        var list = new List<object>();
        list.Add(new { Id = "10", Title = "Item #10", Price = 100 });
        list.Add(new { Id = "12", Title = "Item #12", Price = 200 });
        list.Add(new { Id = "31", Title = "Item #31", Price = 350 });
        var data = list
            .Select(x => DictionaryUtility.ToDictionary(x))
            .ToList();
        using var doc1Template = File.OpenRead(doc1Src).ToBinaryFile();
        var doc1Input = new WordTemplateInput
        {
            Template = doc1Template,
            CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>()
        };
        doc1Input.CollectionParameters.Add("Template_Table", data!);
        // doc2
        var doc2Src = Path.Combine(_assetsDir, "Input", "template_image.docx");
        using var doc2Template = File.OpenRead(doc2Src).ToBinaryFile();
        var imgFile = Path.Combine(_assetsDir, "Input", "sample1.jpg");
        var doc2Input = new WordTemplateInput
        {
            Template = doc2Template,
            Images = new List<WordImage>(),
            Options = new() { InheritFont = true }
        };
        doc2Input.Images.Add(new()
        {
            Name = "placeholder", // Alt Text
            File = await FileSystemUtility.Parse(imgFile)
        });

        // create
        var docParams = new Dictionary<string, WordTemplateInput>();
        docParams.Add("doc1", doc1Input);
        docParams.Add("doc2", doc2Input);
        using var inputFile = File.OpenRead(srcPath).ToBinaryFile();
        var input = new WordTemplateInput
        {
            Template = inputFile,
            DocumentParameters = docParams
        };
        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input))
            .ToBinaryFile();

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        var hasContent = await HasContent(outputFile, "Item #12");
        Assert.That(hasContent, Is.True);
        var hasDoc2Parameter = await HasContent(outputFile, "<{ doc2  }>");
        ClassicAssert.IsFalse(hasDoc2Parameter);
    }
    [Test]
    public async Task Extern_Document_Inherit_Font()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "parent-template.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "parent-template.docx");
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        // doc1
        var doc1Src = Path.Combine(_assetsDir, "Input", "extern-small-font.docx");
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
        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input));

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }

    [Test]
    public async Task From_Json()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "template_row.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "from_json.docx");

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
        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input))
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
        var srcPath = Path.Combine(_assetsDir, "Input", "json-input.json");
        var outputPath = Path.Combine(_assetsDir, "Output", "from-json-file.docx");

        var json = await File.ReadAllTextAsync(srcPath);
        var model = JsonSerializer.Deserialize<WordDocumentModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var input = new WordTemplateInput
        {
            Template = model.TemplateBytes.ToBinaryFile(),
            GlobalParameters = model.GlobalParameters!
        };

        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input))
            .ToBinaryFile();

        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }

    [Test]
    public async Task Factuur()
    {
        var srcPath = Path.Combine(_assetsDir, "Input", "Factuur", "factuur.docx");
        var outputPath = Path.Combine(_assetsDir, "Output", "Factuur", "factuur.docx");
        // ReSharper disable once AssignNullToNotNullAttribute
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

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
        await File.WriteAllTextAsync(Path.Combine(_assetsDir, "Output", "Factuur", "factuur-parameters.json"), json);

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
        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input))
            .ToBinaryFile();
        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        // pdf
        using var pdfFile = await mgr.Convert(new WordTemplateInput { Template = outputFile }, FileFormat.Pdf);
        var pdfPath = Path.Combine(_assetsDir, "Output", "Factuur", "factuur.pdf");
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
        var srcPath = Path.Combine(_assetsDir, "Input", "Factuur", "invoice.dotx");
        var outputPath = Path.Combine(_assetsDir, "Output", "Factuur", "invoice.docx");
        // ReSharper disable once AssignNullToNotNullAttribute
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

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
        var customerFile = Path.Combine(_assetsDir, "Input", "Factuur", "customer.dotx");
        using var customerTemplate = File.OpenRead(customerFile).ToBinaryFile();
        var customerInput = new WordTemplateInput
        {
            Template = customerTemplate
        };
        // invoice-details
        var invoiceDetailsFile = Path.Combine(_assetsDir, "Input", "Factuur", "invoice-details.dotx");
        using var invoiceDetailsTemplate = File.OpenRead(invoiceDetailsFile).ToBinaryFile();
        var invoiceDetailsInput = new WordTemplateInput
        {
            Template = invoiceDetailsTemplate
        };
        // invoice-lines
        var invoiceLinesFile = Path.Combine(_assetsDir, "Input", "Factuur", "invoice-lines.dotx");
        var tmpLinesFile = Path.Combine(Path.GetTempPath(), Path.GetFileName(invoiceLinesFile));
        File.Copy(invoiceLinesFile, tmpLinesFile, true);
        using var invoiceLinesTemplate = File.OpenRead(tmpLinesFile).ToBinaryFile();
        var invoiceLinesInput = new WordTemplateInput
        {
            Template = invoiceLinesTemplate
        };
        // invoice-summary
        var invoiceSummaryFile = Path.Combine(_assetsDir, "Input", "Factuur", "invoice-summary.dotx");
        using var invoiceSummaryTemplate = File.OpenRead(invoiceSummaryFile).ToBinaryFile();
        var invoiceSummaryInput = new WordTemplateInput
        {
            Template = invoiceSummaryTemplate
        };
        // header
        var headerFile = Path.Combine(_assetsDir, "Input", "Factuur", "header.dotx");
        using var headerTemplate = File.OpenRead(headerFile).ToBinaryFile();
        var headerInput = new WordTemplateInput
        {
            Template = headerTemplate
        };
        // footer
        var footerFile = Path.Combine(_assetsDir, "Input", "Factuur", "footer-inline.dotx");
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
        var mgr = new WordManager();
        using var outputFile = (await mgr.Create(input))
            .ToBinaryFile();
        var file = await outputFile.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);

        // pdf
        using var pdfFile = await mgr.Convert(new WordTemplateInput { Template = outputFile }, FileFormat.Pdf);
        var pdfPath = Path.Combine(_assetsDir, "Output", "Factuur", "invoice.pdf");
        await pdfFile.SaveAs(pdfPath);
    }


    private async Task<bool> HasContent(IBinaryFile file, string s)
    {
        var mgr = new WordManager();
        var content = await mgr.GetText(new WordTemplateInput
        {
            Template = file
        });
        return content.Contains(s);
    }
}