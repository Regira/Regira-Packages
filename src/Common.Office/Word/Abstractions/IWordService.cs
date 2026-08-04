namespace Regira.Office.Word.Abstractions;

public interface IWordService : IWordCreator, IWordConverter, IWordMerger, IWordTextExtractor, IWordImageExtractor, IWordToImagesService;

[Obsolete("Use IWordService instead.", false)]
public interface IWordManager : IWordService;