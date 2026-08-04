using Regira.Invoicing.UblSharp.Attributes;

namespace Regira.Invoicing.UblSharp.Core;

[PeppolList("UNCL4461")]
public enum PaymentMeansCode
{
    [PeppolCode("1")]
    NotDefined,
    // ... -> https://docs.peppol.eu/poacc/billing/3.0/codelist/UNCL4461/
    [PeppolCode("42")]
    BankAccount,
    [PeppolCode("ZZZ")]
    MutuallyDefined
}