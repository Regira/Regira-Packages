namespace TreeList.Testing.Infrastructure;

public interface ICookbookItem
{
    int Id { get; set; }
    string Name { get; set; }
}

public class Recipe : ICookbookItem
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>The ingredients used in this recipe (many-to-many).</summary>
    public IList<Ingredient> Ingredients { get; set; } = [];
    public override string ToString() => Name;
}

public class Ingredient : ICookbookItem
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>The sub-ingredients that compose this ingredient (many-to-many).</summary>
    public IList<Ingredient> SubIngredients { get; set; } = [];
    public override string ToString() => Name;
}