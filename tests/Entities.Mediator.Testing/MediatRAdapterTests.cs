using System.Net;
using System.Net.Http.Json;
using Entities.Mediator.Testing.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Mediator;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.MediatR;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Web.Models;

namespace Entities.Mediator.Testing;

/// <summary>
/// With <c>UseMediatR()</c>, every entity request — the generated endpoints' and a job's alike — runs inside the app's
/// MediatR pipeline, while handler overrides, the entity behaviours and <c>Duration</c> stay as they are.
/// </summary>
public class MediatRAdapterTests
{
    /// <summary>What the app's behaviour saw: the entity and the operation of every entity request.</summary>
    public sealed class Recorder
    {
        public List<(Type Entity, EntityOperation Operation)> Seen { get; } = [];
    }

    /// <summary>An app's ordinary open MediatR behaviour; it reads the envelope to tell a read from a write.</summary>
    public sealed class RecordingBehavior<TRequest, TResponse>(Recorder recorder) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (request is EntityRequestMessage message)
            {
                recorder.Seen.Add((message.Request.EntityType, message.Request.Operation));
            }
            return await next();
        }
    }

    private static void AddMediatR(IServiceCollection services)
    {
        services.AddSingleton<Recorder>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Recorder>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RecordingBehavior<,>));
    }

    private static Task<Shop> CreateHostAsync(Action<IServiceCollection>? services = null)
        => Shop.CreateHostAsync(s =>
        {
            AddMediatR(s);
            services?.Invoke(s);
        }, o => o.UseMediatR());

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
            services.AddTransient<IEntityRequestHandler<DetailsQuery<Product, int, Product>, DetailsResult<Product>>, EntityRequestExecutorTests.FixedDetailsHandler>());

        var response = await shop.Client!.GetFromJsonAsync<DetailsResult<Product>>($"/products/{shop.PenId}");

        Assert.Equal("Fixed", response!.Item.Title);
        Assert.Single(shop.Services.GetRequiredService<Recorder>().Seen);
    }

    [Fact]
    public async Task A_Job_Sends_Through_MediatR_Too()
    {
        await using var shop = await CreateHostAsync();
        using var scope = shop.Services.CreateScope();

        var saved = await scope.ServiceProvider.GetRequiredService<IEntitySender>()
            .Send(new SaveCommand<Product, int, Product, ProductInputDto>(new ProductInputDto { Title = "Ink", Stock = 3 }));

        Assert.True(saved!.IsNew);
        Assert.Equal([(typeof(Product), EntityOperation.Save)], shop.Services.GetRequiredService<Recorder>().Seen);
    }

    [Fact]
    public async Task UseMediatR_Replaces_The_InHouse_Sender_Whatever_The_Order()
    {
        // inside UseEntities(): before UseEntities() registers the in-house sender
        await using (var inside = await CreateHostAsync())
        {
            using var scope = inside.Services.CreateScope();
            Assert.IsType<MediatREntitySender>(scope.ServiceProvider.GetRequiredService<IEntitySender>());
        }

        // after it, on the options of a later UseEntities() call
        await using var after = await Shop.CreateHostAsync(services =>
        {
            AddMediatR(services);
            services.UseEntities().UseMediatR();
        });
        using (var scope = after.Services.CreateScope())
        {
            Assert.IsType<MediatREntitySender>(scope.ServiceProvider.GetRequiredService<IEntitySender>());
        }
        await after.Client!.GetAsync($"/products/{after.PenId}");
        Assert.Single(after.Services.GetRequiredService<Recorder>().Seen);
    }

    /// <summary>A faulty app behaviour that answers every request with something of its own.</summary>
    public sealed class ReplacingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
            => Task.FromResult((TResponse)(object)"replaced");
    }

    [Fact]
    public async Task A_Response_Of_The_Wrong_Type_Throws_Instead_Of_Reading_As_Not_Found()
    {
        await using var shop = await CreateHostAsync(services =>
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ReplacingBehavior<,>)));
        using var scope = shop.Services.CreateScope();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEntitySender>().Send(new DetailsQuery<Product, int, Product>(shop.PenId)));

        Assert.Contains("String", ex.Message);
        Assert.Contains("DetailsResult", ex.Message);
    }

    [Fact]
    public async Task Without_MediatR_Registered_The_Sender_Says_What_Is_Missing()
    {
        await using var shop = await Shop.CreateServicesAsync(entities: o => o.UseMediatR());
        using var scope = shop.Services.CreateScope();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEntitySender>().Send(new DetailsQuery<Product, int, Product>(shop.PenId)));

        Assert.Contains("AddMediatR", ex.Message);
    }
}
