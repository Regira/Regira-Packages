using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Entities.Mediator.Testing.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Mediator;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models;
using Regira.Entities.Web.Controllers;

namespace Entities.Mediator.Testing;

/// <summary>
/// The input DTO's DataAnnotations, checked before a save or a patch maps it: with MVC's own model validation in an MVC
/// host, with <see cref="DataAnnotationsEntityInputValidator"/> elsewhere.
/// </summary>
public class InputValidationTests
{
    public class Order
    {
        [Required]
        public string? Code { get; set; }
        public Address? Address { get; set; }
        public List<OrderLine> Lines { get; set; } = [];
        public byte[]? Scan { get; set; }
        public Order? Self { get; set; }
    }
    public class Address
    {
        [Required]
        public string? Street { get; set; }
    }
    public class OrderLine
    {
        [Range(1, 10)]
        public int Quantity { get; set; }
    }

    [Fact]
    public void DataAnnotations_Descend_Into_Objects_And_Collection_Items_Keyed_By_Path()
    {
        var order = new Order
        {
            Address = new Address(),
            Lines = [new OrderLine { Quantity = 1 }, new OrderLine { Quantity = 11 }],
            Scan = new byte[100_000]
        };
        order.Self = order;

        var errors = DataAnnotationsEntityInputValidator.Instance.Validate(order);

        Assert.Equal(["Code", "Address.Street", "Lines[1].Quantity"], errors.Select(x => x.Key));
    }

    [Fact]
    public void DataAnnotations_Alone_Do_Not_Require_A_NonNullable_String()
        => Assert.Empty(DataAnnotationsEntityInputValidator.Instance.Validate(new ProductInputDto { Title = null! }));

    [Fact]
    public async Task A_Job_Is_Refused_An_Invalid_Input_With_The_Errors_Of_A_Validator()
    {
        await using var shop = await Shop.CreateServicesAsync();
        using var scope = shop.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEntitySender>();

        var ex = await Assert.ThrowsAsync<EntityInputException<Product>>(() =>
            sender.Send(new SaveCommand<Product, int, Product, ProductInputDto>(new ProductInputDto { Title = "Ink", Stock = 5000 })));

        Assert.Equal(["Stock"], ex.Errors.Select(x => x.Key));
    }

    [Fact]
    public async Task Without_Input_Validation_The_Write_Pipeline_Alone_Judges()
    {
        await using var shop = await Shop.CreateServicesAsync();
        using var scope = shop.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEntitySender>();

        var saved = await sender.Send(new SaveCommand<Product, int, Product, ProductInputDto>(
            new ProductInputDto { Title = "Ink", Stock = 5000 }, ValidateInput: false));

        Assert.Equal(5000, saved!.Item.Stock);
    }

    [Fact]
    public async Task An_Mvc_Host_Validates_With_Mvc()
    {
        await using var shop = await Shop.CreateHostAsync();
        using var scope = shop.Services.CreateScope();

        Assert.IsType<MvcEntityInputValidator>(scope.ServiceProvider.GetRequiredService<IEntityInputValidator>());
    }

    [Fact]
    public async Task A_Patch_Fails_Where_The_Same_Body_Sent_To_Put_Would()
    {
        await using var shop = await Shop.CreateHostAsync();

        // the merge drops a null title, so the merged input's non-nullable Title is null: MVC refuses that, as for a PUT
        var response = await shop.Client!.PatchAsync($"/products/{shop.PenId}", JsonContent.Create(new { title = (string?)null }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("Title", out _));
        Assert.Equal("Title", body.RootElement.GetProperty("errorDetails")[0].GetProperty("key").GetString());
        Assert.Equal("Pen", (await shop.FindAsync(shop.PenId))!.Title);
    }

    [Fact]
    public async Task A_Put_Is_Judged_By_Mvc_Model_Binding_Once()
    {
        await using var shop = await Shop.CreateHostAsync(services =>
            services.Configure<ApiBehaviorOptions>(o => o.SuppressModelStateInvalidFilter = true));

        // the app suppressed MVC's automatic 400: the generated PUT saves what model binding let through, as it always did
        var response = await shop.Client!.PutAsJsonAsync($"/products/{shop.PenId}", new ProductInputDto { Title = "Pen", Stock = 5000 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(5000, (await shop.FindAsync(shop.PenId))!.Stock);
    }
}
