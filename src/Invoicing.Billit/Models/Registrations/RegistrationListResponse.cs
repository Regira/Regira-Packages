using Regira.Invoicing.Billit.Models.Contacts;
using Regira.Invoicing.Billit.Models.Identifiers;

namespace Regira.Invoicing.Billit.Models.Registrations;

public class RegistrationListResponse
{
    public List<CompanyItemDto> Companies { get; set; } = null!;
}

public class CompanyItemDto
{
    public int RegistrationId { get; set; }

    public CompanyDetailsDto CompanyDetails { get; set; } = null!;
    public List<CompanyIntegrationDto>? Integrations { get; set; }
}

public class CompanyIntegrationDto
{
    public int IntegrationId { get; set; }
    public string Integration { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? IntegrationStatusDescription { get; set; }
    public string? IntegrationDocumentToSign { get; set; }
}

public class CompanyDetailsDto
{
    public string TaxIdentifier { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public List<AddressDto>? Addresses { get; set; }
    public bool TaxDeductable { get; set; }
    public bool TaxLiable { get; set; }
    public List<IdentifierDto>? Identifiers { get; set; }
}