using Microsoft.Extensions.DependencyInjection;
using Regira.Licensing.Models;
using Regira.Licensing.Utilities;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Clients.Abstractions;
using Regira.Office.Clients.Services;
using Regira.Office.Csv.Abstractions;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Mail.Abstractions;
using Regira.Office.OCR.Abstractions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.Word.Abstractions;

namespace Regira.Office.Clients.DependencyInjection;

public static class OfficeClientServiceCollectionExtensions
{
    public static IServiceCollection AddOfficeClients(this IServiceCollection services, Action<OfficeClientOptions> configure)
    {
        var options = new OfficeClientOptions();
        configure(options);

        void ConfigureClient(IServiceProvider sp, HttpClient c)
        {
            c.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            var licenseKey = LicenseUtility.Resolve(sp.GetServices<License>(), LicenseDefaults.Products.Services)?.RawKey;
            if (!string.IsNullOrEmpty(licenseKey))
                c.DefaultRequestHeaders.Add("X-License-Key", licenseKey);
        }

        services.AddHttpClient<IBarcodeService, BarcodeClient>(ConfigureClient);
        services.AddHttpClient<IQRCodeService, QRCodeClient>(ConfigureClient);

        services.AddHttpClient<IHtmlToPdfService, PdfClient>(ConfigureClient);
        services.AddHttpClient<IImagesToPdfService, PdfClient>(ConfigureClient);
        services.AddHttpClient<IPdfMerger, PdfClient>(ConfigureClient);
        services.AddHttpClient<IPdfSplitter, PdfClient>(ConfigureClient);
        services.AddHttpClient<IPdfTextExtractor, PdfClient>(ConfigureClient);
        services.AddHttpClient<IPdfToImageService, PdfClient>(ConfigureClient);

        services.AddHttpClient<IExcelService, ExcelClient>(ConfigureClient);

        services.AddHttpClient<IWordCreator, WordClient>(ConfigureClient);
        services.AddHttpClient<IWordConverter, WordClient>(ConfigureClient);
        services.AddHttpClient<IWordMerger, WordClient>(ConfigureClient);
        services.AddHttpClient<IWordTextExtractor, WordClient>(ConfigureClient);

        services.AddHttpClient<IOcrService, OcrClient>(ConfigureClient);
        services.AddHttpClient<ICsvService, CsvClient>(ConfigureClient);
        services.AddHttpClient<IMessageParser, MessageParserClient>(ConfigureClient);

        // Asks the API what it makes of the key sent above; answers for an expired key too.
        services.AddHttpClient<ILicenseStatusClient, LicenseStatusClient>(ConfigureClient);

        return services;
    }
}
