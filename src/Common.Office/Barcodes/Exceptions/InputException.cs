namespace Regira.Office.Barcodes.Exceptions;

public class InputException(string message, Exception? innerException = null) : Exception(message, innerException);