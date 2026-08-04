using Testing.Library.Contoso;

namespace Testing.Library.Data;

public class Courses(params Course[] items)
{
    public Course GoingToTheMoon { get; set; } = items.First(x => x.Title!.Replace(" ", string.Empty).Equals(nameof(GoingToTheMoon), StringComparison.InvariantCultureIgnoreCase));
    public Course BefriendingChina { get; set; } = items.First(x => x.Title!.Replace(" ", string.Empty).Equals(nameof(BefriendingChina), StringComparison.InvariantCultureIgnoreCase));
    public Course Cheating { get; set; } = items.First(x => x.Title == "How to cheat");

    public Course ArtificialIntelligence { get; set; } = items.First(x => x.Title!.Replace(" ", string.Empty).Equals(nameof(ArtificialIntelligence), StringComparison.InvariantCultureIgnoreCase));
    public Course DataStructures { get; set; } = items.First(x => x.Title == "Data Structures and Algorithms");
    public Course Genetics { get; set; } = items.First(x => x.Title!.Replace(" ", string.Empty).Equals(nameof(Genetics), StringComparison.InvariantCultureIgnoreCase));
    public Course Ecology { get; set; } = items.First(x => x.Title!.Replace(" ", string.Empty).Equals(nameof(Ecology), StringComparison.InvariantCultureIgnoreCase));
    public Course MentalDisorders { get; set; } = items.First(x => x.Title!.Replace(" ", string.Empty).Equals(nameof(MentalDisorders), StringComparison.InvariantCultureIgnoreCase));

    public IList<Course> All =>
    [
        GoingToTheMoon, BefriendingChina, Cheating,
        ArtificialIntelligence, DataStructures,
        Genetics, Ecology,
        MentalDisorders
    ];
}