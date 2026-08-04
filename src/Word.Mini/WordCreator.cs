using MiniSoftware;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Models;

namespace Regira.Office.Word.Mini;

public class WordCreator : IWordCreator
{
    public async Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream();
        var templateBytes = input.Template.GetBytes()!;
        var miniValue = GetMiniValue(input);
        await ms.SaveAsByTemplateAsync(templateBytes, miniValue);
        return ms.ToBinaryFile();
    }

    internal object GetMiniValue(WordTemplateInput input)
    {
        return
            // Global Parameters
            (input.GlobalParameters ?? new Dictionary<string, object>())
            // Pictures
            .Concat(input.Images
                ?.Select(x => new KeyValuePair<string, object>(
                    x.Name,
                    new MiniWordPicture
                    {
                        Bytes = x.File?.GetBytes(),
                        Width = x.Size?.Width ?? 0,
                        Height = x.Size?.Height ?? 0
                    }
                )) ?? []
            )
            // Tables
            .Concat(input.CollectionParameters
                ?.Select(x => new KeyValuePair<string, object>(x.Key, x.Value))
                ?? []
            )
            .ToDictionary(x => x.Key, x => x.Value);
    }
}