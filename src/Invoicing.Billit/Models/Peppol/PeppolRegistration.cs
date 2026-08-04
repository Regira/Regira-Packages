namespace Regira.Invoicing.Billit.Models.Peppol;

public class PeppolRegistration
{
    public bool Registered { get; set; }
    public string Identifier { get; set; } = null!;
    public List<string> DocumentTypes { get; set; } = [];
    public List<ServiceDetail> ServiceDetails { get; set; } = [];
}

public class ServiceDetail
{
    public string DocumentIdentifier { get; set; } = null!;
    public string DocumentIdentifierScheme { get; set; } = null!;
    public List<ProcessDetail> Processes { get; set; } = [];
}

public class ProcessDetail
{
    public string ProcessIdentifier { get; set; } = null!;
    public string ProcessIdentifierScheme { get; set; } = null!;
}