using Regira.Entities.Normalizing.Abstractions;
using Testing.Library.Contoso;

namespace Entities.Testing.Infrastructure.Normalizers;

public class Department1Normalizer : EntityNormalizerBase<Department>
{
    public override Task HandleNormalize(Department item, CancellationToken token = default)
    {
        item.NormalizedContent = $"DEPARTMENT_1 {item.NormalizedContent}".Trim();
        return Task.CompletedTask;
    }
}
public class Department2Normalizer : EntityNormalizerBase<Department>
{
    public override Task HandleNormalize(Department item, CancellationToken token = default)
    {
        item.NormalizedContent = $"DEPARTMENT_2 {item.NormalizedContent}".Trim();
        return Task.CompletedTask;
    }
}