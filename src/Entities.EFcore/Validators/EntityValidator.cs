using Microsoft.EntityFrameworkCore;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.EFcore.Validators;

/// <summary>An <see cref="IEntityValidator{TScope}"/> built from a delegate — what the builder's <c>Validate(ctx =&gt; …)</c> registers.</summary>
public class EntityValidator<TScope>(Action<IEntityValidatorContext<TScope>> validate) : EntityValidatorBase<TScope>
    where TScope : class
{
    public override Task Validate(IEntityValidatorContext<TScope> context, CancellationToken token = default)
    {
        validate(context);
        return Task.CompletedTask;
    }
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
    /// <summary>A delegate without the token — what the builder's <c>Validate(async (ctx, db) =&gt; …)</c> registers.</summary>
    public EntityValidator(TContext dbContext, Func<IEntityValidatorContext<TScope>, TContext, Task> validate)
        : this(dbContext, (context, db, _) => validate(context, db))
    {
    }

    public override Task Validate(IEntityValidatorContext<TScope> context, CancellationToken token = default)
        => validate(context, dbContext, token);
}
