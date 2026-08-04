namespace Regira.Invoicing.Billit.Models.Contacts;

public class AddressDto
{
    public string? AddressType { get; set; }
    public string? Name { get; set; }
    public string? Street { get; set; }
    public string? StreetNumber { get; set; }
    public string? Box { get; set; }
    public string? Zipcode { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string? Phone { get; set; }
}