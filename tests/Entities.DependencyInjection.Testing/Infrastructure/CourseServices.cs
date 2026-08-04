using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Services;
using Regira.Entities.Services.Abstractions;
using Testing.Library.Contoso;

namespace Entities.DependencyInjection.Testing.Infrastructure;

public interface ICourseService3A : IEntityService<Course, int, SearchObject<int>>;
public interface ICourseService3B : IEntityService<Course, int, CourseSearchObject>;
public interface ICourseService5 : IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>, ICourseService3B;

// Repositories
public class CourseRepository1(IEntityReadService<Course, int, SearchObject<int>> readService, IEntityWriteService<Course, int> writeService)
    : EntityRepository<Course>(readService, writeService), ICourseService3A;

public class CourseRepository2(IEntityReadService<Course, int, SearchObject<int>> readService, IEntityWriteService<Course, int> writeService)
    : EntityRepository<Course, int>(readService, writeService), ICourseService3A;

public class CourseRepository3A(IEntityReadService<Course, int, SearchObject<int>> readService, IEntityWriteService<Course, int> writeService)
    : EntityRepository<Course, int, SearchObject<int>>(readService, writeService), ICourseService3A;

public class CourseRepository3B(IEntityReadService<Course, int, CourseSearchObject> readService, IEntityWriteService<Course, int> writeService)
    : EntityRepository<Course, int, CourseSearchObject>(readService, writeService), ICourseService3B;

public class CourseRepository4(IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes> readService, IEntityWriteService<Course, int> writeService)
    : EntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(readService, writeService), ICourseService5;

public class CourseRepository5(IEntityReadService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes> readService, IEntityWriteService<Course, int> writeService)
    : EntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>(readService, writeService), ICourseService5;

// Managers
public class CourseManager1(IEntityRepository<Course, int, SearchObject<int>> repo)
    : EntityManager<Course>(repo), ICourseService3A;
public class CourseManager2(IEntityRepository<Course, int, SearchObject<int>> repo)
    : EntityManager<Course, int>(repo), ICourseService3A;
public class CourseManager3A(IEntityRepository<Course, int, SearchObject<int>> repo)
    : EntityManager<Course, int, SearchObject<int>>(repo), ICourseService3A;
public class CourseManager3B(IEntityRepository<Course, int, CourseSearchObject> repo)
    : EntityManager<Course, int, CourseSearchObject>(repo), ICourseService3B;
public class CourseManager4(IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes> repo)
    : EntityManager<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(repo), ICourseService5;
public class CourseManager5(IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes> repo)
    : EntityManager<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>(repo), ICourseService5;

// Custom service

public class CourseServiceA(IEntityRepository<Course, int, SearchObject<int>> service)
    : EntityWrappingServiceBase<Course>(service), ICourseService3A;
public class CustomCourseServiceA(ICourseService3A service)
    : EntityWrappingServiceBase<Course>(service);

public class CourseService3B(IEntityRepository<Course, int, CourseSearchObject> service)
    : EntityWrappingServiceBase<Course, int, CourseSearchObject>(service);
public class CustomCourseService3B(ICourseService3B service)
    : EntityWrappingServiceBase<Course, int, CourseSearchObject>(service);

public class CourseService4(IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes> service)
    : EntityWrappingServiceBase<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>(service), IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>;
public class CustomCourseService4(ICourseService5 service)
    : EntityWrappingServiceBase<Course, CourseSearchObject, CourseSortBy, CourseIncludes>(service), IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>;

public class CourseService5(IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes> service)
    : EntityWrappingServiceBase<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>(service);
public class CustomCourseService5(ICourseService5 service)
    : EntityWrappingServiceBase<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>(service);