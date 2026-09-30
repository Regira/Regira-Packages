using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Entities.Models;
using Regira.Entities.Web.Controllers;
using Regira.Entities.Web.DependencyInjection;
using System.Text.Json;

namespace Entities.Web.Testing;

// The entity exceptions used to be mapped only inside ControllerExtensions.Save/Delete, so a hand-written
// domain action on an entity controller — which writes through IEntityService and runs the same preppers —
// answered 500 for a rule breach the generated PUT answered 400 for.
public class EntityExceptionFilterTests
{
    private static ExceptionContext ContextFor(Exception ex) => new(
        new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), new ModelStateDictionary()),
        [])
    { Exception = ex };

    [Fact]
    public void InputException_Becomes_400_With_Its_Field_Errors()
    {
        var ex = new EntityInputException<object>("rejected") { InputErrors = { ["Status"] = "Only a submitted request can be approved." } };
        var context = ContextFor(ex);

        new EntityExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        // BadRequest(ModelState) serializes as a SerializableError — the same body ControllerExtensions.Save
        // returns, so a hand-written action and the generated one are indistinguishable to a client.
        var errors = Errors(context);
        Assert.Equal(["Only a submitted request can be approved."], Assert.IsType<string[]>(errors["Status"]));
    }

    // The body is a dictionary, so the camelCase naming policy of ConfigureDefaultJsonOptions() does not reach its keys:
    // each goes out as it was thrown. The concurrency recipe documents the key a required version stamp left out gets.
    [Fact]
    public void InputException_Keys_Go_Out_As_Thrown()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.ConfigureDefaultJsonOptions();
        using var sp = services.BuildServiceProvider();
        var json = sp.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        var context = ContextFor(new EntityInputException<object>("rejected")
        {
            InputErrors = { ["ConcurrencyToken"] = "Required on an update: send the value read with the record." }
        });

        new EntityExceptionFilter().OnException(context);

        Assert.Equal("""{"ConcurrencyToken":["Required on an update: send the value read with the record."]}""",
            JsonSerializer.Serialize(Errors(context), json));
    }

    private static SerializableError Errors(ExceptionContext context) =>
        Assert.IsType<SerializableError>(Assert.IsType<BadRequestObjectResult>(context.Result).Value);

    // A prepper guarding a related entity throws EntityInputException<Product> while the action's own TEntity
    // is Order. The generated actions catch one closed generic and miss that; the filter matches the base.
    [Fact]
    public void InputException_Is_Matched_Whatever_Entity_It_Names()
    {
        var context = ContextFor(new EntityInputException<string>("rejected"));

        new EntityExceptionFilter().OnException(context);

        Assert.IsType<BadRequestObjectResult>(context.Result);
    }

    // Errors is what the body is built from: several messages of one field stay separate entries, where InputErrors
    // shows them joined.
    [Fact]
    public void InputException_Errors_Keep_Every_Message_Of_A_Field()
    {
        var ex = new EntityInputException<object>("rejected")
        {
            Errors = { new EntityInputError("Code", "At most 5 characters."), new EntityInputError("Code", "Upper case only.") }
        };
        var context = ContextFor(ex);

        new EntityExceptionFilter().OnException(context);

        Assert.Equal(["At most 5 characters.", "Upper case only."], Assert.IsType<string[]>(Errors(context)["Code"]));
        Assert.Equal("At most 5 characters. Upper case only.", ex.InputErrors["Code"]);
    }

    // InputErrors is a view over Errors, not a second store: a field a handler adds to a caught rejection before it
    // rethrows reaches the body next to the validators' errors.
    [Fact]
    public void InputException_A_Field_Added_Through_InputErrors_Joins_The_Errors()
    {
        var ex = new EntityInputException<object>("rejected") { Errors = { new EntityInputError("Name", "A name is required.") } };
        ex.InputErrors["Code"] = "Code is taken.";
        var context = ContextFor(ex);

        new EntityExceptionFilter().OnException(context);

        Assert.Equal(["A name is required."], Assert.IsType<string[]>(Errors(context)["Name"]));
        Assert.Equal(["Code is taken."], Assert.IsType<string[]>(Errors(context)["Code"]));
    }

    [Fact]
    public void InputException_Without_Field_Errors_Still_Carries_Its_Message()
    {
        var context = ContextFor(new EntityInputException<object>("Quantity must be positive."));

        new EntityExceptionFilter().OnException(context);

        Assert.Equal(["Quantity must be positive."], Assert.IsType<string[]>(Errors(context)[string.Empty]));
    }

    [Fact]
    public void ConstraintException_Becomes_409_With_The_Shared_Problem_Body()
    {
        var context = ContextFor(new EntityConstraintException("UNIQUE constraint failed"));

        new EntityExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        var result = Assert.IsType<ConflictObjectResult>(context.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(EntityConstraintException.ClientMessage, problem.Detail);
    }

    [Fact]
    public void ConcurrencyException_Becomes_409_With_Its_Own_Problem_Body()
    {
        var context = ContextFor(new EntityConcurrencyException(EntityConcurrencyException.ClientMessage));

        new EntityExceptionFilter().OnException(context);

        Assert.True(context.ExceptionHandled);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ConflictObjectResult>(context.Result).Value);
        Assert.Equal(EntityConcurrencyException.ClientMessage, problem.Detail);
        // both conflicts answer 409; the title is how a client tells "reload and retry" from "fix the input"
        Assert.NotEqual(Regira.Entities.Web.EntityConstraintProblem.Create().Title, problem.Title);
    }

    // The attachment controller bases carry the attribute, so a host without the global filter still answers 409.
    [Fact]
    public void The_Conflict_Attribute_Maps_A_Concurrency_Conflict_Too()
    {
        var context = ContextFor(new EntityConcurrencyException(EntityConcurrencyException.ClientMessage));

        new EntityConstraintConflictAttribute().OnException(context);

        Assert.True(context.ExceptionHandled);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ConflictObjectResult>(context.Result).Value);
        Assert.Equal(Regira.Entities.Web.EntityConcurrencyProblem.Create().Title, problem.Title);
    }

    [Fact]
    public void Other_Exceptions_Are_Left_Alone()
    {
        var context = ContextFor(new InvalidOperationException("boom"));

        new EntityExceptionFilter().OnException(context);

        Assert.False(context.ExceptionHandled);
        Assert.Null(context.Result);
    }

    [Fact]
    public void ConfigureDefaultJsonOptions_Registers_The_Filter_Once()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        // Repeated setup calls are common (UseEntities + a second options instance); a duplicate filter would
        // add the same model errors twice.
        services.ConfigureDefaultJsonOptions();
        services.MapEntityExceptions();

        using var sp = services.BuildServiceProvider();
        var filters = sp.GetRequiredService<IOptions<MvcOptions>>().Value.Filters;

        Assert.Single(filters.OfType<EntityExceptionFilter>());
    }

    // `Filters.Add<T>()` records a TypeFilterAttribute, not an instance, so an `is EntityExceptionFilter`
    // test alone would not see a consumer's own registration and would add a second copy.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_Consumer_Registration_By_Type_Is_Not_Duplicated(bool asServiceFilter)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<MvcOptions>(o =>
        {
            if (asServiceFilter) o.Filters.AddService<EntityExceptionFilter>();
            else o.Filters.Add<EntityExceptionFilter>();
        });
        services.MapEntityExceptions();

        using var sp = services.BuildServiceProvider();
        var filters = sp.GetRequiredService<IOptions<MvcOptions>>().Value.Filters;

        Assert.Single(filters);
    }
}
