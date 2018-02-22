---
title: Marten1004
description: Possible premature query materialization
category: Usage
severity: Warning
---

## Cause

A query (`IQuerySession.Query`, `IQuerySession.QueryAsync`, `IBatchedQuery.Query`) is materialized without any predicates.

## Reason for rule

Materializing the query before applying any predicates leads further evaluation of any predicates to be executed in memory of the querying process.

## How to fix violations

Apply predicates before materializing the query.

## Examples

### Violates

```csharp
// Fetch all issues & evaluate predicate in memory
var issues = session.Query<Issue>().ToList().Where(x => x.Critical);
```

### Does not violate

```csharp
// Evaluate predicate in database, then materialize matches
var issues = session.Query<Issue>().Where(x => x.Critical).ToList();
```
