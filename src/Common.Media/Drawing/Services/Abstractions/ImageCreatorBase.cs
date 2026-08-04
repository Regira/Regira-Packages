using Regira.Media.Drawing.Models.Abstractions;

namespace Regira.Media.Drawing.Services.Abstractions;

public abstract class ImageCreatorBase<T> : IImageCreator<T>
    where T : class
{
    bool IImageCreator.CanCreate(object input)
        => input is T item && CanCreate(item);
    public virtual bool CanCreate(T input) => true;

    Task<IImageFile?> IImageCreator.Create(object input, CancellationToken cancellationToken)
        => TryCreate((T)input, cancellationToken);
    protected virtual Task<IImageFile?> TryCreate(T input, CancellationToken cancellationToken)
        => CanCreate(input) ? Create(input, cancellationToken) : Task.FromResult<IImageFile?>(null);

    public abstract Task<IImageFile?> Create(T input, CancellationToken cancellationToken = default);
}
