---
title: Marten1005
description: Consider using batched query
category: Usage
severity: Info
---

## Cause

Multiple queries (`Load`, `LoadAsync`, `LoadMany`, `LoadManyAsync`, `Query`, `QueryAsync` of `IQuerySession`) within a method body or expression load data with no interdependencies between the materialized data and query parameters.

## Reason for rule

Queries with no interdependent data can be batched to reduce roundtrips to database.

## How to fix violations

Enlist the quries in `IBatchedQuery`.

## Examples

### Violates

```csharp
// Two roundtrips to database
var issues = session.LoadMany<Issue>("Issue-1", "Issue-2");
var assignee = session.Load<Assignee>("Assignee");
```

### Does not violate

```csharp
var batch = session.CreateBatchQuery();

var issues = batch.LoadMany<Issue>().ById("Issue-1", "Issue-2");
var assignee = batch.Load<Assignee>("Assignee");

// Single roundtrip to database
batch.Execute();
```
