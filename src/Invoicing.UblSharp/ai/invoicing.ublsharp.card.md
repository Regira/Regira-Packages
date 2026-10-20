# Regira.Invoicing.UblSharp — index card

> Converts the shared `IInvoice` model into a UBL 2.1 invoice (`XDocument`) for Peppol BIS Billing 3.0. It builds the
> XML only; sending it goes through an access point such as `Regira.Invoicing.ViaAdValvas`. The guide lives on the hub
> `Regira.Invoicing`: `get_package(id: "Regira.Invoicing", section: "invoicing.instructions", heading: "UblSharp — UBL Conversion")`.

- **`new UblConverter().Convert(new UblDocumentInput { Invoice = invoice, Supplier = invoice.Supplier })`**
  (`Regira.Invoicing.UblSharp.Services`, `Regira.Invoicing.UblSharp.Core`). The seller is read from `Supplier` only.
- **Each party needs** a VAT number with its country prefix in `Code`, its Peppol participant ID as the first
  `Identifiers` entry (`new PartyIdentifier { Scheme = "0208", Value = "0123456789" }`) and an `Address`
  (`PartyAddress` with `CountryCode`). Without the address Peppol refuses the document (BR-08 to BR-11).
- **Lines:** `Title` is the item name, `Description` the line note. VAT is computed per rate and rounded once, so
  print the same per-rate totals on a PDF invoice.
- **Fixed values:** invoice type `380` (no credit notes), currency `EUR`, payment means `1`, tax category `S` on every
  line. A 0 % line needs another category set in the returned `XDocument` (BR-S-05).
- The member-to-element table is *How the model maps to UBL* in the hub guide.
