# Applying the 6.6.0 four-apps test-run findings
<!-- {% raw %} -->

As of 2026-10-15. Sources: the report *Regira 6.6.0 pre-release test run — four apps* (ShopMate, HelpDesk, FleetAdv,
LearnHub; kept outside the repositories), Regira-Packages `wip` at `3137b99`, Regira-Modules `wip` at `812007d` and
the tooling repository at `8435f50`. Four read-only checks, one per area, re-read every finding in the code; the code
fixes started from a test that fails on `wip`.

**Status: built 2026-10-15, uncommitted. See *Outcome* at the end.**

## Rules

1. **Security and critical are always fixed**: wrong results, lost data, a crash, a hole. Otherwise: fix small,
   document hard (KISSS), and drop best-practice work that adds API without a defect behind it.
2. **A code fix starts with a test that fails on `wip`.**
3. **The report's exact texts are checked, not copied.** Half of them named a wrong overload, missed a caveat or would
   have shipped unsafe advice; the table says where.
4. **Docs in final state; versions stay Bram's call.** Every package touched sits in the unreleased 6.6.0 cycle.

## Decisions taken

| ID | Decision | Why |
|---|---|---|
| D20 | **Built.** `Item/Name` from `Title`, a `TaxAmount` per VAT subtotal, VAT rounded once per rate (half away from zero) with the totals following from it, and an optional party `Address` written as `PostalAddress` | Wrong results: every converted invoice failed Peppol BIS 3.0, and the missing subtotal `TaxAmount` failed the UBL schema itself. Three new tests failed on `wip` |
| D21 | Documented (G45) | A precise validator change needs the inner type, which no registration records; the warning stands by design when the inner filter reads `Q` |
| D23 | **`AccountLinks` made public** (one keyword); no `MapAccountEndpoints` | Three documented paths — a minimal-API host, a resend action, an override — had to re-implement the password-reset-poisoning check by hand. The tested helper is the safer shape; a mapper is ten endpoints of new surface |
| D18 | Built (Modules) | Small, and the 401 body already carries `isNotAllowed` |
| D19 | Built (Modules) | One line; a 409 is not a server error |
| D24 | Built (Modules) | Data loss |
| D25 | `DateInputEmits` only | Its `string \| Date` was wrong; `Tab.values` and a `TSearchObject` generic add typing with no defect behind them |
| D26 | Built (Modules) | Two small guards that turn broken output into an error |
| D22, D27 | Not built | New features |
| D28 | Not built | Best practice, no defect (rule 1) |

## Verification and what was done

### Regira-Packages

| ID | Verdict | Correction to the report's fix | Done |
|---|---|---|---|
| G40 | Confirmed, also at `entities.instructions.md:1355` | There is one `UseDefaults` with an optional callback, not two overloads; `UseNormalizerDefaults` adds no `Q` filter | Both places |
| G41 | Confirmed | Re-saving does not re-normalize an unchanged row; the transform is app-wide. **Found:** under a Turkish culture `DefaultNormalizer` deleted every capital `I` (a culture-sensitive `IgnoreCase` regex) and lowercased to a dotless `ı` | Guide paragraph with `LOWER()` for stored rows; the normalizer fixed (invariant regex and casing), a `tr-TR` test |
| G42, G43 | Confirmed | Mostly fixed by D20; left: category `S` on a 0 % line (BR-S-05), `Code` must be a VAT number and `Identifiers` the endpoint | *How the model maps to UBL* in the hub guide and README, an `Invoicing.UblSharp` card |
| G44 | Confirmed | "`UseRegira` found none" claims a cause the validator cannot see | The free-tier error opens with *No Regira license key covering regira.entities was registered when the first For<>() ran*, only when none was; two tests |
| G45 | Confirmed, also the virtual-entity blueprint | An unqualified "by design" contradicts `ai/AGENTS.md`'s "an ignored `?q=` is a defect" | Qualified: by design when the entity's own repository or service reads `Q`; both blueprints and `ai/AGENTS.md` |
| G46 | Confirmed | "What mirroring costs" is a bold lead-in, not a heading | Warning text names the fourth remedy and the right heading |
| G47 | Confirmed, three places | The validator runs on net10.0 only | `entities.patterns`, `entities.setup`, `entities.instructions` |
| G48 | Confirmed | Duplicate names overwrite entries; `Identifier` would name the entry; `Zip()` is stream-backed | `how_to` recipe `zip-owner-attachments`, its documentation-server fallback, pointers from the bytes note and the storage example |
| G49 | Confirmed | Add the token format; the check now has a public API (D23) | *An endpoint of your own that mails a token link* in `security.instructions`, `docs/controllers`, the card |
| G50 | Confirmed, fix unsafe | Passing `Never` options to `PatchCommand` writes nulls into the merge base and can 400 | `Never` only for serializing the patch; the app's options still go to the command |
| G51 | Confirmed | Standard Base64, UTF-8, `System.Text.Json` defaults: PascalCase keys, read case-sensitively | Named in `security.instructions` and `docs/controllers` |
| G52 | Partly | `Common/README.md` did not say seconds; `DictionaryCacheProvider` ignores `duration` | XML `<param>` docs, README sentence |
| G53 | Confirmed | The parser needs the Newtonsoft serializer's nested dictionaries; bools render `1`/`0` | `web.instructions` names it and what it writes; `io.storage` example fully qualified |
| G54 | Confirmed. **Found:** `MollieConfig.Api` is never read | — | Guide, examples and README |
| G55 | Partly: the behaviour is MiniPdf's, and a workbook's own *Scale to Fit* applies | Reworded; the page order is not verified | XML doc, `office.pdf.instructions`, `docs/pdf` |
| G56 | Confirmed | Also needs `DependencyInjection.Validators` and `Microsoft.EntityFrameworkCore` | Using comments |
| G57 | Confirmed | `List` keeps its own order; foreign ids drop silently; one re-read per row | *Reordering separately registered rows* |
| G58 | Confirmed, fix wrong | An ids cap does not bound an export by search object | Authorization, a required filter or a `PagingInfo` |
| G59 | Confirmed by two new tests (net8, net10) | — | One sentence in the shared-principal bullet |
| G60 | Confirmed, fix would do nothing | No seeding flag exists, and a scoped flag never reaches the reactor's scope | The recipe's reactor keeps a scoped `SettledInvoices` set, one per committed save |
| G61 | Confirmed | Double-quoted `(?!` breaks in interactive bash | Single-quoted `docker run` lines in both layers |
| G62 | **Security**, worse than reported | The placeholder secret passed the 64-byte check, so a forgotten one shipped a known signing key; `ApiKeys[].Key` is no configuration path. `security.examples` had the same flaw | Secrets left out of the template and the example, so startup fails until set; `user-secrets` lines with `Authentication:ApiKeys:0:Key`. On review: a key never set now names its path and user-secrets, where the error named the array shape; three tests |
| G63 | **Security**, guide only | Any method on a `/search` route passed, not only POST | The recipe reads by method and action name; its documentation-server fallback |
| G64 | Confirmed | — | Inline in the split criteria |
| G65 | Confirmed | Save right after `Modify`; hold the trusted-writer flag around it only | A bullet in the mediator workflow |
| G66 | Confirmed | The simple slot loses the includes flag; the linked option is a party link, not a profile entity | Blueprint budget line |
| G67 | Claim right, reason wrong | Transient alone is not safe; it is the scope the entity service resolves in | Beside the row-level scoping bullet |
| G68 | Confirmed | `sortBy` is one `TSortBy?` | Both forms on the card |
| G69 | Confirmed | `sign-up` adds the token `up` and `e-mail` the token `mail`, which tied Auth.Web with MailKit on an existing test | Tags without them |

### Documentation server and test harness

| ID | Verdict | Done |
|---|---|---|
| S7 | Confirmed: the member hint answered, not the type search | `FindNestedTypes` in the named package; two tests |
| S8 | Confirmed; indexers were skipped too | Public `const` (with value) and `static readonly` fields and indexers; golden updated |
| S9 | Confirmed for 15 packages, not ~60 | A `readme` section for a package with no guide and no guide-carrying ancestor, Installation and License dropped; a `--verify` warning |
| S10 | Confirmed | Real-knowledge-base test for HelpDesk's feature list |
| H8, H9 | Confirmed | The test-run guide: processes, listeners and containers before a run is moved aside, `robocopy /E /MOVE` |
| H10 | — | Not done: an allow rule is a permission setting, Bram's to add |
| H11 | Confirmed, fix wrong after a resume | The test-run guide: a continued run's cost is its total minus the previous log's |

### Regira-Modules

| ID | Verdict | Correction to the report's fix | Done |
|---|---|---|---|
| D24 | **Data loss**, confirmed | — | Both echoes removed (an unset collection stays unset), a pending write carries same-tick appends; `useListInput` the same, reads unset as `[]`, and its temp ids stay negative (**found:** rows `3`,`4` gave a new row `2`). Tests mount a real `v-model` parent |
| M19, D18 | Confirmed | Ejecting `LoginForm` alone cannot read the body: `useLoginForm` swallows it. `isNotAllowed` covers phone and account confirmation too | `isNotAllowed` from the 401 body, both flags reset per attempt (`isLockedOut` never was), a neutral message; tests, guides |
| D19 | Confirmed | — | A 4xx below 500 reads "Saving failed"; tests and the failure table |
| M20 | Confirmed | Repeated keys come back as `string[]` | Card, instructions, signatures |
| M21 | Confirmed | Slotting `LoginModal` replaces `LoginForm`; no sign-up route existed in the docs | Link beside `LoginForm` in the slot, the route spelled out |
| M22 | Confirmed | `concurrencyToken` only when the entity carries one (opt-in) | Card |
| M23 | Confirmed (four lines, not three) | The `authStore` guard was always false at install | `lang-plugin.ts` in both shell variants, storage in try/catch; scaffold test |
| M24, M25, D26 | Confirmed | `InputSelector` has no `exclude` prop: it is `:filter-defaults` | Recipe; `--picker` self and `--dir` outside `src/` refused (also `--dir src`, which emitted `@/src/…`); help and tests |
| M26 | Confirmed | It is Bootstrap's JS attribute `data-bs-popper`, not Popper | `data-bs-popper="static"` or `end-0` |
| M27 | Partly: the rows are hydrated copies, not raw | — | Card |
| M28 | Confirmed | `$setCulture` is a template global only; `langCode` is two letters | `useAppStore().setCulture` in a watch, the region caveat |
| M29 | Confirmed | — | try/catch in the generated `InputSelector`, `v-if` advice; test |
| M30 | Confirmed, also at three more snippets | — | "Edit the scaffolded override" everywhere. **Found:** the `prepareItem` example ended in `?? []`, which deletes an unloaded collection — removed |
| M31 | Partly: the answer existed twice | — | Pointer under *Auth store* |
| M32 | Confirmed | Patch through the child's pooled store, re-read through the parent's | Example |
| M33 | Confirmed, **a bug**: Vue skips the second `app.use`, so the app icons never loaded | — | `loadIcons(appIcons)` |
| M34 | Confirmed | — | `v-model="item.lines"`, with `:readonly` |
| M35 | Confirmed | The pooled service, inside the Form | Recipe with `how_to` key `refresh-form-after-custom-endpoint` |
| M36 | Confirmed | — | Card |
| M37 | Confirmed; no `scripts` block existed to check | — | `vue-tsc -b && vite build` in both guides, `scripts` in the known-good `package.json` |
| D25 | `DateInputEmits` wrong (`string \| Date`) | — | Narrowed to `Date` |

**Found:** the custom login form in `auth.examples.md` disabled its button on `isLockedOut` for good; it now shows the flags.

## Outcome

Built 2026-10-15 in all three repositories, uncommitted.

**Verified:**
- Packages: the solution builds; Invoicing 8 (6 new), Entities.DependencyInjection 142 (2 new), Entities 585 / 588 on
  net8 / net10 (2 new), Common 308, Normalizing 27 (1 new), Web.Security 222 / 233 (3 new). Each new code test fails on `wip`.
  GuideVerifier: 20 groups, 314 snippets.
- Modules: vitest 601 (16 new), `vue-tsc` clean, `npm run build` green; the generated templates changed only in
  `main.ts`, `user-plugin.ts`, the new `lang-plugin.ts`, `InputSelector.vue` and the ejectable `LoginForm.vue`.
- Documentation server: 486 / 486 against a scratch knowledge base built from the final trees (`Verify`: 105 packages, 225
  sections, 682 cross-references, no warning; 15 packages gain a `readme`). Against the in-repo knowledge base 485 /
  486: the S10 HelpDesk test needs the rebuild, since it reads the new package tags.

**Not done:**
- The in-place knowledge-base rebuild and deploy: a documentation server (PID 57196 on :60870) runs from that build.
- H10, the allow rule: a permission setting.
- D22, D27, D28, and D25's `Tab`/`TSearchObject` (see *Decisions taken*).

**Versions (your call):** `Regira.Invoicing` gains two public types (`PartyAddress`, `PartyAddressInput`) and a
default interface member, and `Regira.Security.Authentication.Web` makes `AccountLinks` public: minor by the
`AGENTS.md` rule, riding the unpublished 6.6.0.

**Watch:** two recommend tests sit close to their line. On the existing event-feature test, Authentication.Web ties
Office.Mail.Web for sixth (40) and stays seventh only by id order; on the new HelpDesk test it ranks fourth of six,
behind MailGun. A tag change on either package can move them.

<!-- {% endraw %} -->

