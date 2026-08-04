namespace Regira.Office.Excel.Abstractions;

public interface IExcelServiceCore;
public interface IExcelService : IExcelServiceCore, IExcelReader, IExcelWriter;
public interface IExcelService<T> : IExcelServiceCore, IExcelReader<T>, IExcelWriter<T>
    where T : class, new();