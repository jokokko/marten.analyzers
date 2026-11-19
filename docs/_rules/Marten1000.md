---
title: Marten1000
description: Session queried within an iteration
category: Usage
severity: Warning
---

## Cause

Data is queried within an iteration (`while`, `do-while`, `foreach`, `await foreach`, `for`) through `IQuerySession` (`LoadAsync`, `LoadManyAsync`, `Query`, `QueryAsync`, `QueryForNonStaleData`, `QueryByPlanAsync`, `CheckExistsAsync`, `MetadataForAsync`, `StreamJson`, `StreamJsonOne`, `StreamJsonMany`, `ToJsonOne`, `ToJsonMany`, full text searches such as `SearchAsync`), `IJsonLoader` (`FindByIdAsync`, `StreamById` of `session.Json`), `IAdvancedSql` (`QueryAsync`, `StreamAsync`)
or `IBatchedQuery` (`Load`, `LoadMany`, `Query`, `QueryByPlan`).

Not reported: the query providing the items iterated (`foreach (var issue in await session.Query<Issue>().ToListAsync())`).

## Reason for rule

Queries within an iteration can indicate *Select N+1 issues* (see, [What is N+1 SELECT query issue?](https://stackoverflow.com/questions/97197/what-is-n1-select-query-issue)). This rule aids in locating such sites, supporting manual code review of violations.

## How to fix violations

To fix a violation of this rule, [joins](https://martendb.io/documents/querying/linq/include.html) can be used to load related data. Otherwise remodeling relations in a more document-oriented fashion might be considered.

## Examples

### Violates

```csharp
var dict = new Dictionary<Guid, User>();

var issues = await session.Query<Issue>().Where(x => x.AssigneeId != null).ToListAsync();

// Each issue generates an additional query
foreach (var i in issues)
{
	var user = await session.LoadAsync<User>(i.AssigneeId.Value);
	dict.Add(user.Id, user);
}
```

### Does not violate

```csharp
// Issues with assignees are fetched in one round-trip.
var dict = new Dictionary<Guid, User>();

await session.Query<Issue>().Include(x => x.AssigneeId, dict).Where(x => x.AssigneeId != null).ToListAsync();
```
