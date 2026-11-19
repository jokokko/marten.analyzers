---
title: Marten1004
description: Possible premature query materialization
category: Usage
severity: Warning
---

## Cause

A query (`IQuerySession.Query`, `IQuerySession.QueryForNonStaleData`, `IBatchedQuery.Query`) is materialized (`ToListAsync`, `ToAsyncEnumerable`, `ToList`, `ToArray`, `AsEnumerable`) without any predicates.

## Reason for rule

Materializing the query before applying any predicates leads further evaluation of any predicates to be executed in memory of the querying process.

**Note:** as of Marten 9, only asynchronous data access is supported. Materializing a LINQ query of a session synchronously (`ToList`, `ToArray`, or enumerating the result of `AsEnumerable`) compiles, but throws a `NotSupportedException` when executed.

## How to fix violations

Apply predicates before materializing the query.

## Examples

### Violates

```csharp
// Fetch all issues & evaluate predicate in memory
var issues = (await session.Query<Issue>().ToListAsync()).Where(x => x.Critical);
```

### Does not violate

```csharp
// Evaluate predicate in database, then materialize matches
var issues = await session.Query<Issue>().Where(x => x.Critical).ToListAsync();
```
