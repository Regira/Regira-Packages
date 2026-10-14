# Applying the 6.6.0 advanced-apps test-run findings
<!-- {% raw %} -->

As of 2026-10-14. Sources: the report *Regira 6.6.0 pre-release test run — advanced apps* (RoomPlannerAdv and BlogAdv, generated from the release candidate on 2026-10-14; kept outside the repositories), Regira-Packages `wip` at `4885011` and Regira-Modules `wip` at `bf855fa`. Five read-only checks, one per area, re-read every finding in the code. Probes, all in a scratch folder:

- A TestServer app on the built `Security.Authentication.Web`. It covered sign-in under `SignIn.RequireConfirmedEmail`, a confirmation mail that throws, `users/confirm-email` without and with a wrong `userName`, and the shape of the mailed links.
- Builds of the corrected G22, G23, G24, G26, G27, G28, G29 and G33 snippets against the local 6.6.0 packages, the security ones under the GuideVerifier's conventions.
- Restores of `Mail.MailKit` and five other projects against today's NuGet advisory data.
- Two scaffolded advanced slices, with `vue-tsc` on the M13 recipe, and an SFC compile of `LoginModal` and `ForgotPasswordModal`.

The documentation server's side is proposed separately, in the tooling repository: S6, H6, H7 and its copy of the `after-commit` recipe, which G29 changes.

**Status: built 2026-10-14, uncommitted, with every recommended decision (1–7 (a)). See *Outcome* at the end.**

## Recommendation

The report holds up as a list of problems. 18 findings are confirmed, 2 are partly right (G30, G35), and none fails to reproduce. Its *Recommended fixes* are not safe to apply as written:

- Eleven exact changes would ship wrong or unsafe guidance, the basic report's M8 among them (*Corrections*).
- Ten more carry smaller errors, the basic report's G9 text among them.

Every step below uses the corrected change.

Four findings weigh differently from the report:

- **G8 is wider, and its fix is no longer optional.** Six projects, not only MailKit, restore a crypto package that today's advisory data flags, and no upstream release fixes them. Under rule 1 the basic report's D2 is a fix, not a decision (*1a*).
- **G24's fix is unsafe.** In the probe it gave an existing administrator the sign-up role (*1c*).
- **G33 is wider.** The section's snippet and Templates 2–4 map OpenAPI and Scalar in every environment, and neither of the report's two remedies fixes the code (*1d*).
- **M17's advice is wrong twice** (*1e*).

One found item counts as critical too: `--owns Detail` makes the scaffold's own `--overwrite-slice` advice delete the slice's `Details.vue` and `Form.vue` (F18, *1f*).

If only five things are done, do these:

1. *1a*: pin the crypto packages in six projects.
2. *1b*: refuse sign-in with an unconfirmed address when the app asks for it (D11).
3. *1c*: the guarded sign-up example.
4. *1d*: OpenAPI and Scalar in Development only.
5. *1e*: the rate-limit and token-storage warnings.

## Rules

1. **Critical is always fixed.** That means security, crashes, wrong results, and lost or corrupted content. Otherwise: fix small, document hard.
2. **A code fix starts with a test that fails on `wip`.** A finding whose test passes is dropped.
3. **Ship the shape, not the reporting app** (`AGENTS.md`). Guide prose names the mechanism. A draft, a series or a meeting room appears at most once, as the example.
4. **One home per explanation.** A rule that already has a home gets a pointer, not a second copy (G26, G35, M14, M15, M8).
5. **Guidance, not new defaults.** D11 honours an option the app already set; nothing turns on by default.
6. **Docs in final state**, as `AGENTS.md` asks.
7. **Versions stay your call.** Every package touched here sits in the unreleased 6.6.0 cycle, so the changes add changelog bullets only.
8. **These repositories are public.** Nothing here names the private tooling repository or its projects.

## Verification at a glance

| ID | Verdict | What the check found | Step |
|---|---|---|---|
| G22 | Confirmed | No pointer to `office.namespaces.md`, and no usings in the DI, DummyMailer and Identity blocks. The report's single MailKit using fails on the block's first line, `AddSendGrid`. `office.mail.examples.md` and `docs/mail/README.md` have the same gap. The verifier's group usings hid it (F8) | *2c* |
| G23 | Confirmed | `AccountControllerBase.cs:32-62` checks lockout, then the password, then answers `200`. In the probe an unconfirmed account signed in under `RequireConfirmedEmail = true` | *1b* |
| G24 | Confirmed, unsafe fix | `UserControllerBase.cs:42` creates the user and `:58` mails, so a failed mail throws past `if (result is OkResult)` (`security.instructions.md:1006`). The report's `finally` writes to existing accounts | *1c* |
| G25 | Confirmed | `ConfirmEmailInput.UserName` is `[Required]` and never read (`UserControllerBase.cs:71,83`). In the probe, no `userName` answered `400` and a wrong one confirmed the address. The front end has no confirm page, and `"users"` in `credentialUrls` does not cover `users/confirm-email` (`error-logging.ts:35-41`) | *2a*, *2d* |
| G26 | Confirmed, wrong fix | `AttachmentFileReactor` reacts to the `Attachment` only (`:63-68`). A reactor runs after the commit, with no navigations, once the links are gone by cascade, so "capture the links before the delete" cannot be done there. `RemoveItem` on the owner's write service can. The guide's sweep advice (`:1636,1641`) deletes too much (F13) | *2b* |
| G27 | Confirmed | `EntityEndpointMapper.cs:161-162` reads the link's mapping, else the base DTOs, on every mapped link route, the upload included. The overload `HasAttachments(…, a => a.UseMapping<…>())` compiles and works | *2b* |
| G28 | Confirmed | No writer-table row for a counter. The probe: `ExecuteUpdate` bumps it and leaves the `Guid` token, so an open edit is not made stale; but a bump during an edit is lost | *2b* |
| G29 | Confirmed, fix incomplete | Reactors only. A `[ServerOwned]` stamp through `IEntityService` is reverted (probe), "idempotent" overclaims, an exception escaping `ExecuteAsync` stops the host, and the alias `hosted-service` captures queries holding "service" | *2b* |
| G30 | Partly | Step 2's guard already covers creates (`original?.Status ?? Draft`), and `[ServerOwned]` on create is documented. Missing: naming a create-time field as a case of the guard | *2b* |
| G31 | Confirmed, wrong fix | `AccountLinks.cs:64-65` *sets* the query (`UriBuilder.Query = "?token=…"`): a page's own query is dropped, so "appended" is false | *2a*, *2d* |
| G32 | Confirmed, wrong fix | No pointer to *Linking a party to a user account*. The report's sentence contradicts the `DepartmentId` example right above it | *2a* |
| G33 | Confirmed, wider | `project.setup.md:204-211`, the rule lines at :38 and :312, and Templates 2–4 (:610, :679, :771) | *1d* |
| G34 | Confirmed | `auth/password/recover` is anonymous and mails per call (`PasswordControllerBase.cs:45-73`); rate-limit advice exists for `refresh-token` and sign-up only | *1e* |
| G35 | Partly | `entities.patterns.md:2079-2082` already keeps sensitive documents on the authenticated path. Missing: the `<img>` case. The Modules guides say "expose it anonymously" in three places | *2b*, *2d* |
| M13 | Confirmed, wrong fix | `FormOut` exposes no status (`form.ts:47-55`); a 409 lands as "Server error: …" for both kinds. The titles are `"Concurrency conflict"` (`EntityConcurrencyProblem.cs:18`) and `"Conflict"` (`EntityConstraintProblem.cs:17`). The report's reload targets the wrong component | *2d*, *3b* |
| M14 | Confirmed | By design (`details.ts:112-117`). Without the emit, a custom insert keeps editing the pre-save row: no token, and with a custom endpoint no id | *2d* |
| M15 | Confirmed | `LoginModal` declares four emits, `ForgotPasswordModal` none; `onClose` falls through `$attrs` to the injected modal, which declares `close`. The shell relies on it (`entities.shell.template.md:425-426`) | *2d*, *3c* |
| M16 | Confirmed, fix incomplete | `authData` is empty until `validateToken` resolves. The scaffolded views pass `{ immediate: false }`, so a default set only in the handler is lost on in-app navigation | *2d* |
| M17 | Confirmed, wrong fix | See *Recommendation*. Also, `CookieTokenManager` writes `document.cookie` from script, so it cannot be `HttpOnly` | *1e* |
| M18 | Confirmed | The folder is `kebab(pluralize(childName))` (`scaffold.mjs:882`), and a test pins it (`scaffold.test.js:178`). The report's `:216` is the `pluralize` helper | *2d* |
| G9 (basic) | Amended | No guide names overlaps. The basic report's text and today's guide both call `POST /list` a write; it is a batch read (F11) | *2b* |
| M8 (basic) | Amended | `formatters.instructions.md:73` is already the `v-html` home, and "sanitize" is the wrong tool for plain text | *2d* |

## Corrections

The report's exact changes that must not ship as written:

| ID | Report's change | Problem | Use instead |
|---|---|---|---|
| G22 | One MailKit using, SendGrid and MailGun in a comment; `Abstractions` in the DI block | The block calls `AddSendGrid` first, so its first line does not compile; the DI block never names `IMailService` | Three provider usings in the DI block; `Abstractions` in the DummyMailer and Identity blocks (*2c*) |
| G24 | `try { base.Create } finally { EnsureProfile }` | Runs after a `400 DuplicateUserName` too, so a role or domain row lands on an existing account | Record whether the name was free before the call; act only when the base returned `OkResult` or threw (*1c*) |
| G26 | A reactor captures the links before the delete | A reactor runs after the commit, without navigations, once the links are gone | Override `RemoveItem` on the owner's write service (*2b*) |
| G29 | Stamp a `[ServerOwned]` sent column; idempotent across restarts | Through `IEntityService` the stamp is reverted; send-then-stamp is at least once; an unhandled tick stops the host | Stamp through the `DbContext`, guard each tick, state the delivery guarantee (*2b*) |
| G31 | "with `?token=…` appended" | The server replaces the page's query | "with its query string set to `?token=…`" (*2a*) |
| G32 | Keep the link in the domain (an employee, a customer) | Contradicts the `DepartmentId` example above it; two app nouns | Separate a value of the account's own from the record it belongs to (*2a*) |
| G33 | A note after the snippet, "or drop `.AllowAnonymous()`" | The code stays wrong; with no fallback policy, dropping it changes nothing; Templates 2–4 missed | The code inside `if (app.Environment.IsDevelopment())`, in the section's snippet and Templates 2–4, and the two rule lines (*1d*) |
| M13 | Reload with `useDetails().load()`; wrap the pooled service's `save` | A second `useDetails()` in the form fetches an item the form does not show; a spread drops the class's methods; the failure banner stays up | Reload through `update:modelValue`, reset the feedback; with D14 no wrapper (*2d*) |
| M16 | Set the default inside `onAuthenticated` | The scaffolded views pass `{ immediate: false }`: in-app navigation never runs the handler | Set it in `setup` and in the handler (*2d*) |
| M17 | `MemoryTokenManager` does not expose it; pair it with refresh tokens | An injected script reads it from the running app; the front end never calls `auth/refresh-token` | The corrected warning (*1e*) |
| M8 | A separate `v-html` line beside the URL bullet | `:73` is already its home; a sanitiser keeps injected links and images in plain text | Amend `:73`: escape plain text, sanitise HTML (*2d*) |

Smaller errors, corrected in the steps:

- **G23:** the Modules pointer names no heading. The Modules sign-up recipe signs in right after the post (F2).
- **G27:** the section's example is `Product`, not `Order`. Every mapped link route is affected, the upload included, and the controller half is missing.
- **G28:** "`[ServerOwned]` untouched" reads as optional. It is required, and the lost bump goes unmentioned.
- **G30:** it prescribes, at step 2, the route `:886-888` calls second best.
- **G34:** the reason is missing, and so is "limit by client, not by account".
- **G35:** two homes for one recipe, and no revoke when the uri changes.
- **M14, M15:** two homes each.
- **M18:** the folder is the kebab-cased class plural, and the flag table in `entities.template.md` repeats the help.
- **G9:** `POST /list`. The conditional `ExecuteUpdate` belongs in the insert's transaction.
- **Line anchors:** G24's override is at :1001-1012, and G32's paragraph ends at :932.

## Found while verifying

| # | Finding | Where | Class | Step |
|---|---|---|---|---|
| F1 | Five more projects restore a vulnerable crypto package: MsgReader 6.1.4 brings Pkcs 10.0.12; Excel.NpoiMapper and PDF.PDFtoPrinter reference `System.Security.Cryptography.Xml` 10.0.12 directly; Syncfusion 35.1.39 brings Xml and Pkcs 10.0.11; Microsoft.Data.SqlClient 7.1.1 brings Pkcs 9.0.18 (net10.0) and 8.0.1 (net8.0) | `Mail.MSGReader`, `Excel.NpoiMapper`, `PDF.PDFtoPrinter`, `Word.Syncfusion`, `DAL.SqlServer` csproj files | Vulnerable dependency | *1a* |
| F2 | The sign-up recipe signs in right after the post. Under a required confirmation that sign-in is refused and the catch shows "Sign-up failed" for an account it created | Modules `auth.instructions.md:338-339` | Wrong guide | *1b* |
| F3 | `PasswordControllerBase<TUser>` requires `TUser : IdentityUser`; the other two bases, the guide (:943) and the docs (:27) say `IdentityUser<string>` | `PasswordControllerBase.cs:14` | Contract mismatch | *3d* |
| F4 | The confirm-email body reads `{ token, userName }` in the guide and `{ token, userName, password? }` in the docs | `security.instructions.md:1051`, `docs/controllers.md:97` | Inconsistent docs | *2a*, *3a* |
| F5 | The table of endpoints that opt out with `[AllowAnonymous]` leaves out `POST auth/refresh-token` | `docs/controllers.md:58-65`, `AccountControllerBase.cs:112` | Missing docs | *2a* |
| F6 | "appended" in four more places | Modules `auth.instructions.md:285`, `auth.examples.md:116`, `entities.shell.template.md:880`, `_template/app-shell/src/components/users/ForgotPasswordForm.vue:9` | Wrong wording | *2d* |
| F7 | The security guide presents refresh tokens as closing the gap every SPA hits; `@regira/modules` stores one token and never calls `auth/refresh-token` | `security.instructions.md:352-355`, Modules `auth-service.ts:58-78` | Misleading guide | *1e* |
| F8 | GuideVerifier's `office-mail` group prepends the mail namespaces to every block, which is why the DummyMailer and Identity blocks compiled without their usings. Its README asks for SDK-implicit usings only; `office-data`, `office-pdf`, `office-excel` and `office-word` do the same | `tools/GuideVerifier/projects.json` | Test gap | *2c* |
| F9 | Missing usings beyond the report: the DummyMailer block, `office.mail.examples.md` (:7, :56-57), `docs/mail/README.md` *Quick Start* (:40-58) | as listed | Missing usings | *2c* |
| F10 | The OpenAPI block lacks the usings for `ScalarAuthenticationOptions`, `ApiKeyDefaults` and `JwtBearerDefaults` | `project.setup.md:192` | Missing usings | *1d* |
| F11 | `POST /list` is called a write; it sends a `ListQuery` | `entities.instructions.md:496-497`, `EntityEndpointMapper.cs:245-252` | Wrong guide | *2b* |
| F12 | A `[ServerOwned]` stamp written through `IEntityService.Modify` is reverted; through the `DbContext` it is kept | probe | Trap | *2b* (G29) |
| F13 | Step 6 recommends sweeping every `Attachment` row no link names. That also takes what `HasStrictRelation = false` keeps and what another owner type links | `entities.instructions.md:1636,1641`, `docs/attachments.md:244-246,261-264` | Wrong guide | *2b* (G26) |
| F14 | "An `onMounted` fetch runs before the stored token is validated and 401s": the bearer header is attached before validation (`plugin.ts:83` vs `:100`), and `rehydration-ordering.test.js:101` pins it. With a valid stored token, the cost is a second request | Modules `auth.instructions.md:97-100`, `onAuthenticated.ts:23-24` | Wrong guide | *2d* |
| F15 | `new MemoryTokenManager()` fails with TS2554; the instructions table shows `token?` | Modules `auth.instructions.md:63`, `token-manager.ts:33` | Wrong guide | *1e* |
| F16 | `ForgotPasswordFormEmits` says its payloads are `username`; `fail` carries `ex?` | Modules `auth.signatures.md:282`, `useForgotPasswordForm.ts:7` | Wrong guide | *2d* |
| F17 | The shell's `LoginModal` has no `@close`, so its header X does nothing | Modules `entities.shell.template.md:419-426` | Dead button | *Not proposed* |
| F18 | `--owns Detail` collides with the slice's own `details/`: `Entity.ts` imports a folder with no barrel (vue-tsc fails), and the suggested `--overwrite-slice` `rmSync`s `Details.vue` and `Form.vue`. Today only a class named `Detail` hits it | Modules `_template/scaffold.mjs:882,887-893` | Lost content, rare | *1f* |
| F19 | The 409 table says "for a save and for a delete alike"; restore ("Restoring failed: …") is not listed | Modules `entities.patterns.md:1031` | Incomplete guide | *2d* |
| F20 | "The chips have nowhere to put the scalars": `InputSelectorInline` has a `#chip` slot that receives `row` | Modules `entities.patterns.md:856-857`, `form/InputSelectorInline.vue:6` | Overstated guide | *2d* |

## 1. Critical and security

### 1a. Vulnerable crypto packages (G8, D2, F1)

**Mechanism.** The advisories GHSA-846f-f764-76mx (High) and GHSA-2fmv-rv2c-59x7 (Moderate) cover `System.Security.Cryptography.Pkcs` [10.0.0, 10.0.12], [9.0.0, 9.0.20] and [8.0.0, 8.0.1]. `System.Security.Cryptography.Xml` 10.0.12 carries three High advisories of its own (GHSA-r955-8q5j-3h83, GHSA-7hqp-7c5m-4vq6, GHSA-c399-9853-6jmj). No central package management exists, so each project pins its own.

| Project | Restores today | Change |
|---|---|---|
| `Mail.MailKit` | MailKit 4.18.1 → MimeKit → Pkcs 10.0.0 | Add `<PackageReference Include="System.Security.Cryptography.Pkcs" Version="10.0.13" />` beside MailKit (:25), with the comment `<!-- MimeKit 4.18.1 floors System.Security.Cryptography.Pkcs at 10.0.0 (GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7) -->` |
| `Mail.MSGReader` | MsgReader 6.1.4 → Pkcs 10.0.12 (6.2.0 still asks 10.0.12) | The Pkcs line, with a comment naming MsgReader instead of MimeKit |
| `Excel.NpoiMapper` | Xml 10.0.12 (:20) → Pkcs 10.0.12 | Raise Xml to 10.0.13 (it brings Pkcs 10.0.13) |
| `PDF.PDFtoPrinter` | Xml 10.0.12 (:20) | Raise Xml to 10.0.13; it lifts Windows.Compatibility's Pkcs 9.0.2 too |
| `Word.Syncfusion` | Syncfusion 35.1.39 → Xml 10.0.11 → Pkcs 10.0.11 | Add `System.Security.Cryptography.Xml` 10.0.13 |
| `DAL.SqlServer` | Microsoft.Data.SqlClient 7.1.1 (latest) → Pkcs 9.0.18 / 8.0.1 | The Pkcs line, with a comment naming Microsoft.Data.SqlClient |

The MailKit, MSGReader and Xml chains were probed: each restores clean once pinned. Syncfusion's was read from its restore output only. `Excel.EPPlus` restores Pkcs 4.7.0, outside the ranges. NpoiMapper's `<!-- Upgraded for vulnerabilities -->` group is the precedent for the comment. ImageSharp's advisories have a worktree of their own (`claude/imagesharp-advisories`).

The basic report's G8 guide line ("Reference … 10.0.13 or later directly") then drops: it said "not needed if D2 lands".

Packages: the six above, **Security** changelog bullets.

### 1b. A confirmed address before sign-in (G23, D11, F2)

With decision 1 (a):

1. **Test first** in `tests/Web.Security.Testing/IdentityControllerTests.cs`:
   - Unconfirmed user, `RequireConfirmedEmail = true`, right password: `401` with `isNotAllowed: true`. On `wip` it answers `200`.
   - Wrong password: `401` without the flag, failed count 1.
   - Option off: `200`, as today.
2. **`AccountControllerBase.Authenticate`**: after the password check, when `userManager.Options.SignIn.RequireConfirmedEmail` is set and `IsEmailConfirmedAsync` is false, answer `StatusCode(401, CreateFailedResponse(false) with { IsNotAllowed = true })`. A `with` expression keeps the protected `CreateFailedResponse` signature, so a subclass compiled against 6.5 does not hit `MissingMethodException`. Checking after the password keeps the base's order: a locked-out account still gets the lockout answer, a wrong password still counts, and only a caller who knows the password learns why.
3. **`AuthenticateResponseDto`** (a record): `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public bool? IsNotAllowed { get; init; }`, so existing `401` bodies stay as they are.
4. **Guide** (`security.instructions.md`, the `AccountControllerBase<TUser>` failure cell, :1026): "An account whose address is not confirmed answers `401 { isNotAllowed: true }` once its password checks, when `SignIn.RequireConfirmedEmail` is set. A user you seed, or create through `POST users` without `confirmEmailUrl`, then needs `EmailConfirmed = true`, or it can never sign in." Same sentence in `docs/controllers.md` (:71, :80).
5. **Modules** `auth.instructions.md`, the bullet starting "To have the API mail a confirmation link" (:354-356), becomes:
   > - To have the API mail a confirmation link, post `confirmEmailUrl` too: the absolute URL of the app's confirm page (_Confirm page_ below), on an origin the API lists (`Regira.Security` → security.instructions → _Mailed links: allowed origins_). Sign-in does not wait for the confirmation unless the API sets `SignIn.RequireConfirmedEmail`. Then drop the `login` call after the post and tell the visitor to follow the mailed link: signing in answers `401` with `isNotAllowed` until they do, and the stock `LoginForm` shows only its generic failure.

`SignInManager.CanSignInAsync` would cover the phone and account flags too, but the base takes no `SignInManager`, and adding one changes its constructor. Not proposed.

With decision 1 (b), the guide gets a heading of its own, `### Requiring a confirmed address`, after the paragraph ending "failure increments it (Identity lockout)." (:1034). The Modules bullet in step 5 then reads "…unless the API requires it (`Regira.Security` → security.instructions → _Requiring a confirmed address_)" and "signing in answers `403` until they do". The section:

````markdown
### Requiring a confirmed address

⚠️ `POST auth` checks the lockout and the password only: it ignores `SignIn.RequireConfirmedEmail`, so an account
whose address was never confirmed signs in. To require the confirmation, override `Authenticate` and refuse such an
account before the base runs. Check lockout first and the password last, as the base orders them: a locked-out
account still gets the lockout answer, a wrong password still counts toward lockout (in the base), and only a
caller who knows the password learns why it was refused.

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Regira.Security.Authentication.Jwt.Abstraction;    // ITokenHelper
using Regira.Security.Authentication.Web.Controllers;
using Regira.Security.Authentication.Web.Models;          // AuthenticateInput

public class AccountController : AccountControllerBase<AppUser>
{
    private readonly UserManager<AppUser> _userManager;

    public AccountController(ITokenHelper tokenHelper, UserManager<AppUser> userManager,
        IUserClaimsPrincipalFactory<AppUser> claimsFactory, ILogger<AccountController> logger)
        : base(tokenHelper, userManager, claimsFactory, logger) => _userManager = userManager;

    // [AllowAnonymous] and the route come with the base action
    public override async Task<IActionResult> Authenticate([FromBody] AuthenticateInput model, [FromQuery] string clientApp)
    {
        var user = await _userManager.FindByNameAsync(model.Username);
        if (user is { EmailConfirmed: false } && _userManager.Options.SignIn.RequireConfirmedEmail
            && !await _userManager.IsLockedOutAsync(user)
            && await _userManager.CheckPasswordAsync(user, model.Password))
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Email address not confirmed");
        }
        return await base.Authenticate(model, clientApp);
    }
}

public class AppUser : IdentityUser;   // stands for your app's own Identity user — don't declare a second one
```

Set `o.SignIn.RequireConfirmedEmail = true` in `AddIdentityCore`. A user you seed, or create through `POST users`
without `confirmEmailUrl`, needs `EmailConfirmed = true`, or it can never sign in.
````

The probe ran it: unconfirmed with the right password `403`, with a wrong one `401` and a failed count of 1, confirmed `200`, locked out `401 {isLockedOut:true}`.

Packages: `Regira.Security.Authentication.Web` (code under (a), **behaviour change:** for apps that set the option), `Regira.Security` (guide).

### 1c. The role a sign-up assigns (G24)

1. **`security.instructions.md`**: replace the block at :985-1016 (`using Microsoft.AspNetCore.Authorization;` … `public class UsersController : UserControllerBase<AppUser>`). It compiles under the verifier's conventions, so it keeps no marker:

   ```csharp
   using Microsoft.AspNetCore.Authorization;
   using Microsoft.AspNetCore.Identity;
   using Microsoft.AspNetCore.Identity.UI.Services;          // IEmailSender
   using Microsoft.AspNetCore.Mvc;
   using Regira.Security.Authentication.Web.Controllers;
   using Regira.Security.Authentication.Web.Models;          // UserInput

   public class UsersController : UserControllerBase<AppUser>
   {
       // An explicit constructor with a field of its own: a primary-constructor parameter used in the body AND passed
       // to the base is captured twice (warning CS9107), and the base keeps its copy private.
       private readonly UserManager<AppUser> _userManager;

       public UsersController(UserManager<AppUser> userManager) : base(userManager) => _userManager = userManager;

       [AllowAnonymous]
       public override async Task<IActionResult> Create(UserInput model, [FromServices] IEmailSender mailer)
       {
           // The base creates the account before it mails the link, so a failed mail throws past any check on the
           // result. Assign in finally, and only to an account this call created: a taken name answers 400.
           var isNew = await _userManager.FindByNameAsync(model.Username) == null;
           IActionResult? result = null;
           try
           {
               result = await base.Create(model, mailer);
               return result;
           }
           finally
           {
               if (isNew && (result is null || result is OkResult))
               {
                   await EnsureRole(model.Username);
               }
           }
       }

       // adds only what is missing
       private async Task EnsureRole(string username)
       {
           var user = await _userManager.FindByNameAsync(username);   // null: the base refused the input
           if (user != null && !await _userManager.IsInRoleAsync(user, "Customer"))
           {
               await _userManager.AddToRoleAsync(user, "Customer");
           }
       }
   }

   public class AppUser : IdentityUser;   // stands for your app's own Identity user — don't declare a second one
   ```

2. **A new `### Sign-up with profile fields`** after the paragraph ending "rather than reading it from the payload." (:1020):

   ````markdown
   ### Sign-up with profile fields

   `Create` binds `UserInput` only. A sign-up that also takes profile fields gets an action of its own with its own
   input: it hands the account part to `base.Create` and adds the rest in the same guarded `finally`, while `POST users`
   stays guarded. The SPA posts to `users/sign-up` and lists it in its `credentialUrls`.

   <!-- no-compile -->
   ```csharp
   public record SignUpInput
   {
       [Required] public string Username { get; set; } = null!;
       [Required] public string Password { get; set; } = null!;
       public string? ConfirmEmailUrl { get; set; }
       [Required, MaxLength(100)] public string DisplayName { get; set; } = null!;
   }

   // in a UsersController whose Create stays guarded (no anonymous override of it), with _userManager and
   // _db (the app's DbContext) injected; Profile is the app's own row
   [AllowAnonymous]
   [HttpPost("sign-up")]
   public async Task<IActionResult> SignUp(SignUpInput input, [FromServices] IEmailSender mailer)
   {
       var isNew = await _userManager.FindByNameAsync(input.Username) == null;
       IActionResult? result = null;
       try
       {
           result = await base.Create(new UserInput
           {
               Username = input.Username,
               Password = input.Password,
               ConfirmEmailUrl = input.ConfirmEmailUrl
           }, mailer);
           return result;
       }
       finally
       {
           if (isNew && (result is null || result is OkResult))
           {
               await EnsureProfile(input);
           }
       }
   }

   // adds only what is missing
   private async Task EnsureProfile(SignUpInput input)
   {
       var user = await _userManager.FindByNameAsync(input.Username);
       if (user == null)
       {
           return;
       }
       if (!await _userManager.IsInRoleAsync(user, "Customer"))
       {
           await _userManager.AddToRoleAsync(user, "Customer");
       }
       if (!await _db.Profiles.AnyAsync(p => p.UserId == user.Id))
       {
           _db.Profiles.Add(new Profile { UserId = user.Id, DisplayName = input.DisplayName });
           await _db.SaveChangesAsync();
       }
   }
   ```

   A domain row that is the account's party is linked as `Regira.Entities` → `entities.blueprints` → *Linking a party to
   a user account* describes.
   ````

   In the probe, the role and the row survived a failed mail, a taken admin name was left alone, a missing `DisplayName` answered `400`, and `POST users` still answered `401`.
3. **`docs/controllers.md:99`**: "so an override that assigns a role or tenant after the base call acts only on an account it created" becomes "An override that assigns a role or tenant does so only for an account this call created. A failed confirmation mail throws after the account exists, so it assigns in a `finally`, guarded by a check that the name was free before the call."
4. **Modules** `auth.instructions.md`, *Sign-up*, after the bullet at :347-348: "- **An API whose sign-up takes profile fields** has an action of its own (`users/sign-up`). Post there with the fields, and add that path to `credentialUrls` too."

Package: `Regira.Security` (guide), `Regira.Security.Authentication.Web` (docs).

### 1d. OpenAPI and Scalar in Development only (G33, F10)

The rule's one home is *OpenAPI & Scalar UI* in `src/Common.Setup/ai/project.setup.md`:

1. :183 "All API templates expose:" becomes "All API templates expose, in Development only:".
2. After the Swashbuckle paragraph (:187): "Map both inside `if (app.Environment.IsDevelopment())`. The document lists every endpoint and its schemas, the guarded ones included. `.AllowAnonymous()` only exempts the two endpoints from a fallback policy: it does not guard them, and `MapControllers().RequireAuthorization()` never covers them."
3. Replace the block at :192-212:

   ```csharp
   using Microsoft.AspNetCore.Authentication.JwtBearer;            // JwtBearerDefaults
   using Regira.Security.Authentication.ApiKey.Models;             // ApiKeyDefaults
   using Regira.Security.Authentication.Web.OpenApi.Transformers;
   using Scalar.AspNetCore;                                        // ScalarAuthenticationOptions

   builder.Services.AddOpenApi(options =>
   {
       // declares every registered scheme — API key, JWT, cookie, OpenID Connect — from the descriptor
       // each Add…Authentication contributes, so adding a scheme needs no change here
       options.AddDocumentTransformer<AuthenticationSchemeDocumentTransformer>();
       // declares WHICH operations need one — without it every endpoint reads as anonymous
       options.AddOperationTransformer<SecurityRequirementOperationTransformer>();
   });

   if (app.Environment.IsDevelopment())
   {
       app.MapOpenApi().AllowAnonymous();
       app.MapScalarApiReference(options =>
       {
           options.Authentication = new ScalarAuthenticationOptions
           {
               PreferredSecuritySchemes = [ApiKeyDefaults.AuthenticationScheme, JwtBearerDefaults.AuthenticationScheme]
           };
       }).AllowAnonymous();
   }
   ```

4. Template 4's `Program.cs` (:771-782) and Templates 2 and 3 (:610-611, :679-680): wrap the mapping the same way, as `dotnet new webapi` does.
5. :312 becomes "- OpenAPI / Scalar endpoints are mapped in Development only and marked `.AllowAnonymous()` (*OpenAPI & Scalar UI*)." :38 "and `.AllowAnonymous()` on the OpenAPI/Scalar endpoints" becomes "and the OpenAPI/Scalar endpoints mapped in Development only, with `.AllowAnonymous()`".

The launch profile (:244) sets `ASPNETCORE_ENVIRONMENT=Development`, so `launchUrl: scalar` keeps working. The probe built the block with 0 warnings on Microsoft.AspNetCore.OpenApi 10.0.13 and Scalar.AspNetCore 2.18.0.

Package: `Regira.Setup` (guide); `project.setup.md` sits in the verifier's `security` group.

### 1e. Password recovery and token storage (G34, M17, F7, F15)

- **G34**, appended at `security.instructions.md:1044` after the G31 rewording: "It is anonymous and mails the named account on every call, so a caller can flood that inbox and spend the app's mail quota: rate-limit it like `refresh-token`. Limit by client, not by account, so a `429` reveals no more than the `200` does." `docs/controllers.md` *Security notes* (:174-179) gains: "- **Rate-limit the anonymous endpoints that cost something:** `auth/refresh-token` (its token is the credential) and `auth/password/recover` (it mails the named account on every call)."
- **M17**, Modules `auth.instructions.md`, `## Token storage`:
  - "Three implementations, all keyed by `prefix + "auth:token"`:" (:56-57) becomes "Three implementations; the two persistent ones are keyed by `prefix + "auth:token"`:".
  - The row `| MemoryTokenManager(token?) |` (:63) becomes `| MemoryTokenManager(token) | in-memory (cleared on reload; pass undefined to start signed out) |` (F15).
  - After the table, before "The bearer interceptor reads `tokenManager.token`…" (:65):
    > ⚠️ **No manager hides the token from script.** `LocalStorageTokenManager` survives reloads and new tabs, so a script injected into the origin (XSS) can read it at any time until it expires. `CookieTokenManager` is no safer: a cookie written by script cannot be `HttpOnly`. `MemoryTokenManager` leaves nothing at rest, at the price of a sign-in on every reload and new tab, but an injected script can still read it from the running app. This module keeps a single token and never calls the API's `auth/refresh-token`, so a short access-token lifetime means signing in again when it runs out (`auth/refresh` renews only a token that is still valid). Keep script injection out: no `v-html` on user content, and a Content-Security-Policy.
- **F7**, `security.instructions.md:352-355`, after the refresh-token sentence: "`@regira/modules` keeps one token and does not call `auth/refresh-token`; an SPA on it renews through `auth/refresh` while the token is valid."

Packages: `Regira.Security` (guide), `Regira.Security.Authentication.Web` (docs); Modules: `vue/auth` (guide).

### 1f. A sub-slice folder that is one of the slice's own (F18)

Test first in `scaffold.test.js`: `--owns Detail` stops with a message and leaves `details/Details.vue` in place. Then `scaffold.mjs` refuses a sub-slice folder that matches an entry of the slice template (`readdirSync(srcRoot)`), before anything is written, and names the clash: the child class needs another name, since `--as` does not move the folder (decision 5). Modules: Scaffold.

## 2. The easy fixes, one batch per area

A `>` line quotes the new text. Where the target already sits inside a blockquote (G27, G28, G26's snippet, G35), the text keeps its `> ` prefix; elsewhere it goes in as a plain paragraph or bullet.

### 2a. Security guides, docs and XML docs (`src/Common.Security`, `Security.Authentication.Web`)

- **G31**, anchored on the quoted phrases:
  - `security.instructions.md:1044`: "with a Base64 `token` (reset token + username) as its query string, percent-encoded, so" becomes "with its query string set to `?token=<Base64 of the reset token and user name>` (a query `siteUrl` carried is dropped), percent-encoded so".
  - `:1053`: "with a Base64 `token` as its query string, percent-encoded like the reset link's" becomes "with its query string set to `?token=<Base64 of the confirmation token and user name>`, percent-encoded like the reset link's".
  - `docs/controllers.md:90`: "with its query string set to `?token=…`, percent-encoded so any query parser (`URLSearchParams`, vue-router's `route.query`) reads it back intact; a query `siteUrl` carried is dropped. The token is a Base64 payload of the Identity reset token plus the username." `:99` likewise for the confirmation token; `:107-108` "with the token as its query string" becomes "with `?token=…` as its query string".
  - XML docs: `PasswordControllerBase.cs:37` ("… with its query string set to <c>?token=…</c>, the percent-encoded Base64 of the reset token and user name, under a subject…"), `UserControllerBase.cs:18-19`, `UserInput.cs:13` ("mailed that page with its query string set to <c>?token=…</c>;"), `RecoverPasswordInput.cs:10-11` ("the link replaces its query string with <c>?token=…</c>.").
- **G25**, appended at `security.instructions.md:1053` after G31, with decision 2 (a): "The confirm page posts `{ token }` to `users/confirm-email`: the link's `token` query value as the parser decoded it. The server reads the user from the token; `userName` is accepted and not read." Under 2 (b): "…posts `{ token, userName }` … `userName` only satisfies validation (without it the answer is `400`): send the name the token carries, `JSON.parse(atob(token)).Username`." `docs/controllers.md:99` gets the matching clause.
- **F4**: the guide's table (:1051) and the docs (:97) list the same body: `{ token }` under decision 2 (a), with *3a*; `{ token, userName }` under 2 (b). The docs add that `password` is accepted for overrides.
- **F5**: `docs/controllers.md:58-65` gains the row "`POST auth/refresh-token` | the refresh token is the credential; `404` unless refresh tokens are registered".
- **G32**, a new paragraph after "Like a role, a changed value reaches the SPA on the next refresh." (:932):
  > A value of the account's own, like `DepartmentId`, can live on `AppUser`. The domain record the account belongs to (its party) is linked from the domain instead: `Regira.Entities` → `entities.blueprints` → *Linking a party to a user account*. The factory looks that link up. It is registered scoped, so it can take the app's `DbContext` (`db.Set<PartyUser>().Where(x => x.UserId == user.Id).Select(x => (int?)x.PartyId).FirstOrDefaultAsync()`), and it adds the claim only when a link exists.

  The blueprint names no `DbSet`, hence `Set<PartyUser>()`; `AddClaimsPrincipalFactory` registers the factory scoped.

Packages: `Regira.Security` (guide), `Regira.Security.Authentication.Web` (docs, XML docs). Run the verifier's `security` group.

### 2b. Entities guides (`src/Common.Entities/ai`)

- **G26**, `entities.instructions.md`, *Attachments*:
  1. Step 6 (:1636): "…sweeps it against the stored `Path` values, as it does for the attachments of a deleted owner:" ends at "…sweeps it against the stored `Path` values:" (F13).
  2. The bullet `- **Deleting the owner leaves its attachments, by design.**` (:1641) becomes: "**Deleting the owner leaves its attachments, by design.** Its links go with it, but the `Attachment` rows and their files stay: an attachment can be linked from more than one entity, so the library does not decide that it is no longer needed. To delete them with the owner, mark them in its write service: *Deleting an owner's attachments with it* below."
  3. Before `> ⚠️ **A link rule belongs in the owner's validator too.**` (:1644):

     ````markdown
     > **Deleting an owner's attachments with it.** Not in a reactor: it runs after the commit, when the links are
     > gone, and its change carries no navigations. Override `RemoveItem` on the owner's write service — it runs once the
     > validators passed — and mark each attachment no other link names, so it goes in the owner's own save and
     > `AttachmentFileReactor` removes its file after the commit:
     > <!-- no-compile -->
     > ```csharp
     > // Regira.Entities.EFcore.Services, Regira.Entities.Preppers.Abstractions, Regira.Entities.Validators.Abstractions
     > public class ProductWriteService(AppDbContext dbContext, IEntityReadService<Product, int> readService,
     >     IEnumerable<IEntityPrepper> preppers, IEnumerable<IEntityValidator> validators, ILoggerFactory? loggerFactory = null)
     >     : EntityWriteService<AppDbContext, Product>(dbContext, readService, preppers, validators, loggerFactory)
     > {
     >     protected override async Task RemoveItem(Product item, CancellationToken token = default)
     >     {
     >         await base.RemoveItem(item, token);              // first: it attaches a loaded graph, which the query below reuses
     >         if (item is IArchivable) return;                 // a soft delete keeps them for a restore
     >         var links = await DbContext.Set<ProductAttachment>().AsTracking().Include(x => x.Attachment)
     >             .Where(x => x.ObjectId == item.Id).ToListAsync(token);
     >         foreach (var link in links)
     >         {
     >             DbContext.Remove(link);
     >             var linkedElsewhere = await DbContext.Set<ProductAttachment>()
     >                 .AnyAsync(x => x.AttachmentId == link.AttachmentId && x.ObjectId != item.Id, token);
     >             if (!linkedElsewhere && link.Attachment != null)
     >             {
     >                 DbContext.Remove(link.Attachment);       // AttachmentFileReactor removes its file after the commit
     >             }
     >         }
     >     }
     > }
     > // .For<Product>(e => e.UseWriteService<ProductWriteService>().HasAttachments<AppDbContext, Product, ProductAttachment>(x => x.Attachments))
     > ```
     > It reads this owner type's links only, as the library does when a link goes. The file sweep of step 6 stays for what
     > a failed save or a rollback leaves; deleting every `Attachment` row no link names instead also takes the ones
     > `HasStrictRelation = false` keeps, and treats as orphaned whatever another owner type links.
     ````

     The probe removed rows and files for a loaded-graph delete and a stub delete, and kept an attachment another link named.
  4. Mirror 1 and 2 in `docs/attachments.md` (:244-246, :261-264), its pointer naming the guide: `ai/entities.instructions.md` → *Deleting an owner's attachments with it*. The snippet keeps one home.
- **G27**, the blockquote `> **Ordering & extending the DTOs.**` (:1726), after "…by the `EntityAttachmentDto` base type)." (:1733):
  > A derived pair is registered on the link, through `configure`: `e.HasAttachments<AppDbContext, Product, ProductAttachment>(x => x.Attachments, a => a.UseMapping<ProductAttachmentDto, ProductAttachmentInputDto>())` (the read DTO derives from `EntityAttachmentDto`). The attachment routes mapped under the owner read and bind the link's mapping — without it the base DTOs, which drop the derived fields; an attachment controller names its pair instead (`EntityAttachmentControllerBase<ProductAttachment, ProductAttachmentDto, ProductAttachmentInputDto>`). The owner's DTOs carry the same types (step 3).
- **G28**, a row after "Only a gated domain action may change it" in the writer table (:405):
  > | A counter a public action bumps (views, likes) | `[ServerOwned]`, so an edit keeps it, and the bump as one statement on the `DbContext`: `await db.Products.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ViewCount, x => x.ViewCount + 1), token)` — atomic; no prepper, primer or reactor runs, so the concurrency token stays and an open edit is not made stale. Repeat the read's scope in the `Where` (global filters do not run on `db`). An edit writes every column, so a bump during one is lost: a count that must be exact goes on a row of its own |

  Not in the similar table at `entities.patterns.md:1747-1752`, which sits inside the `server-owned-fields` recipe body.
- **G29**, `entities.patterns.md`, inside the `after-commit` recipe body, after "…action: the other paths to the same state would skip it." (:236), before `**See:**` (:238):

  ````markdown
  **Not for a time-based trigger** (a reminder N minutes before a start): a reactor fires on a commit, never at a
  moment. Poll from a hosted service: a `BackgroundService` with a `PeriodicTimer`, whose every tick opens a scope of
  its own (`CreateAsyncScope()`, so a fresh `DbContext`), queries what is due, sends, and stamps a sent column per row.
  Declare that column `[ServerOwned]`, so a client's edit cannot clear it and send again, and stamp it through the
  `DbContext`: on the `IEntityService` path the guard restores it.

  <!-- no-compile -->
  ```csharp
  public class DueNoticeJob(IServiceScopeFactory scopes, ILogger<DueNoticeJob> logger) : BackgroundService
  {
      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
          while (await timer.WaitForNextTickAsync(stoppingToken))
          {
              try
              {
                  await using var scope = scopes.CreateAsyncScope();                 // a fresh DbContext per tick
                  var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                  var mailer = scope.ServiceProvider.GetRequiredService<IOrderMailer>();
                  var now = DateTime.UtcNow;
                  var due = await db.Orders.Where(x => x.NoticeSentAt == null && x.NoticeDueAt <= now)
                      .Select(x => x.Id).ToListAsync(stoppingToken);
                  foreach (var id in due)
                  {
                      await mailer.SendNotice(id, stoppingToken);
                      await db.Orders.Where(x => x.Id == id)                         // the DbContext: IEntityService would restore a [ServerOwned] column
                          .ExecuteUpdateAsync(s => s.SetProperty(x => x.NoticeSentAt, DateTime.UtcNow), stoppingToken);
                  }
              }
              catch (Exception ex) when (ex is not OperationCanceledException)
              {
                  logger.LogError(ex, "A due-notice tick failed");                   // left unhandled, it stops the host
              }
          }
      }
  }
  // builder.Services.AddHostedService<DueNoticeJob>();
  ```

  Stamped after the send, a crash between the two sends that row again; stamp first instead — the update filtered on
  `NoticeSentAt == null` and reporting 1 row — for at most once, or when several instances run the job.
  ````

  The marker (:218) appends `schedule,scheduled,reminder,reminders,hosted,hostedservice,backgroundservice,periodic,periodictimer,timer`, not `hosted-service`: a replay of the ranking showed `hosted-service` turns "how do I inject a service into a reactor" from `registered-service` to `after-commit`, and this set flips no existing query. The documentation server keeps a word-for-word copy of this recipe, so its side lands in the same pass. Optional: a pointer under *How reactors run* in `entities.instructions.md`, "Not at a moment in time: entities.patterns → *React to a committed change*."
- **G30**, `entities.patterns.md`, *Role-gated transitions* (no recipe body, so no copy moves):
  - After "`Modify` would be reverted too." (:746), inside step 2: "A field only a trusted writer may set **on create** (a series id) takes the same guard: clear it on a create unless the flag is set, and restore it from `original` on every update. `[ServerOwned]` is no substitute here either: on create it keeps whatever the client sent."
  - The bullet "The other route — the fields kept on `TInputDto`" (:886-888) becomes: "The other route — the fields kept on `TInputDto` behind the guard prepper, the action flipping the flag and sending a `PatchCommand` or, for a create-time field, a `SaveCommand` — works too, but puts the fields back on the input DTO every caller binds, where the guard alone keeps a caller from setting them. Prefer the request."
  - Optional: `entities.instructions.md:405` "may change it" becomes "may set or change it".
- **G35**, the Packages half: `entities.instructions.md:1735-1737` becomes:
  > **Images in `<img>` tags 401 on a secured API** — the browser sends no `Authorization` header for `src` URLs. A public file: expose the download anonymously (uploads/deletes stay guarded): [`entities.patterns.md`](./entities.patterns.md) → Public (anonymous) attachment downloads. A file that must stay private keeps the guarded download, and the SPA shows it from an object URL of an authenticated download (`regira_modules.vue.entities` → patterns → *Attachments (files)*).

  Optional: `entities.patterns.md:2081` adds ", and show an image from an object URL" after "which sends the bearer".
- **G9**, supersedes the basic report's text. Anchor `⚠️ **A prepper enforcing a cap must count the change tracker's pending rows too.**` (:492); replace from "This is not a seeding-only concern:" (:496-497) with:
  > This is not a seeding-only concern: a batch import, or an endpoint of the app's own that adds several rows before one `SaveChanges()`, has the same shape. Two concurrent requests do not: each has its own change tracker. A hard cap needs the database to arbitrate: a counter on the capacity row with `IHasConcurrencyToken` (409 to the loser), or a conditional `ExecuteUpdate … Where(x => x.Taken < x.Capacity)` in the insert's transaction, where 0 rows affected is reported as a 400. An overlap rule (no two bookings of one room overlap) is check-then-insert too, with no counter to guard: in one transaction, first update the resource's own row (an `ExecuteUpdate` of a version column), whose lock holds until the commit — SQLite locks the database for the write — so a second request waits and its check sees the first row; on PostgreSQL an exclusion constraint can refuse the overlap itself (`EXCLUDE USING gist (room_id WITH =, tstzrange(starts_at, ends_at) WITH &&)`, extension `btree_gist`), answered 409 like any constraint. A `Serializable` transaction alone fails the loser with a deadlock (SQL Server) or a serialization error (PostgreSQL), to retry.

  SQLSTATE 23P01 is in class 23, which `DbUpdateExceptionExtensions.cs:34` maps to the constraint 409.

Package: `Regira.Entities` (guides, docs). Run the verifier's `entities` group.

### 2c. Office guides (`src/Common.Office`)

- **G22**, `office.mail.instructions.md`:
  1. *Module Context*: after the row `` | `Regira.Office.Mail` | Email sending, … `` (:11), before `**Related:**` (:13): "Exact usings: `office.namespaces.md`." (the PDF guide's wording, :13).
  2. *DI Registration*: open the block (:164) with:

     ```csharp
     using Regira.Office.Mail.SendGrid.DependencyInjection;   // AddSendGrid
     using Regira.Office.Mail.MailGun.DependencyInjection;    // AddMailGun
     using Regira.Office.Mail.MailKit.DependencyInjection;    // AddMailKit
     // keep the one for the provider you register; inject IMailService (Regira.Office.Mail.Abstractions)
     ```

     The block keeps its `<!-- no-compile -->` marker (:163) only if it still fails to compile with these usings.

  3. *Testing — `DummyMailer`* (:238), above `services.AddSingleton<IMailService, DummyMailer>();`: `using Regira.Office.Mail.Abstractions;   // IMailService` and `using Regira.Office.Mail.Services;       // DummyMailer` (F9).
  4. *ASP.NET Identity Integration*, between the two usings (:275-276): `using Regira.Office.Mail.Abstractions;                    // IMailService`.
- **F9**: `office.mail.examples.md` *DI Registration* (:7) gains the SendGrid using, its Identity block (:56-57) the `Abstractions` one; `docs/mail/README.md` *Quick Start* (:40-58) the same usings.
- **The pointer line** after each Module Context row: `office.csv.instructions.md` (after :11), `office.excel.instructions.md` (after :11), `office.word.instructions.md` (after :12), `office.barcodes.instructions.md` (after :11, before the `> **A QR code *is* a barcode**` note), `office.ocr.instructions.md` (after :11) and `office.vcards.instructions.md` (after :11, before `---`).
- **F8**, with decision 6 (a): the `office-mail` group in `tools/GuideVerifier/projects.json` keeps the SDK-implicit usings only, and any mail block that then fails gets its usings in this step.

Package: `Regira.Office` (guides, docs). Run the verifier's `office-mail` group.

### 2d. Regira-Modules (`src/vue`)

- **M13**, `entities/ai/entities.patterns.md`, *Form validation & error handling*, after the blockquote line "`:disabled="feedback.isPending"`, as in the example, prevents double-submits." (:1117), before `## Tabbed forms` (:1119). With decision 3 (a):

  ````markdown
  **A 409 comes in two kinds**, told apart by the ProblemDetails `title`: `"Concurrency conflict"`, a stale concurrency
  token (the row changed or was removed since it was read), and `"Conflict"`, a database constraint. Both land in
  `feedback.message` as `"Server error: "` plus the server's `detail`; `useForm`'s `failure` tells them apart. To offer
  a Reload for the first kind, re-read the row and pass it up through `update:modelValue`: the details page and the form
  both take it, with its current token, and the user re-applies the edit.

  ```ts
  import { computed } from "vue"
  import { toFeedbackError } from "@regira/modules/vue/ui"
  import { ArchivedFilter } from "@regira/modules/vue/entities"

  const { service: entityService } = useEntityStore()
  const { item, feedback, failure, handleCancel, handleSubmit, handleRemove, handleRestore } = useForm<Entity>({ entityService, props, emit })
  const isStale = computed(() => failure.value?.status === 409 && failure.value.title === "Concurrency conflict")

  async function reload() {
      feedback.pending("Reloading…")
      try {
          // the details page's v-model takes the fresh row, and useForm resets item and original to it
          emit("update:modelValue", await entityService.details(item.value.$id, { archived: ArchivedFilter.included }))
          feedback.reset()
      } catch (ex) {
          feedback.fail("Reloading failed", toFeedbackError(ex))
      }
  }
  ```

  ```vue
  <button v-if="isStale" type="button" class="btn btn-warning" :disabled="feedback.isPending" @click="reload">Reload</button>
  ```

  Reload from the form, not through `useDetails().load()`: `load` belongs to the `useDetails` in `Details.vue`, and a
  second `useDetails()` in the form fetches on its own mount and fills an `item` the form does not show. A delete's 409
  shows as `"Deleting failed: …"`, a restore's as `"Restoring failed: …"`.
  ````

  Under 3 (b) the recipe wraps the pooled service instead. It is a class instance, so a spread would drop its methods; `Object.create(service, { save: { value: async (entity) => { … } } })` overrides `save` only, sets `isStale` from `ex?.response?.status` and `ex.response.data?.title`, and re-throws so `useForm` still reports it. That version passed `vue-tsc` on a scaffolded slice; under it, `reload()` also clears `isStale`. F19: the table at :1031 adds restore. Optional pointer in Packages `entities.patterns.md`, "**3. Handle the 409 on the client.**" (:1864-1866): "In a Vue SPA on `@regira/modules`: its `entities.patterns` → *Form validation & error handling* has the Reload recipe."
- **M14**: after the blockquote ending "…cannot paint over a newer result either." (:357), before `## Entity selector (relation picker)` (:359):
  > **A custom insert on a `/new` route** gets none of `useForm`'s hand-over either. Emit the saved row, `emit("update:modelValue", saved)` (through `service.toEntity(…)` when a custom endpoint answered it), then `router.replace` the route with its id, as `useForm` does (entities.signatures → §6, *What `handleSubmit` does besides saving*). `useDetails` re-loads when `:id` changes on the same route, except when it leaves `new`. Without the emit the page goes on editing the row as it was before the save, without what the server set (a concurrency token; the id too, when a custom endpoint did the insert), and its next save carries no token or inserts again.

  `entities.signatures.md:411`: `    routeId: ComputedRef<string> // the route's :id, "new" on an insert; a change on the same route re-runs load(), except one that leaves "new"`.
- **M15**, with decision 4 (a): `auth/ai/auth.instructions.md:306` stays as it is. `auth.signatures.md:313`: `emits: LoginModalEmits = LoginFormEmits (from its default form only; a slotted form emits its own) + close (header X)`; `:314`: `emits: ForgotPasswordModalEmits = close`; and `entities.shell.template.md:425` reads "@close is the modal's header X" (it regenerates `App.vue`). Under 4 (b): `:313` notes the re-emits come from the default form only; a line after `:314` says "Neither modal declares close: `@close` falls through (`$attrs`) to the injected modal, which emits it from its header X, so it needs a swapped-in Modal that emits close, and an ejected copy that keeps one root and `inheritAttrs`"; `:306` points there.
- **M16**: `auth.instructions.md`, `### Reacting to a token — onAuthenticated`, after the paragraph ending "…hook then loads." (:100), before "⚠️ **Register it below everything its handler uses.**":
  > ⚠️ **A default read from the signed-in user is unknown in `setup` on a reload.** `authData` stays empty until the stored token is validated, so a search-object default taken from a claim (an `ownerId`) is `undefined` there and the first search runs without it. Set it inside the `onAuthenticated` handler, before the search it runs. With `{ immediate: false }` the handler skips a token that is already there, so also set it in `setup` for in-app navigation:
  > ```ts
  > const applyDefaults = () => (searchObject.value.ownerId ??= authStore.getClaimValue("owner_id") as string | undefined)
  > applyDefaults()
  > onAuthenticated(() => { applyDefaults(); searchHandler(false) }, { immediate: false })
  > ```
- **F14**: the same section (:97-100) and the `onAuthenticated.ts:23-24` JSDoc: "one there runs before the stored token is validated and 401s, wasting a request" becomes "one there sends a second request: it carries the stored token (the bearer header is attached before validation), and 401s only when that token has expired". The banner sentence after it then speaks of "a failed fetch's banner".
- **F16**: `auth.signatures.md:282`: `fail` carries `ex?`.
- **M18**, with decision 5 (a): `_template/scaffold.mjs`, the printUsage `--as <fieldName>` entry after "…(default camelCase class, Employee → employee)" (:1227) and the header comment after :37: "It renames the key only: an --owns sub-slice folder stays the kebab-cased class plural (OrderLine → order-lines/)". `entities/ai/entities.template.md:15`, the `--owns <Child>` row: "Trailing `--as <field>` sets the JSON key only; the sub-slice folder stays the kebab-cased class plural (`--owns OrderLine --as lines` → `order-lines/`)". Test first: the `--help` test (`scaffold.test.js:332`) expects `"order-lines/"`.
- **M8**, `formatters/ai/formatters.instructions.md`, *Gotchas*: bullet :73 becomes the one below, and the basic report's URL bullet follows it unchanged:
  > - **`formatTextPreserveNewLines` emits HTML and escapes nothing.** `v-html` renders markup as is: escape user text first, `v-html="formatTextPreserveNewLines(htmlEncode(text))"` (`htmlEncode` from `@regira/modules/utilities/string-utility`), or skip `v-html` with CSS `white-space: pre-line`; user-supplied HTML (rich text) goes through a sanitiser such as DOMPurify first.
- **G25**, the front-end half: in the *Account UI* table (:271-278) the row `| Confirm email (from the mail link) | none: _Confirm page_ below | — |`, and after the confirmation bullet (:356), before `## $auth global` (:358):

  ````markdown
  ### Confirm page

  The confirmation mail links to the page `confirmEmailUrl` named, with `?token=…` as its query string. The page is
  anonymous, so it loads in `onMounted` (_What a first load shows_), and posts the token back:

  ```vue
  <!-- views/ConfirmEmailView.vue, route { path: "/confirm-email", name: "confirmEmail", meta: { allowAnonymous: true } } -->
  <script setup lang="ts">
  import { onMounted } from "vue"
  import { useRoute } from "vue-router"
  import { useAxios } from "@regira/modules/vue/http"
  import { Feedback, useFeedback, toFeedbackError } from "@regira/modules/vue/ui"

  const route = useRoute()
  const feedback = useFeedback({ autoHideDelay: 0 })
  onMounted(async () => {
      const token = route.query.token
      if (typeof token !== "string") return feedback.fail("This link is incomplete")
      feedback.pending("Confirming your address")
      try {
          // the API reads the user from the token
          await useAxios().post("users/confirm-email", { token })
          feedback.success("Your address is confirmed: you can sign in")
      } catch (ex) {
          feedback.fail("This link is invalid or has expired", toFeedbackError(ex))
      }
  })
  </script>

  <template>
      <Feedback :feedback="feedback" />
  </template>
  ```

  - **Add `"users/confirm-email"` to `credentialUrls`** beside `"users"`: the body carries the token, and `"users"`
    matches that path alone.
  - The sign-up form posts `confirmEmailUrl: new URL(router.resolve({ name: "confirmEmail" }).href, location.origin).href`,
    as the shell's `ForgotPasswordForm` builds its `siteUrl`.
  ````

  Under decision 2 (b) the post sends `{ token, userName: JSON.parse(atob(token)).Username }`; the token's JSON is ASCII (System.Text.Json escapes the rest), as the shell's `ResetPasswordView` already relies on.
- **G31**, the front-end half: `auth.instructions.md:387-388`, "the recover email links to `siteUrl` with its query string set to `?token=…`, the Base64 token percent-encoded (`/reset-password?token=…`)". F6: the four other places say "with `?token=…` as its query string"; the `.vue` and its template `.md` change together.
- **G35**, the front-end home: the blockquote `> **\`<img src>\` on a guarded download 401s**` (`entities.patterns.md:1018-1020`) becomes:

  ````markdown
  > **`<img src>` on a guarded download 401s** — the tag sends no `Authorization` header. Preview a file not yet
  > uploaded from its in-memory `uri` (object URL). Show a stored file that must stay private (a draft's image) from
  > an object URL of a download through the shared axios, which sends the bearer; expose the download **anonymously**
  > on the back-end only for public files (`Regira.Entities` → _Public (anonymous) attachment downloads_):
  >
  > ```ts
  > import { onBeforeUnmount, ref, watch } from "vue"
  > import { useAxios } from "@regira/modules/vue/http"
  >
  > const props = defineProps<{ uri?: string }>()                       // the link DTO's uri
  > const src = ref<string>()
  > const release = () => { if (src.value) URL.revokeObjectURL(src.value) }   // an object URL holds its blob until revoked
  > watch(() => props.uri, async (uri) => {
  >     release()
  >     src.value = uri ? URL.createObjectURL(await useAxios().getFile(uri)) : undefined
  > }, { immediate: true })
  > onBeforeUnmount(release)
  > // <img v-if="src" :src="src" alt="" />
  > ```
  >
  > The list DTO nests file metadata under `attachment.*` (`item.attachment?.fileName`), not on the row root — read it there.
  ````

  `entities.instructions.md:696`, the row `Attachment images/thumbnails 401 in \`<img>\` tags`, gets the Fix cell "Public file: expose the download anonymously (`Regira.Entities` → `entities.patterns` → _Public (anonymous) attachment downloads_). Private file: load it with `useAxios().getFile(uri)` into an object URL (entities.patterns → *Attachments (files)*)". `entities.attachments.template.md:424-426` adds "A file that must stay private loads through `getFile` instead (entities.patterns → *Attachments (files)*)."
- **F20**: `entities.patterns.md:856-857`: "the chips have room for one value at most (the `#chip` slot receives the row); a scalar to edit per row needs the table".

Then `npm run build`, which regenerates the templates from the shell and component sources.

## 3. Code, after the decisions

Guide lines read these: M13 uses *3b*'s `failure`; the confirm page (*2d*), G25's sentence and F4 (*2a*) describe *3a*'s `{ token }`; M15's text follows *3c*. Build each before its guide lines.

### 3a. `ConfirmEmailInput.UserName` optional (D12)

With decision 2 (a). Test first: `ConfirmEmail_WithoutUserName_Confirms` answers `400` on `wip`. Then `ConfirmEmailInput.UserName` drops `[Required]` and becomes `string?`, with the XML doc "Not read: the user comes from the token." It is not `[Obsolete]`, like the unused `Password` the docs call "available to overrides". Guide :1051 and `docs/controllers.md:97` list `{ token }`, "`userName` is accepted and not read".

Package: `Regira.Security.Authentication.Web`. **Behaviour change:** validation only loosens; the OpenAPI schema marks the field optional.

### 3b. The form's last failure (D14)

With decision 3 (a). Tests first, in `__tests__/vue/entities/form-failures.test.js` (it already mocks `save`): a 409 titled "Concurrency conflict" sets `failure`; the next submit clears it, and so does a new `modelValue` (the M13 reload); a network error leaves `status` undefined. Then `form.ts` gains `failure: Ref<{ status?: number; title?: string } | undefined>`, set in the three catch blocks (:106-123, :170-186, :203-210), cleared at each `pending` and in the `modelValue` watch (:228-233). `FormOut` in `entities.signatures.md:465-472` lists it. About 12 lines.

Modules: `vue/entities`, an addition to a returned object.

### 3c. `close` on the auth modals (D15)

With decision 4 (a).

1. **Test first**: a new `__tests__/vue/auth/modal-close.test.js`, with `createApp` and `h` as `ui/confirm-button.test.js` does, since there is no `@vue/test-utils`.
   - `LoginModal.emits` and `ForgotPasswordModal.emits` contain `"close"`. This fails on `wip`.
   - A stub modal provided through `MODAL_COMPONENT_KEY` emits `close`, and the parent's `onClose` runs exactly once. This passes before and after, and catches a forgotten forward.
2. **Types**: `LoginModalEmits` (`LoginFormEmits` plus `(e: "close"): void`) in `useLoginForm.ts`, and `ForgotPasswordModalEmits` in `useForgotPasswordForm.ts`. Both are exported from `auth/index.ts`, which the ejected copies import through the barrel.
3. **Components**: both declare the type with `defineEmits` and forward it on the root: `<component :is="Modal" … @close="$emit('close')">`. Declaring `close` takes `onClose` out of `$attrs`, so without the forward every current `@close` stops working.
4. `npm run build` regenerates `_template/ui/LoginModal` and `_template/ui/ForgotPasswordModal`.

Apps that ejected earlier keep working by fall-through. Modules: `vue/auth`, a contract change, not a behaviour change.

### 3d. The password controller's user type (F3)

With decision 7 (a). Test first: a controller derived from `PasswordControllerBase<TUser>` over a type that derives only from `IdentityUser<string>`; on `wip` the test project does not compile (CS0311). Then the constraint becomes `where TUser : IdentityUser<string>`, as on the other two bases. Every existing `TUser` still satisfies it. Package: `Regira.Security.Authentication.Web`.

## Not proposed

- **D13** (`useFileUrl` or `<AuthImage>`): defer past 6.6.0. G35's recipe covers one image. Revocation, a re-fetch after a sign-in, and the uncached download behind every thumbnail need a design and tests of their own.
- **D17** (`--picker` keeps one scalar): decline. *Owned rows with a relation and scalars* (`entities.patterns.md:854`) covers it, and the scaffold's own output points there. It would undo the "a join with a scalar is not pure" rule stated in five places, and the scaffold cannot know the enum's values.
- **F17** (the shell's `LoginModal` has no `@close`): a sign-in gate may want no way out. Your call.
- **`form.ts:116` labels a 409 "Server error"**: wording, but it changes what every app shows. *2d*'s M13 says how to read it.
- **`form.ts:137` mutates the live route before `router.replace`**: only a cancelled replace shows it (the URL keeps `?src=`).
- **Refresh tokens in the front end**: a feature. *1e* documents the gap.
- **The other office verifier groups** (F8): decision 6.
- **The tooling side**: proposed in the tooling repository.

## Coordination with the basic rerun

The basic rerun's 39 Fix-before-publish items have no proposal yet, and many land in the same sections. Build both in one pass, area by area, anchored on headings and quoted lines, since each insertion shifts the numbers below it:

| File | Basic rerun | This proposal |
|---|---|---|
| `entities.instructions.md` | G5 (:1642), G9 (:492-497), G10 (:266, :1488), G21 (Attachments) | G9 here supersedes its text; G26 (:1636-1644), G27 (:1726-1733), G28 (:405), G35 (:1735-1737) |
| `entities.patterns.md` (Packages) | G1 (:1999, :2030), G2 (:934-937), G3 (:2033), G11 (:2080) | G29 (:218-238), G30 (:746, :886), G35's optional :2081 beside G11 |
| `office.mail.instructions.md` | G8 (Installation, :28) | G8's line drops (*1a*); G22 (:11, :164, :238, :275) |
| `project.setup.md` | G4 (:349-363, :391-392) | G33 (:38, :183-212, :312, :610, :679, :771); no overlap |
| Modules `entities.patterns.md` | M1 (*Tabbed forms*, :1132), M3 (:302), M4 (:966), M5 (:1010) | M13 just before *Tabbed forms* (:1119), M14 (:357), G35 (:1018), F20 (:856) |
| Modules `formatters.instructions.md` | M8 (after :73) | :73 rewritten (*2d*); M8 follows it |
| Modules auth guides | M7 (`auth.instructions.md:221`, `auth.examples.md:38`) | M15–M17, G23, G25, G31 |
| `_template/scaffold.mjs` | M11 (:1043) | M18 (:37, :1227), F18 (*1f*) |

## Decisions

1. **Sign-in with an unconfirmed address (*1b*).**
   - (a) The base honours `SignIn.RequireConfirmedEmail` and answers `401 { isNotAllowed: true }` (D11). *Recommended:* the app set an option the base silently ignores, so honouring it is a security fix, not a new default. Every app is spared copying a 20-line override.
   - (b) Document the override (G23's recipe). No behaviour change, but every app that sets the option must find and copy it.
2. **`ConfirmEmailInput.UserName` (*3a*).**
   - (a) Optional, not read (D12). *Recommended:* a required field the server never reads makes every client decode the token to pass validation. Nobody breaks.
   - (b) Keep it required and document why (G25 as reported).
3. **The form's last failure (*3b*).**
   - (a) `failure` (status and title) on `FormOut` (D14). *Recommended:* about 12 lines, additive, and every concurrency app is spared a delegating wrapper.
   - (b) The wrapper recipe only.
4. **`close` on the auth modals (*3c*).**
   - (a) Declare and forward it (D15). *Recommended:* today the documented `@close` works by fall-through, which an eject or a modal swap breaks silently, and the API listings do not show it.
   - (b) Document the fall-through (M15).
5. **The sub-slice folder after `--as` (D16).**
   - (a) Keep the class plural and say so in the help (M18). *Recommended:* the folder is internal and unique per child class.
   - (b) Name it after `--as`. It needs F18's guard against reserved names, and a re-run on an existing slice leaves two sub-slices.
6. **The verifier's group usings (F8, *2c*).**
   - (a) Trim `office-mail` to the SDK-implicit usings now, so G22's blocks are checked as written. Trim the other four office groups after 6.6.0. *Recommended:* it fixes the check for the guide this step edits, without opening every Office guide before the release.
   - (b) Trim all five office groups now.
   - (c) Leave them.
7. **The password controller's user type (F3, *3d*).**
   - (a) Relax the constraint to `IdentityUser<string>`. *Recommended:* it matches the other two bases and the docs, and every existing type still satisfies it.
   - (b) The docs say `IdentityUser` for this base.

## How every step is tested

- **Baseline first.** Run each step's test projects on `net8.0` and `net10.0` before touching code, and note anything already red. Run mail tests offline with `--filter "TestCategory!=Network"`.
- **Crypto pins (*1a*).** `dotnet list <csproj> package --vulnerable --include-transitive` on each of the six projects reports nothing, a restore shows no NU1902 or NU1903, and their test projects stay green. A consumer of the local feed that references `Regira.Office.Mail.MailKit` restores clean without its own pin.
- **Code fixes: a failing test first.** That covers *1b* and *3a* (`IdentityControllerTests`), *3d* (Web.Security), *3b* (`form-failures.test.js`), *3c* (`modal-close.test.js`), and *1f* and M18 (`scaffold.test.js`).
- **Docs compile.** Run `dotnet run --project tools/GuideVerifier -- . --group <group>` for every group a step edits: `security` (it holds `project.setup.md`), `entities` and `office-mail`.
- **The documentation server after the Packages guide edits.** It keeps word-for-word copies of some `entities.patterns` recipes, `after-commit` among them, so its tests run against the edited guides. Its proposal says how.
- **Regira-Modules.** `npm test`, `npm run types`, then `npm run build` to regenerate the templates, then `git ls-files --eol` for LF.
- **Knowledge rebuild last.** Check that:
  - `search_docs` for `Regira.Office.Mail.MailKit.DependencyInjection` reaches the mail guide;
  - `how_to "how do I send a reminder before a meeting starts"` answers the reactor recipe with its timer paragraph;
  - `get_type` for `MailAddress` lists its conversions (the tooling side's S6).
- **Changelog.** Each step adds its packages' bullets under *Unreleased*. **Security** goes on *1a* and *1b*. **Behaviour change** goes on *1b* (a) for apps that set the option, *3a* (validation loosens) and *3c* (a typed `close`).

## Outcome

Built 2026-10-14 in Packages and Modules, uncommitted; the tooling side is built in its own repository. Every decision took (a). Deviations from the steps above:

- **1a.** The dependency refresh (`8626b34`) came after this proposal: `System.Security.Cryptography.Xml` 10.0.13 already cleared `Excel.NpoiMapper` and `PDF.PDFtoPrinter`, MsgReader is 6.2.0 (it still floors Pkcs at 10.0.12) and Syncfusion 35.1.40 (Xml 10.0.11). Four projects got a pin: `Mail.MailKit`, `Mail.MSGReader` and `DAL.SqlServer` (Pkcs 10.0.13), `Word.Syncfusion` (Xml 10.0.13). Each restores clean. Left as they are: AutoMapper 14.0.0 and ImageSharp 3.1.12 (documented), and the test-only `Entities.Providers.Testing`, which restores Pkcs 9.0.11 through EF Core's SQL Server provider.
- **1b.** `IsNotAllowed` is `{ get; set; }`, like the record's other properties. The guide's sentence sits below the table, the cell pointing at it.
- **1d.** The *OpenAPI & Scalar UI* snippet keeps its `<!-- no-compile -->`: the `security` group carries no Scalar package.
- **2b.** `docs/attachments.md` describes the owner's `RemoveItem` override in prose, without a pointer into `ai/` (the two layers never refer to each other). G29's optional pointer in `entities.instructions` was not added. G30's "may set or change it", G35's object-URL clause and the M13 pointer were added.
- **2c.** With `office-mail` on the SDK-implicit usings (F8), more blocks failed than F9 lists. `docs/mail/README.md` (*DI Registration*, *MailAddress*, *MailRecipient*, *DummyMailer*, *Identity*) and `docs/mail/examples.md` (examples 4–6) gained their usings, as did the guide's *MailAddress* and *MailRecipient* blocks. The guide's DI block keeps its marker (`environment` is ambient). The `security` group's `Microsoft.AspNetCore.OpenApi` pin went from 10.0.12 to 10.0.13, which the refresh had broken (NU1605).
- **2d.** Beyond the text: `vue/entities/docs/views.md` mentions `failure`, the formatters README says `formatTextPreserveNewLines` escapes nothing, F16 lists each emit's payload, and 1f has a second test (`--overwrite-slice` on an existing slice).
- **Tests.** Failing first: 1b and 3a (`IdentityControllerTests`), 3d (CS0311), 1f, M18, 3b and 3c. Green: Web.Security 215 (net8.0) / 226 (net10.0), Office.Mail 60 offline, DAL.SqlServer 37, Word Syncfusion 123, Modules 585 with `vue-tsc` and `npm run build`, GuideVerifier `security` 41, `entities` 54 and `office-mail` 17, the solution build with 0 errors. A scratch knowledge base built from both trees passes `--verify` and the three checks above.

<!-- {% endraw %} -->
