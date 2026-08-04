using Regira.Invoicing.UblSharp.Attributes;

namespace Regira.Invoicing.UblSharp.Core;

//[PeppolList("UNCL1001")]
public enum TaxExemptionReasonCode
{
    [PeppolCode("BR-S-10")]
    StandardRate,
    [PeppolCode("BR-Z-10")]
    ZeroRated,
    [PeppolCode("BR-E-10")]
    ExemptFromVAT
}