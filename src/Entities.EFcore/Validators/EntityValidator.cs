using Microsoft.EntityFrameworkCore;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.EFcore.Validators;

/// <summary>
/// An <see cref="IEntityValidator{TScope}"/> built from an asynchronous delegate — what the builder's
/// <c>Validate(async ctx =&gt; …)</c> registers. The write awaits it, so an error added after an <c>await</c> still
/// refuses the write.
/// </summary>
public class EntityValidator<TScope>(Func<IEntityValidatorContext<TScope>, Task> validate) : EntityValidatorBase<TScope>
    where TScope : class
{
    /// <summary>A synchronous delegate — what the builder's <c>Validate(ctx =&gt; …)</c> registers.</summary>
    public EntityValidator(Action<IEntityValidatorContext<TScope>> validate)
        : this(context =>
        {
            validate(context);
            return Task.CompletedTask;
        })
    {
    }

    public override Task Validate(IEntityValidatorContext<TScope> context, CancellationToken token = default)
        => validate(context);
}

/// <summary>
/// An <see cref="IEntityValidator{TScope}"/> built from a delegate that receives the request's <typeparamref name="TContext"/>
/// — what the builder's <c>Validate(async (ctx, db, token) =&gt; …)</c> registers. The token is the write's own, so a
/// lookup is cancelled with the request.
/// </summary>
public class EntityValidator<TContext, TScope>(TContext dbContext, Func<IEntityValidatorContext<TScope>, TContext, CancellationToken, Task> validate)
    : EntityValidatorBase<TScope>
    where TContext : DbContext
    where TScope : class
{
    public override Task Validate(IEntityValidatorContext<TScope> context, CancellationToken token = default)
        => validate(context, dbContext, token);
}
