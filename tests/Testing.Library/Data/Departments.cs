using Testing.Library.Contoso;

namespace Testing.Library.Data;

public class Departments(params Department[] items)
{
    public Department ComputerScience { get; set; } = items.First(x => x.Title!.Replace(" ", string.Empty) == nameof(ComputerScience));
    public Department BusinessAdministration { get; set; } = items.First(x => x.Title!.Replace(" ", string.Empty) == nameof(BusinessAdministration));
    public Department MechanicalEngineering { get; set; } = items.First(x => x.Title!.Replace(" ", string.Empty) == nameof(MechanicalEngineering));
    public Department Biology { get; set; } = items.First(x => x.Title!.Replace(" ", string.Empty) == nameof(Biology));
    public Department Psychology { get; set; } = items.First(x => x.Title!.Replace(" ", string.Empty) == nameof(Psychology));

    public IList<Department> All => [ComputerScience, BusinessAdministration, MechanicalEngineering, Biology, Psychology];
}