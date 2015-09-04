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
