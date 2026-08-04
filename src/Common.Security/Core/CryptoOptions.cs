using System.Text;

namespace Regira.Security.Core;

public class CryptoOptions
{
    public string? AlgorithmType { get; set; }
    public string? Secret { get; set; }
    public int? Iterations { get; set; }
    public Encoding? Encoding { get; set; }
}