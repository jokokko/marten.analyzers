# Marten.Analyzers [![Build status](https://ci.appveyor.com/api/projects/status/hea16xybd755txv7?svg=true)](https://ci.appveyor.com/project/jokokko/marten-analyzers) [![NuGet Version](http://img.shields.io/nuget/v/Marten.Analyzers.svg?style=flat)](https://www.nuget.org/packages/Marten.Analyzers/)
Code Analyzers for [Marten](https://martendb.io/).

**Package** [Marten.Analyzers](https://www.nuget.org/packages/Marten.Analyzers) | **Platforms** .NET Standard 2.0 | **Marten** 9

2.x is for Marten 9, 1.x for Marten 2.x. See [changes](CHANGELOG.md).

```xml
<PackageReference Include="Marten.Analyzers" Version="2.0.0" PrivateAssets="all" />
```

For rules, see https://jokokko.github.io/marten.analyzers/.

Remarks: The current analyzers exist to identify Select N+1 candidates as well as candidates for SQL injections to aid myself in code reviews.

```csharp
public async Task<Dictionary<Department, List<object>>> GetDepartmentsAndPersonnel(IDocumentStore store, string fromUserInput)
{
    await using (var session = store.LightweightSession())
    {
        // fromUserInput: 'dontcare' or 1 = 1 ; drop table mt_doc_department ;
        var departments = await session.QueryAsync<Department>("where data ->> 'Site' = " + fromUserInput); // Marten1003
        var departmentPersonnel = departments.ToDictionary(x => x, x => new List<object>());

        foreach (var d in departmentPersonnel.Keys)
        {
            var people = await session.Query<Person>().Where(p => p.Department == d.Id).ToListAsync(); // Marten1000

            foreach (var p in people)
            {
                var timeCard = await session.Events.AggregateStreamAsync<Timecard>(p.Id); // Marten1001

                var contactDetails = await GetContactDetails(session); // Marten1002

                departmentPersonnel[d].Add(new { Person = p, TimeCard = timeCard, ContactDetails = contactDetails });
            }
        }

        return departmentPersonnel;
    }
}
```

```
warning Marten1003: Possible site for SQL injection in 'IQuerySession.QueryAsync<Department>()'.
warning Marten1000: Session queried within an iteration with 'IQuerySession.Query<Person>()'.
warning Marten1001: Event store queried within an iteration with 'IQueryEventStore.AggregateStreamAsync<Timecard>()'.
warning Marten1002: Session used as a method argument within an iteration in 'Sample.GetContactDetails()'.
```

Note: This is a contributor project.

Some of the structure (docs, unit test helper) is copied from [xunit.analyzers](https://github.com/xunit/xunit.analyzers).