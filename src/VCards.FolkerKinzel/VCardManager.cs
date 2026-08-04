using FolkerKinzel.VCards;
using FolkerKinzel.VCards.Enums;
using FolkerKinzel.VCards.Extensions;
using Regira.IO.Utilities;
using Regira.Office.VCards.Abstractions;
using Regira.Office.VCards.Exceptions;
using static Regira.Office.VCards.FolkerKinzel.Converting.Converter;
using VCard = Regira.Office.VCards.Models.VCard;

namespace Regira.Office.VCards.FolkerKinzel;

public class VCardManager : IVCardService
{
    public async Task<VCard> Read(string content, CancellationToken token = default)
    {
        var items = await ReadMany(content);
        return items.FirstOrDefault() ?? throw new InvalidCardException("Invalid card", content);
    }
    public Task<IEnumerable<VCard>> ReadMany(string content, CancellationToken token = default)
    {
        using var stream = content.GetStreamFromString();
        var items = Vcf.Deserialize(stream);
        if (!items.Any())
        {
            throw new InvalidCardException("Invalid card", content);
        }
        return Task.FromResult(items.Select(Convert));
    }

    public Task<string> Write(VCard item, VCardVersion version = VCardVersion.V3_0, CancellationToken token = default)
    {
        var vCard = Convert(item);
        var stream = new MemoryStream();
        vCard.SerializeVcf(stream, GetVersion(version), null, VcfOpts.Default, true);
        return Task.FromResult(stream.GetString()!);
    }
    public Task<string> Write(IEnumerable<VCard> items, VCardVersion version = VCardVersion.V3_0, CancellationToken token = default)
    {
        var stream = new MemoryStream();
        items.Select(Convert).ToList().SerializeVcf(stream, GetVersion(version), null, VcfOpts.Default, true);
        return Task.FromResult(stream.GetString()!);
    }

    VCdVersion GetVersion(VCardVersion version)
    {
        switch (version)
        {
            case VCardVersion.V2_1:
                return VCdVersion.V2_1;
            case VCardVersion.V3_0:
                return VCdVersion.V3_0;
            case VCardVersion.V4_0:
                return VCdVersion.V4_0;
            default:
                return VCdVersion.V3_0;
        }
    }
}