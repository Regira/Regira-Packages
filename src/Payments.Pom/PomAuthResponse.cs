namespace Regira.Payments.Pom;

public class PomAuthResponse
{
    public string UserId { get; set; } = null!;
    public string AuthToken { get; set; } = null!;
    public DateTime Expires { get; set; }
    // public List<(string,string)> Authorities {get; set;}
}