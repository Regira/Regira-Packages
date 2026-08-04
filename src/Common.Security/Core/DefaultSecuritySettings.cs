using System.Security.Cryptography;
using System.Text;

namespace Regira.Security.Core;

public static class DefaultSecuritySettings
{
    public static string SaltKey = "38F81F41-DC54-4323-BED4-9434EB364C04";
    public static Encoding Encoding = Encoding.UTF8;
    public static string HashAlgorithm = HashAlgorithmName.SHA512.Name!;//"SHA512";
}