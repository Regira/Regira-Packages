using Microsoft.Extensions.Configuration;
using NUnit.Framework.Legacy;
using Regira.Invoicing.Payments.Models;
using Regira.Payments.Mollie.Config;
using Regira.Payments.Mollie.Services;
using Regira.Utilities;

[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Payments.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class MollieTests
{
    private readonly PaymentService _repo;
    public MollieTests()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(GetType().Assembly)
            .Build();
        var mollieConfig = new MollieConfig
        {
            Api = config["Payments:Mollie:Api"]!,
            Key = config["Payments:Mollie:Key"]!,
            RedirectFactory = _ => "http://localhost"
        };
        _repo = new PaymentService(mollieConfig);
    }

    //[TestCase(.23, "0.23")]
    //[TestCase(1.23, "1.23")]
    //[TestCase(10.23, "10.23")]
    //[TestCase(0, "0.00")]
    //[TestCase(1, "1.00")]
    //[TestCase(10, "10.00")]
    //public void Test_Parse_MollieAmount(decimal value, string expected)
    //{
    //    var amount = value;
    //    ClassicAssert.AreEqual(amount, expected);
    //}

    [Test]
    public async Task Create()
    {
        var rnd = new Random();
        var payment = new Payment
        {
            Amount = rnd.Next(1, 1000) / 100m,
            Description = "Test payment"
        };
        await _repo.Save(payment);
        ClassicAssert.IsNotNull(payment.Id);
    }
    [Test]
    public async Task GetPayment()
    {
        var items = (await _repo.List()).AsList();
        Assert.That(items, Is.Not.Empty);
        var details = await _repo.Details(items.Last().Id);
        ClassicAssert.IsNotNull(details);
    }

    [Test]
    public async Task GetPayments_Over_5Euro()
    {
        var items = (await _repo.List(DictionaryUtility.ToDictionary(new { MinAmount = 5m })))
            .AsList();
        Assert.That(items, Is.Not.Empty);
        foreach (var payment in items)
        {
            Assert.That(payment.Amount >= 5, Is.True);
        }
    }

    [Test]
    public async Task Create_With_MetaData()
    {
        var rnd = new Random();
        var payment = new Payment
        {
            Amount = rnd.Next(1, 1000) / 100m,
            Description = "Test payment",
            Metadata = DictionaryUtility.ToDictionary(new
            {
                orderId = Guid.NewGuid().ToString(),
                appId = Guid.NewGuid().ToString(),
                email = "xxx.xxx@gmail.com",
                name = "XXXÔXçÑ  Xxxóxx"
            })
        };
        await _repo.Save(payment);
        ClassicAssert.IsNotNull(payment.Id);
    }
}