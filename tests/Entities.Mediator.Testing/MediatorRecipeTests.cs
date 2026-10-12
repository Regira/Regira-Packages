using System.Net;
using System.Net.Http.Json;
using Entities.Mediator.Testing.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Entities.Mediator;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Web.Models;

namespace Entities.Mediator.Testing;

// The recipe of entities.patterns (Dispatching through a mediator library), as an app writes it
public sealed record EntityMessage(IEntityRequest Request, Func<IEntitySender, CancellationToken, Task<object?>> Run) : IRequest<object?>;

public class EntityMessageHandler(IServiceProvider services) : IRequestHandler<EntityMessage, object?>
{
    public Task<object?> Handle(EntityMessage message, CancellationToken token)
        => message.Run(ActivatorUtilities.CreateInstance<EntitySender>(services), token);
}

public class MediatREntitySender(ISender mediator) : IEntitySender
{
    public async Task<TResponse?> Send<TResponse>(IEntityRequest<TResponse> request, CancellationToken token = default)
        => (TResponse?)await mediator.Send(new EntityMessage(request, async (inner, t) => await inner.Send(request, t)), token);
}

/// <summary>
/// With the recipe's sender, every entity request — the generated endpoints' and a job's alike — runs inside the app's
/// MediatR pipeline, while handler overrides, the entity behaviours and <c>Duration</c> stay as they are.
/// </summary>
public class MediatorRecipeTests
{
    /// <summary>What the app's behaviour saw: the entity and the operation of every entity request.</summary>
    public sealed class Recorder
    {
        public List<(Type Entity, EntityOperation Operation)> Seen { get; } = [];
    }

    /// <summary>An app's ordinary open MediatR behaviour; it reads the message to tell a read from a write.</summary>
    public sealed class RecordingBehavior<TRequest, TResponse>(Recorder recorder) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (request is EntityMessage message)
            {
                recorder.Seen.Add((message.Request.EntityType, message.Request.Operation));
            }
            return await next();
        }
    }

    private static Task<Shop> CreateHostAsync(Action<IServiceCollection>? services = null)
        => Shop.CreateHostAsync(s =>
        {
            s.AddSingleton<Recorder>();
            s.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<EntityMessage>());
            s.AddTransient(typeof(IPipelineBehavior<,>), typeof(RecordingBehavior<,>));
            s.Replace(ServiceDescriptor.Scoped<IEntitySender, MediatREntitySender>());
            services?.Invoke(s);
        });

    [Fact]
    public async Task A_Generated_Endpoint_Runs_Inside_The_Apps_Pipeline()
    {
        await using var shop = await CreateHostAsync();

        var response = await shop.Client!.GetFromJsonAsync<DetailsResult<Product>>($"/products/{shop.PenId}");

        Assert.Equal("Pen", response!.Item.Title);
        Assert.NotNull(response.Duration);
        Assert.Equal([(typeof(Product), EntityOperation.Details)], shop.Services.GetRequiredService<Recorder>().Seen);
    }

    [Fact]
    public async Task A_Refused_Write_Still_Answers_400()
    {
        await using var shop = await CreateHostAsync();

        var response = await shop.Client!.PutAsJsonAsync($"/products/{shop.PenId}", new ProductInputDto { Title = Shop.RefusedTitle, Stock = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errorDetails", await response.Content.ReadAsStringAsync());
        Assert.Equal([(typeof(Product), EntityOperation.Save)], shop.Services.GetRequiredService<Recorder>().Seen);
    }

    [Fact]
    public async Task A_Closed_Handler_Still_Overrides()
    {
        await using var shop = await CreateHostAsync(services =>
            services.AddTransient<IEntityRequestHandler<DetailsQuery<Product, int, Product>, DetailsResult<Product>>, EntitySenderTests.FixedDetailsHandler>());

        var response = await shop.Client!.GetFromJsonAsync<DetailsResult<Product>>($"/products/{shop.PenId}");

        Assert.Equal("Fixed", response!.Item.Title);
        Assert.Single(shop.Services.GetRequiredService<Recorder>().Seen);
    }

    [Fact]
    public async Task A_Job_Sends_Through_The_Mediator_Too()
    {
        await using var shop = await CreateHostAsync();
        using var scope = shop.Services.CreateScope();

        var saved = await scope.ServiceProvider.GetRequiredService<IEntitySender>()
            .Send(new SaveCommand<Product, int, Product, ProductInputDto>(new ProductInputDto { Title = "Ink", Stock = 3 }));

        Assert.True(saved!.IsNew);
        Assert.Equal([(typeof(Product), EntityOperation.Save)], shop.Services.GetRequiredService<Recorder>().Seen);
    }
}
