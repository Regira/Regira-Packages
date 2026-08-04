namespace Regira.IO.Storage.Helpers;

public enum OnExistsAction
{
    ThrowError = 0,
    Overwrite,
    GenerateNewFileName
}