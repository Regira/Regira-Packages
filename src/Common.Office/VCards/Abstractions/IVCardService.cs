using Regira.Office.VCards.Models;

namespace Regira.Office.VCards.Abstractions;

public interface IVCardService
{
    Task<VCard> Read(string content, CancellationToken cancellationToken = default);
    Task<IEnumerable<VCard>> ReadMany(string content, CancellationToken cancellationToken = default);

    Task<string> Write(VCard item, VCardVersion version = VCardVersion.V3_0, CancellationToken cancellationToken = default);
    Task<string> Write(IEnumerable<VCard> items, VCardVersion version = VCardVersion.V3_0, CancellationToken cancellationToken = default);
}