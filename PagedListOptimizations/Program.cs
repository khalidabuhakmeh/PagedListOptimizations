using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using EntityFramework.Extensions;
using My.Hydrator;
using My.Hydrator.Conventions;
using PagedList;
using RimDev.Automation.Sql;

namespace PagedListOptimizations
{
    class Program
    {
        static void Main(string[] args)
        {
            var hydrator = new Hydrator();

            hydrator.Configure(x =>
            {
                x.For<Person>().Property(y => y.Id).Skip();
                x.For<Person>().Property(y => y.FirstName).Is(CommonType.AmericanName);
                x.For<Person>().Property(y => y.LastName).Is(CommonType.AmericanLastName);
                x.For<Person>().Property(y => y.Weight).Use(new IntTypeConvention(99, 250));
                x.For<Person>().Property(y => y.Height).Use(new IntTypeConvention(100, 999));
            });

            var people = Enumerable.Range(1, 1000)
                .Select(x => hydrator.Hydrate<Person>())
                .ToList();

            IPagedList<Person> original;
            IPagedList<Person> optimized;
            IPagedList<Person> manual;
            IPagedList<Person> future;

            using (var database = new LocalDb())
            {
                var connectionString = database.ConnectionString + "MultipleActiveResultSets=True;";

                Context.Load(connectionString, people);

                var stopwatch = new Stopwatch();
                
                using (var context = new Context(connectionString))
                {
                    var query = context.Persons.OrderBy(x => x.Id).AsNoTracking();
                    stopwatch.Start();
                    original = query.ToPagedList(1, 100);
                    stopwatch.Stop();
                    Print("original", original, stopwatch);
                }

                using (var context = new Context(database.ConnectionString))
                {
                    var query = context.Persons.OrderBy(x => x.Id).AsNoTracking();
                    stopwatch.Restart();
                    optimized = new EntityFrameworkPagedList<Person>(query, 1, 100);
                    stopwatch.Stop();
                    Print("optimized", optimized, stopwatch);
                }

                using (var context = new Context(database.ConnectionString))
                {
                    var query = context.Persons.OrderBy(x => x.Id).AsNoTracking();
                    stopwatch.Restart();
                    manual = new StaticPagedList<Person>(query.Skip(0).Take(100).ToList(), 1, 100, query.Count()) ;
                    stopwatch.Stop();
                    Print("manual", manual, stopwatch);
                }

                using (var context = new Context(database.ConnectionString))
                {
                    var query = context.Persons.OrderBy(x => x.Id).AsNoTracking();

                    var items = query.Skip(0).Take(100).Future();
                    var total = query.FutureCount();

                    stopwatch.Restart();
                    future = new StaticPagedList<Person>(items, 1, 100, total);
                    stopwatch.Stop();
                    Print("future (static)", future, stopwatch);
                }
            }

            Console.ReadLine();
        }

        public static void Print(string title, IPagedList list, Stopwatch stopwatch)
        {
            Console.WriteLine("{0} : {1} ms | pageCount: {2} | total {3} ",
                title,
                stopwatch.Elapsed.TotalMilliseconds,
                list.PageCount,
                list.TotalItemCount);
        }
    }



    public class Context : DbContext
    {
        public Context(string connectionString)
            : base(connectionString)
        {}

        public DbSet<Person> Persons { get; set; }

        public static void Load(string connectionString, IEnumerable<Person> people)
        {
            using (var context = new Context(connectionString))
            {
                context.Persons.AddRange(people);
                context.SaveChanges();
            }
        }
    }

    public class Person
    {
        public int Id { get; set; }
        [DataType(DataType.PhoneNumber)]
        public string Phone { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public int Weight { get; set; }

        public int Height { get; set; }

        public DateTime? DateOfBirth { get; set; }
    }

    [Serializable]
    public class EntityFrameworkPagedList<T> : BasePagedList<T> where T : class
    {
        public EntityFrameworkPagedList(IQueryable<T> superset, int pageNumber, int pageSize)
        {
            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException("pageNumber", pageNumber, "PageNumber cannot be below 1.");
            if (pageSize < 1)
                throw new ArgumentOutOfRangeException("pageSize", pageSize, "PageSize cannot be less than 1.");

            var result = superset.Skip((pageNumber - 1)*pageSize).Take(pageSize).Future();

            // execute both queries
            // set source to blank list if superset is null to prevent exceptions
            TotalItemCount = superset.FutureCount();

            PageSize = pageSize;
            PageNumber = pageNumber;
            PageCount = TotalItemCount > 0
                        ? (int)Math.Ceiling(TotalItemCount / (double)PageSize)
                        : 0;
            HasPreviousPage = PageNumber > 1;
            HasNextPage = PageNumber < PageCount;
            IsFirstPage = PageNumber == 1;
            IsLastPage = PageNumber >= PageCount;
            FirstItemOnPage = (PageNumber - 1) * PageSize + 1;
            var numberOfLastItemOnPage = FirstItemOnPage + PageSize - 1;
            LastItemOnPage = numberOfLastItemOnPage > TotalItemCount
                            ? TotalItemCount
                            : numberOfLastItemOnPage;

            // add items to internal list
            if (superset != null && TotalItemCount > 0)
                Subset.AddRange(result);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:PagedList.PagedList`1"/> class that divides the supplied superset into subsets the size of the supplied pageSize. The instance then only containes the objects contained in the subset specified by index.
        /// 
        /// </summary>
        /// <param name="superset">The collection of objects to be divided into subsets. If the collection implements <see cref="T:System.Linq.IQueryable`1"/>, it will be treated as such.</param><param name="pageNumber">The one-based index of the subset of objects to be contained by this instance.</param><param name="pageSize">The maximum size of any individual subset.</param><exception cref="T:System.ArgumentOutOfRangeException">The specified index cannot be less than zero.</exception><exception cref="T:System.ArgumentOutOfRangeException">The specified page size cannot be less than one.</exception>
        public EntityFrameworkPagedList(IEnumerable<T> superset, int pageNumber, int pageSize)
          : this(superset.AsQueryable<T>(), pageNumber, pageSize)
        {
        }
    }

}
