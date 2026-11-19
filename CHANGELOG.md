# Changes

## 2.0.0

Analyzers for Marten 9.

### Breaking

- Rules match the asynchronous API of Marten 9. For the synchronous API of Marten 2.x, stay on 1.x.
- A single .NET Standard 2.0 analyzer built against Roslyn 4.12: requires .NET SDK 9 (Visual Studio 2022 17.12) or later.
- Marten1003 no longer reports SQL made of compile time values only.

### Rules

See the [rules](https://jokokko.github.io/marten.analyzers/) for what is matched.

- Marten1000, Marten1001: queries of Marten 9, e.g. JSON streaming, full text search, `IAdvancedSql`, queries by tags, `FetchForWriting`, `WriteToAggregate`.
- Marten1002, Marten1006, Marten1007: any `IQuerySession`, e.g. the session of a tenant.
- Marten1003: everything that takes SQL. Interpolation and `string.Format`. Local variables are followed.
- Marten1004: `ToListAsync`, `ToAsyncEnumerable`, `QueryForNonStaleData`. Results of SQL queries are no longer considered.
- Marten1006: `Parallel.ForEachAsync`. Any access to the session, e.g. writes. Sessions in parameters, fields and properties.
- Marten1007: `Task.Run`, `Task task = new(...)`. Sessions in parameters, fields and properties.
- Marten1008: registrations of Marten 9, e.g. `Snapshot`, `AddProjectionWithServices`, `Register` of the projection.

### Fixes

- Marten1002, Marten1003: nothing reported by a recent compiler.
- Marten1007: nothing reported with `#nullable enable`. Every task of a method reported once any lambda captured a session.
- Marten1000, Marten1001, Marten1002: the source of a loop reported, e.g. `foreach (var issue in await session.Query<Issue>().ToListAsync())`.
- Marten1005: an assignment nested in a declaration ignored.
- Marten1006: the source of `Parallel.ForEach` reported.
- AD0001 on a `null` argument (Marten1002), a session not held in a variable (Marten1005, Marten1006), a primary constructor in another file (Marten1005), a namespace qualified type name (Marten1006).

### Package

- Development dependency: reference with `PrivateAssets="all"`.
- License as an expression (MIT).
- No symbols package.
