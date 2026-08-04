using Regira.Media.Drawing.Models.Abstractions;

namespace Regira.Media.Drawing.Services.Abstractions;

public interface IImageCreator
{
    public bool CanCreate(object input);
    public Task<IImageFile?> Create(object input, CancellationToken cancellationToken = default);
}
public interface IImageCreator<in T> : IImageCreator
    where T : notnull
{
    public bool CanCreate(T input);
    public Task<IImageFile?> Create(T input, CancellationToken cancellationToken = default);
}
