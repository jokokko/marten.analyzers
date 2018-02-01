---
title: Marten1000
description: Session queried within	an iteration
category: Usage
severity: Warning
---

## Cause

Data is queried within an iteration (`while`, `do-while`, `foreach`, `for`) through `IQuerySession` (`Load`, `LoadAsync`, `LoadMany`, `LoadManyAsync`, `Query`, `QueryAsync`).

## Reason for rule

Queries within an iteration can indicate *Select N+1 issues* (see, [What is N+1 SELECT query issue?](https://stackoverflow.com/questions/97197/what-is-n1-select-query-issue)). This rule aids in locating such sites, supporting manual code review of violations.

## How to fix violations

To fix a violation of this rule, [joins](http://jasperfx.github.io/marten/documentation/documents/querying/include/) can be used to load related data. Otherwise remodeling relations in a more document-oriented fashion might be considered.

## Examples

### Violates

```csharp
var dict = new Dictionary<Guid, User>();

var issues = query.Query<Issue>();

// Each issue generates an additional query
foreach (var i in issues)
{
	var user = query.Load<User>(i.AssigneeId.Value);
	dict.Add(user.Id, user);
}
```

### Does not violate

```csharp
// Issues with assignees are fetched in one round-trip.
var dict = new Dictionary<Guid, User>();

query.Query<Issue>().Include(x => x.AssigneeId, dict).ToArray();
```
