using Regira.IO.Models;
using Regira.Office.OCR.Abstractions;

namespace Office.OCR.Testing;

public static class OCRTestsExtensions
{
    const string EXPECTED_EN = @"MOTHER'S DAY POEM
Your arms were always open
when I needed a hug.
Your heart understood
when I needed a friend.
Your gentle eyes were stern
when I needed a lesson.
Your strength and love has guided me
and gave me wings to fly.";
    const string EXPECTED_NL = @"je gaf mij het leven
ving mijn tranen
deelde mijn lach
daarom wil ik stilstaan
bij alles wat je bent en doet
ik zet jou in het zonnetje
op deze mooie dag
little universe";

    public static Task Test_Read_EN(this IOcrService service, string assetsDir)
        => Test_Read(service, Path.Combine(assetsDir, "poem-en.jpg"), EXPECTED_EN);
    public static Task Test_Read_NL(this IOcrService service, string assetsDir)
        => Test_Read(service, Path.Combine(assetsDir, "poem-nl.jpg"), EXPECTED_NL);

    static async Task Test_Read(this IOcrService service, string input, string expected)
    {
        var img = new BinaryFileItem
        {
            Bytes = await File.ReadAllBytesAsync(input)
        };
        var result = await service.Read(img);
        Assert.That(result.Text, Is.Not.Null);

        var contentLines = result.Text
            .ReplaceLineEndings()
            .ToLower()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        var expectedLines = expected
            .ToLower()
            .Split(Environment.NewLine);

        Assert.That(contentLines, Is.Not.Empty);
        Assert.That(string.Join(Environment.NewLine, contentLines!), Is.EqualTo(string.Join(Environment.NewLine, expectedLines)));
    }
}