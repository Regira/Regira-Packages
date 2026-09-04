using Regira.Entities.Attributes;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Preppers.Abstractions;

namespace Regira.Entities.Preppers;

/// <summary>
/// Enforces <see cref="ServerOwnedAttribute"/> on <typeparamref name="TEntity"/>: every marked scalar is
/// restored from the stored row on update. Create is left alone — the attribute is protect-only;
/// <see cref="ServerOwnedPrepper{TEntity,TProp}"/> is the form that also mints.
/// </summary>
public class AutoServerOwnedPrepper<TEntity> : EntityPrepperBase<TEntity>
    where TEntity : class
{
    public override Task Prepare(TEntity modified, TEntity? original, CancellationToken token = default)
    {
        if (original != null)
        {
            // the runtime type, not TEntity: this runs registered against IEntity
            foreach (var property in ServerOwnedProperties.Protected(modified.GetType()))
            {
                property.SetValue(modified, property.GetValue(original));
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// <inheritdoc cref="AutoServerOwnedPrepper{TEntity}"/>
/// <para>Registered for every entity by <c>UseDefaults()</c>.</para>
/// </summary>
public class AutoServerOwnedPrepper : AutoServerOwnedPrepper<IEntity>;
