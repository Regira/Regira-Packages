using System.Collections;
using NUnit.Framework.Legacy;
using Regira.Utilities;

namespace Common.Testing.Utilities;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class TypeUtilityTests
{
    #region Models

    public enum TestEnum
    {
        Value1,
        Value2
    }

    public struct TestStruct
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    public interface ITestCore<T>
    {
        T Id { get; set; }
        DateTime Created { get; set; }
        DateTime? LastModified { get; set; }
    }
    public interface ITestWithDescription
    {
        string? Description { get; set; }
    }
    public interface ITestWithParent<T>
    {
        ITestCore<T>? Parent { get; set; }
    }
    public abstract class TestClassBase<T> : ITestCore<T>
    {
        public T Id { get; set; } = default!;
        public string? Title { get; set; }
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime? LastModified { get; set; }
    }

    public class Test1Class : TestClassBase<int>, ITestWithDescription
    {
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public ICollection<TestStruct> Structs { get; set; } = null!;
    }

    public class Test2Class : TestClassBase<string>, ITestWithParent<string>
    {
        public ITestCore<string>? Parent { get; set; }
    }
    #endregion

    [Test]
    public void Test_IsSimpleType()
    {
        Assert.That(TypeUtility.IsSimpleType(typeof(int)), Is.True);
        Assert.That(TypeUtility.IsSimpleType(typeof(string)), Is.True);
        Assert.That(TypeUtility.IsSimpleType(typeof(DateTime)), Is.True);
        Assert.That(TypeUtility.IsSimpleType(typeof(TestEnum)), Is.True);

        Assert.That(TypeUtility.IsSimpleType(typeof(int?)), Is.True);
        Assert.That(TypeUtility.IsSimpleType(typeof(string)), Is.True);
        Assert.That(TypeUtility.IsSimpleType(typeof(DateTime?)), Is.True);
        Assert.That(TypeUtility.IsSimpleType(typeof(TestEnum?)), Is.True);

        ClassicAssert.IsFalse(TypeUtility.IsSimpleType(typeof(TestStruct)));
        ClassicAssert.IsFalse(TypeUtility.IsSimpleType(typeof(TestStruct?)));
        ClassicAssert.IsFalse(TypeUtility.IsSimpleType(typeof(ITestCore<>)));
    }
    [Test]
    public void Test_IsNullableType()
    {
        ClassicAssert.IsFalse(TypeUtility.IsNullableType(typeof(int)));
        ClassicAssert.IsFalse(TypeUtility.IsNullableType(typeof(string)));
        ClassicAssert.IsFalse(TypeUtility.IsNullableType(typeof(DateTime)));
        ClassicAssert.IsFalse(TypeUtility.IsNullableType(typeof(TestEnum)));
        ClassicAssert.IsFalse(TypeUtility.IsNullableType(typeof(TestStruct)));

        Assert.That(TypeUtility.IsNullableType(typeof(int?)), Is.True);
        Assert.That(TypeUtility.IsNullableType(typeof(DateTime?)), Is.True);
        Assert.That(TypeUtility.IsNullableType(typeof(TestEnum?)), Is.True);
        Assert.That(TypeUtility.IsNullableType(typeof(TestStruct?)), Is.True);

        ClassicAssert.IsFalse(TypeUtility.IsNullableType(typeof(ITestCore<>)));
    }

    [Test]
    public void Test_IsEnumerable()
    {
        var aStringCollection = "this string is a collection of chars";
        // ReSharper disable once CollectionNeverQueried.Local
        var aIntCollection = new[] { 1, 2, 3, 4, 5 };
        // ReSharper disable once UseCollectionExpression (removing TestStruct will make test fail)
        var test1Class = new Test1Class { Structs = new TestStruct[] { new(), new() } };

        Assert.That(TypeUtility.IsTypeEnumerable(aStringCollection.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeEnumerable<string>(aStringCollection.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeEnumerable(aStringCollection.GetType(), typeof(IEnumerable<char>)), Is.True);
        ClassicAssert.IsFalse(TypeUtility.IsTypeEnumerable<IList>(aStringCollection.GetType()));

        Assert.That(TypeUtility.IsTypeEnumerable(aIntCollection.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeEnumerable(aIntCollection.GetType(), typeof(int[])), Is.True);
        Assert.That(TypeUtility.IsTypeEnumerable(aIntCollection.GetType(), typeof(IList<int>)), Is.True);

        ClassicAssert.IsFalse(TypeUtility.IsTypeEnumerable(typeof(int)));
        ClassicAssert.IsFalse(TypeUtility.IsTypeEnumerable(typeof(Test1Class)));
        ClassicAssert.IsFalse(TypeUtility.IsTypeEnumerable(typeof(TestStruct)));
        Assert.That(TypeUtility.IsTypeEnumerable(typeof(ArrayList)), Is.True);

        Assert.That(TypeUtility.IsTypeEnumerable(test1Class.Structs.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeEnumerable<IList>(test1Class.Structs.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeEnumerable<TestStruct[]>(test1Class.Structs.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeEnumerable<IList<TestStruct>>(test1Class.Structs.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeEnumerable<ICollection<TestStruct>>(test1Class.Structs.GetType()), Is.True);
        ClassicAssert.IsFalse(TypeUtility.IsTypeEnumerable<List<TestStruct>>(test1Class.Structs.GetType()));
        ClassicAssert.IsFalse(TypeUtility.IsTypeEnumerable<IList<Test1Class>>(test1Class.Structs.GetType()));
    }

    [Test]
    public void Test_IsCollection()
    {
        var aStringCollection = "this string is a collection of chars";
        // ReSharper disable once CollectionNeverQueried.Local
        var aIntCollection = new[] { 1, 2, 3, 4, 5 };
        // ReSharper disable once UseCollectionExpression (removing TestStruct will make test fail)
        var test1Class = new Test1Class { Structs = new TestStruct[] { new (), new() } };

        Assert.That(TypeUtility.IsTypeACollection(aStringCollection.GetType()), Is.True);
        ClassicAssert.IsFalse(TypeUtility.IsTypeACollection(aStringCollection.GetType(), typeof(IList<char>)));
        ClassicAssert.IsFalse(TypeUtility.IsTypeACollection<IList>(aStringCollection.GetType()));

        Assert.That(TypeUtility.IsTypeACollection(aIntCollection.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeACollection(aIntCollection.GetType(), typeof(int[])), Is.True);
        Assert.That(TypeUtility.IsTypeACollection(aIntCollection.GetType(), typeof(IList<int>)), Is.True);

        ClassicAssert.IsFalse(TypeUtility.IsTypeACollection(typeof(int)));
        ClassicAssert.IsFalse(TypeUtility.IsTypeACollection(typeof(Test1Class)));
        ClassicAssert.IsFalse(TypeUtility.IsTypeACollection(typeof(TestStruct)));
        Assert.That(TypeUtility.IsTypeACollection(typeof(ArrayList)), Is.True);

        Assert.That(TypeUtility.IsTypeACollection(test1Class.Structs.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeACollection<IList>(test1Class.Structs.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeACollection<TestStruct[]>(test1Class.Structs.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeACollection<IList<TestStruct>>(test1Class.Structs.GetType()), Is.True);
        Assert.That(TypeUtility.IsTypeACollection<ICollection<TestStruct>>(test1Class.Structs.GetType()), Is.True);
        ClassicAssert.IsFalse(TypeUtility.IsTypeACollection<List<TestStruct>>(test1Class.Structs.GetType()));
        ClassicAssert.IsFalse(TypeUtility.IsTypeACollection<IList<Test1Class>>(test1Class.Structs.GetType()));
    }

    [Test]
    public void Test_BaseTypes()
    {
        var baseTypes = TypeUtility.GetBaseTypes(typeof(Test2Class))
            .Distinct()
            .ToArray();
        var expectedBaseTypes = new[]
        {
            typeof(TestClassBase<string>),
            typeof(ITestWithParent<string>),
            typeof(ITestCore<string>),
            typeof(object)
        };
        Assert.That(baseTypes, Is.Not.Empty);
        Assert.That(baseTypes, Is.EquivalentTo(expectedBaseTypes));
    }

    [Test]
    public void Test_Get_PropertyName_By_Expression()
    {
        var test1Class = new Test1Class { Structs = Array.Empty<TestStruct>() };

        Assert.That(TypeUtility.GetPropertyName(() => test1Class.Price!), Is.EqualTo(nameof(Test1Class.Price)));
        Assert.That(TypeUtility.GetPropertyName<Test1Class>(x => x.Price!), Is.EqualTo(nameof(Test1Class.Price)));
        Assert.That(TypeUtility.GetPropertyName<Test1Class>(x => x.Description!), Is.EqualTo(nameof(Test1Class.Description)));
        Assert.That(TypeUtility.GetPropertyName<Test1Class>(x => x.Structs.First()), Is.EqualTo(null));
        Assert.That(TypeUtility.GetPropertyName<Test1Class>(x => x.Structs.First().Name), Is.EqualTo(nameof(TestStruct.Name)));
    }
}