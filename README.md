# PagedListOptimizations

A first stab at trying to optimize PagedList for **Entity Framework**. Currently `PagedList` will make **two** network calls to retrieve the paged result. This can be less than ideal, especially in situations where network latency is the biggest cost the application has when dealing with performance. This is my findings from using `EntityFramework.Extented` and utilizing the `FutureQuery` construct.

![](https://www.evernote.com/l/AA9PcZfUtSBB6KPd66rjji4TX04iRWsoxwkB/image.png)

At first glance, this is very promising. We were able to reduce the networks calls from **two** to **one**. If your database is in a different region than your applicatoin then it may be worth going this route. 

Utilizing `StopWatch` around the two iterations of our `PagedList` impelementations I received this result.

![](https://www.evernote.com/l/AA8hX49oYkZHd54SJh6J7vljfQQIjnIkLe8B/image.png)

> ~ 55 ms (original)

> ~ 536 ms ("optimized")

Pretty disappointing, but there are a few things to keep in mind.

- The test results were run locally, where network latency is less of an issue.
- Why is materializing a `FutureQuery` so expensive as compared to a regular query?
- There may be a way to accomplish a paged result without the need to batch queries with `FutureQuery`

## Twist

So I wanted to see what it would be like if I just manually did the paging, instead of depending on the extension method helpers. I wrote a few more samples which you can see in the code. This is the result.

![](https://www.evernote.com/l/AA-DQecQhL5FrJ8YXdXeeYygqYV24-9EvtMB/image.png)

```
original : 27.2981 ms | pageCount: 10 | total 1000
optimized : 585.0301 ms | pageCount: 10 | total 1000
manual : 3.9125 ms | pageCount: 10 | total 1000
future (static) : 1.8939 ms | pageCount: 10 | total 1000
```

WTF! This is crazy. Time to pull out the profiler and figure out where there is a problem in `ToPagedList`. That, or my tests are flawed some how.

## The Code

Here is the code in the readme so far so you don't have to go digging.

```csharp
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

```