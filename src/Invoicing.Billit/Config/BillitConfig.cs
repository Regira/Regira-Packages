namespace Regira.Invoicing.Billit.Config;


public class BillitConfig
{
    public class ApiConfig
    {
        public string? BaseUrl { get; set; }
        public string? Key { get; set; }
    }

    public string? PartyId { get; set; }
    public ApiConfig? Api { get; set; }
}