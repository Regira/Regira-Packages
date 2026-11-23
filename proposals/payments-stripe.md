# A Stripe payment provider

As of 2026-11-23. Sources: the `Regira-Packages` repository, branch `wip` at `60b3414`; the nuget.org nuspec of Stripe.net 53.2.0 and of Mollie.Api 5.1.1, read on 2026-11-23; and the README, `EventUtility.cs`, `V1Services.cs` and `ApiVersion.cs` on the `master` branch of stripe-dotnet, read the same day. `master` can be ahead of 53.2.0. How Stripe's API behaves (session and payment statuses, expiry limits, metadata limits, currency exponents) is taken from Stripe's documentation as remembered and was not measured here. Nothing on this page has been built.

**Status: proposal, not scheduled. Six decisions are open (see *Decisions*).**

## Recommendation

Add `Regira.Payments.Stripe`, a payment provider on Stripe's own SDK (`Stripe.net`). Its `PaymentService` implements `IPaymentService` and does what the Mollie and POM providers do: it creates a payment, returns the address to send the payer to, reads the payment back, and tells the application when it was paid. The Stripe object behind an `IPayment` is a **Checkout Session**, Stripe's hosted payment page, because that is the one with an address to redirect to.

Stripe is the third provider, and with it the shape the three share is plain: create a payment, get a URL. Today that URL has two names and no contract (`CreatePaymentResponse.CheckoutUrl` on Mollie, `PomPaymentResponse.Paylink` on POM, nothing on `IPaymentService.Save`). Add one small contract for it in `Regira.Invoicing`, implemented by all three, so an application can change provider without changing the code that starts a payment (decision 1).

The gap this closes: `Regira.Payments` covers Mollie and POM, both centred on the Benelux. An application that sells outside it, or whose owner already has a Stripe account, has no provider.

The steps, in order:

1. A spike with a test-mode key: the statuses, the expiry limits and the webhook payload.
2. The package: configuration, `Details`, the amount and status mapping.
3. Creating a checkout, with idempotency; cancelling one.
4. The webhook.
5. The shared checkout contract, on all three providers.
6. Guides, docs, routing table and the changelog.

## What exists today

| What | Where | Effect |
|---|---|---|
| The shared contracts live in `Regira.Invoicing` | `IPayment`, `IPaymentService` (`Details(string)`, `Save(IPayment)`), `Payment`, `PaymentStatus` (`Open`, `Canceled`, `Pending`, `Authorized`, `Expired`, `Failed`, `Paid`) in `src/Common.Invoicing/Payments` | A provider references `Regira.Invoicing` and maps onto these |
| `Regira.Payments` holds no code | `src/Common.Payments`: guides and the build targets that extract them | The Stripe provider is documented in its guide, and adds nothing to its assembly |
| Mollie implements no interface | `src/Payments.Mollie/Services/PaymentService.cs`: `Details(object)`, `List`, `Save` returning `CreatePaymentResponse` (`Id`, `CheckoutUrl`), `Save(item, idempotencyKey)`, `Delete` (cancels), `WebHook(id, handler)` | Code that starts a Mollie payment depends on the concrete class |
| POM implements `IPaymentService` | `src/Payments.Pom/PaymentService.cs`: `Save` creates a link and returns nothing; `CreatePaylink` on the concrete class returns it | Code that needs the link depends on the concrete class here too |
| Mollie's webhook reads the payment back | `WebHook(id, handler)` calls `Details(id)` and hands the result to the handler | The handler never trusts what the request carried. The Stripe webhook keeps this |
| One connection pool per provider | A static `SocketsHttpHandler` with `PooledConnectionLifetime` and no cookies, wrapped per call; an `internal Handler` property a test points at a stub. `ai/learnings.md` records the rule | The Stripe provider hands the SDK an `HttpClient` on such a handler |
| Offline provider tests | `tests/Payments.Testing/MollieClientTests.cs` runs the service against a stub `HttpMessageHandler` | The Stripe tests take this shape |
| The guide compares the providers | *Shared Abstraction* and *Provider Comparison* in `src/Common.Payments/ai/payments.instructions.md` | A third column, and the first section changes with decision 1 |
| Mollie declares a marker interface for its exceptions | `IPaymentException` in `Regira.Payments.Mollie.Exceptions` | It is not shared, so the Stripe provider cannot implement it without referencing Mollie |

## What a payment is on Stripe

Stripe has three objects that could stand behind an `IPayment`:

- A **Checkout Session** is one visit to Stripe's hosted payment page. It has a URL, an expiry, and a payment once the payer completes it.
- A **PaymentIntent** is the payment itself. It has no page: the application's own front end collects the card through Stripe's JavaScript.
- A **Payment Link** is a reusable page for a fixed product, not one payment.

Mollie's `Save` and POM's `CreatePaylink` both return a page for one payment, which is a Checkout Session. `IPayment.Id` is therefore the session's id.

| `IPayment` | Checkout Session | Notes |
|---|---|---|
| `Id` | `id` (`cs_…`) | Set by `Save`, as on Mollie and POM |
| `Amount`, `Currency` | One line item: `price_data.unit_amount` and `price_data.currency`, quantity 1 | The amount is converted to the currency's minor unit (see *Amounts*) |
| `Description` | The line item's `price_data.product_data.name`, and the payment's description | Stripe requires a name: an empty `Description` falls back to a configured default (see *Configuration*) |
| `Metadata` | `metadata` on the session and on its payment | Stripe's metadata is text to text. See *Metadata* |
| `ExpiresAt` | `expires_at` | Stripe accepts a time between 30 minutes and 24 hours after creation. See decision 3 |
| `CreatedAt` | `created` | In UTC |
| `Status` | From the session's `status` and `payment_status`, and its payment's `status` | See *Status* |

### Amounts

Stripe takes an amount as a whole number of the currency's smallest unit: 4999 for EUR 49.99. The exponent depends on the currency. Most have two decimals, some have none (JPY, KRW and others), and a few have three. A fixed "times 100" overcharges a yen payment a hundredfold.

- The provider converts with a table of Stripe's zero-decimal and three-decimal currencies, read from Stripe's currency page in step 1, and two decimals for everything else.
- An amount with more decimals than the currency has throws `ArgumentException`. Rounding money without being asked is not this package's call.
- `Details` converts back with the same table.
- `Currency` is sent in lower case. A `null` currency is `EUR`, as on Mollie.

### Status

To be confirmed against the API in step 1:

| Stripe | `PaymentStatus` |
|---|---|
| Session `open` | `Open` |
| Session `expired` | `Expired` |
| Session `complete`, `payment_status` `paid` | `Paid` |
| Session `complete`, `payment_status` `unpaid`: a delayed method such as a bank debit is still processing | `Pending` |
| Payment `requires_capture` | `Authorized` |
| Payment `canceled` | `Canceled` |
| A delayed payment that failed after the session completed | `Failed` |

A card that is declined on the page does not fail the session: the payer can try again until it expires, so the session stays `Open`.

### Metadata

`IPayment.Metadata` is `IDictionary<string, object?>`. Stripe stores text keys and text values, with limits on their number and length.

- A value is written with its invariant-culture text; `null` is left out.
- `Details` returns every value as a `string`. On Mollie a number comes back as a number. The guide says so, with the advice to store ids as text on every provider.
- The metadata goes on the session and on its payment, so it is present whichever of the two an application looks up in Stripe's dashboard.
- Metadata over Stripe's limits is refused by Stripe, and that error is passed on.

## Design

### Package

- `src/Payments.Stripe/`, package and assembly `Regira.Payments.Stripe`, namespace `Regira.Payments.Stripe`, Apache-2.0. Target frameworks as the rest of the family.
- It references `Regira.Invoicing` and `Stripe.net`.
- The SDK's root namespace is `Stripe`. Inside `Regira.Payments.Stripe` the simple name `Stripe` binds to the package's own namespace, so the source reaches SDK types through `using` directives and never through a qualified `Stripe.` name.

### Configuration

`StripeConfig`:

| Property | Meaning |
|---|---|
| `SecretKey` | The API key. An `sk_test_…` key is test mode and an `sk_live_…` key takes real payments; there is no separate switch, as on Mollie |
| `WebhookSecret` | The signing secret of the webhook endpoint (`whsec_…`). Required by `WebHook` only |
| `SuccessUrlFactory` | `Func<IPayment, string>`: where Stripe sends the payer after paying. Required |
| `CancelUrlFactory` | `Func<IPayment, string>?`: where the page's back link leads |
| `DefaultDescription` | The line item's name when `IPayment.Description` is empty |

There is no `WebhookFactory`. Mollie takes a webhook address per payment; Stripe posts every event of an account to the endpoints registered in its dashboard.

### `PaymentService`

<!-- no-compile -->
```csharp
public class PaymentService(StripeConfig config) : IPaymentService
{
    public Task<IPayment?> Details(string paymentId);
    public Task Save(IPayment item);                                    // IPaymentService
    public Task<PaymentCheckout> CreateCheckout(IPayment item, string? idempotencyKey = null);
    public Task Delete(IPayment item);                                  // expires an open session
    public Task WebHook(string json, string signatureHeader, Func<IPayment?, Task> handle);
}
```

- **`Details`** reads the session with its payment expanded, and returns `null` when Stripe answers that no such session exists.
- **`CreateCheckout`** creates a session in `payment` mode, sets `item.Id`, and returns the id and the page's URL. `Save` calls it and drops the result, as POM's `Save` does with `CreatePaylink`.
- **Idempotency.** The key is sent as the request's idempotency key: a repeat with the same key gets the first session back. The SDK retries a failed request twice by default and adds a key of its own for those retries, so a retry inside one call is safe without one. A repeat of the *call*, after a timeout say, is only safe with the caller's key. The guide repeats Mollie's sentence: never repeat a `Save` without one.
- **`Delete`** expires the session, which is Stripe's cancel for a page nobody has paid on. A session that is complete or expired is refused by Stripe.
- **HTTP.** One static `SocketsHttpHandler`, as on Mollie and POM. The service builds a `StripeClient` per instance over an `HttpClient` on that handler, and never touches the SDK's global `StripeConfiguration`. Two services with different keys can therefore live in one process, and a test points the handler at a stub.

### The webhook

Stripe posts a JSON event and signs it in the `Stripe-Signature` header.

1. `WebHook` validates the signature over the raw body with `EventUtility.ValidateSignature`, which also refuses a timestamp older than its tolerance. A request that fails throws, and the endpoint answers 400.
2. It reads two values from the JSON: the event's `type` and the id of the object it carries.
3. For `checkout.session.completed`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed` and `checkout.session.expired`, it calls `Details(id)` and hands the payment to the handler. Every other event is acknowledged and ignored.

Reading the payment back, as Mollie's `WebHook` does, has two effects. Events can arrive out of order or twice, and the handler always sees the payment as it is now. And the event is never parsed into SDK types: `EventUtility.ParseEvent` throws by default when the event was written for another API version than the SDK's (`throwOnApiVersionMismatch = true`), which is what an account whose webhook endpoint was created under an older API version sends.

The guide carries the two rules a Stripe webhook breaks on:

- **The raw body.** The signature is over the bytes Stripe sent. An endpoint that lets model binding parse the JSON and serializes it again fails every check. Read `Request.Body` as text.
- **Idempotent handling.** Stripe redelivers an event until it gets a 2xx, and may deliver one twice. A handler that marks an order paid checks that it is not paid already.

## Dependencies

- **Stripe.net** 53.2.0. Assets for `net8.0`, `net9.0`, `net10.0`, `netstandard2.0` and `net462`; it depends on Newtonsoft.Json 13.0.3 and System.Configuration.ConfigurationManager. Its nuspec names a licence file, not an expression: stripe-dotnet's repository is Apache-2.0, to be read from the package in step 1.
- Each Stripe.net release is pinned to one Stripe API version; `master` reads `2026-11-18.endive`. A major Stripe.net upgrade is therefore an API upgrade, and goes through the status and webhook tests before it ships (see *Risks*).

## Considered and left out

- **A payment form inside the application** (PaymentIntents with Stripe's JavaScript). It needs front-end code, and belongs with the Vue modules if it is wanted.
- **Refunds, captures of an authorized payment, subscriptions, invoices, Connect, tax.** None of them is in `IPaymentService`, and no provider here has them. An application that needs one uses `Stripe.net` beside this package, with the same key.
- **`List`.** Mollie has it; POM does not. Stripe's dashboard and its own SDK cover it, and nothing in the shared contract asks for it.
- **A wrapping exception.** Mollie and POM wrap their errors because their clients throw transport exceptions. The SDK's own `StripeException` already carries the HTTP status and Stripe's error object, and a second type named after it would collide with it. The provider lets it through, and the guide names it.
- **Moving the payment contracts out of `Regira.Invoicing`.** The hub holding no code while the contracts live in another family is odd, but moving them breaks every consumer's namespaces. It is a question for a major release, and not this proposal's.

## Decisions

1. **A shared checkout contract?** In `Regira.Invoicing.Payments.Abstractions`:

   <!-- no-compile -->
   ```csharp
   public interface IPaymentCheckoutService : IPaymentService
   {
       Task<PaymentCheckout> CreateCheckout(IPayment item, string? idempotencyKey = null);
   }
   public record PaymentCheckout(string Id, string? Url);
   ```

   - Stripe implements it from the start.
   - Mollie implements it and `IPaymentService` beside its existing members; `CreatePaymentResponse` and `Details(object)` stay.
   - POM implements it over `CreatePaylink`. POM has no idempotency key, so a key given there is ignored, and the guide says so.

   Recommendation: yes, as step 5, after the Stripe provider has shown the shape fits. It is a new type in `Regira.Invoicing` (minor) and added members on the two providers (patch).
2. **The Stripe object: Checkout Session, or PaymentIntent?** See *What a payment is on Stripe*. Recommendation: Checkout Session.
3. **An `ExpiresAt` outside Stripe's limits.** POM's default is one day and its guide example sets thirty.
   - Option 1: pass it on, and let Stripe refuse it.
   - Option 2: throw `ArgumentOutOfRangeException` before the request, naming the limits.
   - Option 3: clamp it to the nearest limit.

   Recommendation: option 2. A payment page that silently expires 29 days early is worse than an exception, and Stripe's own message does not name the property. A `null` leaves the expiry to Stripe.
4. **An empty `Description`.** Stripe requires a product name. Either `StripeConfig.DefaultDescription` supplies one and `CreateCheckout` throws when both are empty (recommended), or the package invents a name such as "Payment".
5. **Which events reach the handler.** The four session events (recommended), or every event with the payment `null` for the ones that carry none. The second makes the handler filter on something it cannot see.
6. **Tests against Stripe.** Offline tests with a stub handler are the base. A second fixture in the `Network` category with a test-mode key from user secrets creates, reads and expires a session (recommended), or none, relying on the spike alone.

## Steps

1. **Spike.** With a test-mode key: create a session, pay it with a test card, pay one with a delayed test method, let one expire. Record the status values against *Status*, the limits Stripe enforces on `expires_at` and metadata, the JSON of the four events, and the currency lists for *Amounts*. Read the licence in the Stripe.net package.
2. **The package and the read path.**
   - `src/Payments.Stripe/` with its `.csproj`, `README.md`, and the solution entry in `Regira-Packages.slnx`.
   - `StripeConfig`, `Details`, the amount table and the status mapping.
   - `tests/Payments.Testing/StripeClientTests.cs` against a stub handler, with the JSON the spike recorded: every row of the status table, and amounts in a two-decimal, a zero-decimal and a three-decimal currency in both directions.
3. **Create and cancel.** `CreateCheckout`, `Save`, `Delete`. Tests: the request Stripe receives for a payment with metadata, a description and an expiry; that the idempotency key is sent; that an amount with too many decimals and an expiry outside the limits are refused before any request.
4. **The webhook.** `WebHook`, with tests that sign a payload with `EventUtility.GenerateSignatureHeader`: a valid event reaches the handler with the payment read back; a changed body, a wrong secret and a stale timestamp are refused; an event of another type is ignored; an event written for another API version is handled.
5. **The shared contract.** `IPaymentCheckoutService` and `PaymentCheckout` in `Regira.Invoicing`, implemented by the three providers, with one test per provider through the interface.
6. **Docs.**
   - `payments.instructions.md`: the *Projects* table, *Installation*, *Shared Abstraction* (rewritten around the contract), a `## Stripe` section with `StripeConfig`, the service, *Webhook Handling* with the two rules, the amount and metadata notes, and the *Provider Comparison* column.
   - `payments.examples.md`: a checkout and a webhook endpoint.
   - `src/Common.Payments/README.md`, a `README.md` for the new project, and the `Description` in `Common.Payments.csproj`, which names Mollie and POM.
   - The `Payments` row of the routing table in `ai/AGENTS.md`.
   - The `commerce` group in `tools/GuideVerifier/projects.json`.
   - CHANGELOG bullets for `Regira.Payments.Stripe`, `Regira.Payments` (its packed guides), and with step 5 `Regira.Invoicing`, `Regira.Payments.Mollie` and `Regira.Payments.Pom`.

## Risks

- **Stripe's API version moves with the SDK.** A status value or an event's shape can change between Stripe.net majors. The provider reads few fields, and the webhook reads two, which keeps the surface small. The stub tests pin the JSON of one version, so an SDK upgrade also needs step 1's network fixture run before it ships.
- **Money conversion.** The currency table is the one place a mistake charges a customer the wrong amount. It is tested in both directions per exponent class, and an unknown currency code is passed to Stripe as two-decimal, which is what Stripe does for every currency not on its lists.
- **A webhook endpoint is public.** The signature check is the only thing between the internet and "mark this order paid". `WebHook` has no overload without a signature, and the guide's endpoint example reads the raw body.
- **The page's success URL is not proof of payment.** A payer can open it without paying. The guide says the order is marked paid from the webhook or from `Details`, never from the redirect, as it should say for Mollie.
- **Newtonsoft.Json arrives transitively.** Stripe.net depends on it. An application that pins another version resolves it as NuGet does; the provider itself does not use it.
- **A name that shadows the SDK.** A consumer inside a `Regira.Payments.*` namespace of its own who writes `Stripe.Checkout.Session` gets a compile error that points at this package's namespace. One line in the guide's troubleshooting covers it.
