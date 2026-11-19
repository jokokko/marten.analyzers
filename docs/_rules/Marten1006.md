---
title: Marten1006
description: Session accessed in possibly multithreaded context
category: Usage
severity: Warning
---

## Cause

A session (`Marten.IQuerySession` or anything deriving from it) held in a variable, a parameter, a field or a property is accessed in possibly multithreaded context: by a delegate passed to `Parallel.ForEach`, `Parallel.ForEachAsync` or `ThreadPool.QueueUserWorkItem`. Any access counts, e.g. `LoadAsync`, `Store`, `Events`.

Not reported: a session opened by the delegate, or one providing the items iterated (`Parallel.ForEach(await session.LoadManyAsync<Issue>(ids), ...)`).

## Reason for rule

Document session is not threadsafe.

## How to fix violations

Construct sessions per consuming site.

## Examples

### Violates

```csharp
await Parallel.ForEachAsync(issueIds, async (id, token) => {
    var issue = await session.LoadAsync<Issue>(id, token);
});
```

### Does not violate

```csharp
await Parallel.ForEachAsync(issueIds, async (id, token) => {
    await using (var session = store.LightweightSession())
    {
        var issue = await session.LoadAsync<Issue>(id, token);
    }
});
```
