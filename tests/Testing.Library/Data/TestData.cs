using Testing.Library.Contoso;

namespace Testing.Library.Data;

public static class TestData
{
    public static (People people, Departments departments, Courses courses) Generate()
    {
        var people = GetPeople();
        var departments = GetDepartments(people);
        var courses = GetCourses(people, departments);

        return (people, departments, courses);
    }

    public static People GetPeople()
    {
        Student john = new()
        {
            GivenName = "John",
            LastName = "Doe",
            Description = "This is a male test person",
            Email = "john.doe@email.com"
        };
        Student jane = new()
        {
            GivenName = "Jane",
            LastName = "Doe",
            Description = "This is a female test person",
            Phone = "001 234 567 890"
        };
        Student francois = new()
        {
            GivenName = "François",
            LastName = "Du sacre Cœur",
            Description = "Le poète parisien du xiiie siècle Rutebeuf se fait gravement l'écho de la faiblesse humaine, de l'incertitude et de la pauvreté à l'opposé des valeurs courtoises. Crème brûlée"
        };
        Instructor bob = new()
        {
            GivenName = "Robert",
            LastName = "Kennedy",
            Description = "He's an American politician and lawyer, known for his roles as U.S. Attorney General and Senator, his advocacy for civil rights and social justice, and his tragic assassination in 1968 while campaigning for the presidency.",
        };
        Instructor bill = new()
        {
            GivenName = "Richard",
            LastName = "Nixon",
            Description = "He's the 37th President of the United States, remembered for his foreign policy achievements and his involvement in the Watergate scandal, which led to his resignation in 1974.",
        };
        Instructor david = new()
        {
            GivenName = "David",
            LastName = "Attenborough",
            Description = "He's naturalist, and documentary filmmaker known for his captivating narration and lifelong dedication to exploring and preserving the natural world.",
        };
        return new People(john, jane, francois, bob, bill, david);
    }
    public static Departments GetDepartments(People people)
    {
        Department computerScience = new()
        {
            Title = "Computer Science",
            Administrator = people.Bob
        };
        Department businessAdministration = new()
        {
            Title = "Business Administration",
            Administrator = people.Bob
        };
        Department mechanicalEngineering = new()
        {
            Title = "Mechanical Engineering",
            Administrator = people.Bill
        };
        Department biology = new()
        {
            Title = "Biology",
            Administrator = people.David
        };
        Department psychology = new()
        {
            Title = "Psychology",
            Administrator = people.David
        };

        return new Departments(computerScience, businessAdministration, mechanicalEngineering, biology, psychology);
    }
    public static Courses GetCourses(People people, Departments departments)
    {
        Course goingToTheMoon = new()
        {
            Title = "Going to the moon",
            Instructors = [people.Bob]
        };
        Course befriendingChina = new()
        {
            Title = "Befriending China",
            Instructors = [people.Bill]
        };
        Course cheating = new()
        {
            Title = "How to cheat",
            Instructors = [people.Bill]
        };

        Course artificialIntelligence = new()
        {
            Title = "Artificial Intelligence",
            Department = departments.ComputerScience,
            Instructors = [people.Bob],
            Credits = 2
        };
        Course dataStructures = new()
        {
            Title = "Data Structures and Algorithms",
            Department = departments.ComputerScience,
            Instructors = [people.Bob],
            Credits = 3
        };
        Course genetics = new()
        {
            Title = "Genetics",
            Department = departments.Biology,
            Instructors = [people.David],
            Credits = 3
        };
        Course ecology = new()
        {
            Title = "Ecology",
            Department = departments.Biology,
            Instructors = [people.David],
            Credits = 5
        };
        Course mentalDisorders = new()
        {
            Title = "Mental disorders",
            Department = departments.Psychology,
            Instructors = [people.Bill, people.Bob],
            Credits = 4
        };

        return new Courses(goingToTheMoon, befriendingChina, cheating, artificialIntelligence, dataStructures, genetics, ecology, mentalDisorders);
    }
}