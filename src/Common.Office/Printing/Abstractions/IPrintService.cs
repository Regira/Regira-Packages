using Regira.Office.Printing.Models;

namespace Regira.Office.Printing.Abstractions;

public interface IPrintService
{
    /// <summary>
    /// Lists the names of installed printers on the system.
    /// </summary>
    /// <returns></returns>
    IList<string> List();
    /// <summary>
    /// Prints the specified image to the specified printer with the specified settings.
    /// </summary>
    /// <param name="input"></param>
    void Print(ImagePrintInputModel input);
}