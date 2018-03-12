---
title: Marten1006
description: Session accessed in possibly multithreaded context
category: Usage
severity: Warning
---

## Cause

Document session (`Marten.IDocumentSession`) is accessed in possibly multithreaded context (`Parallel.ForEach`, `ThreadPool.QueueUserWorkItem` capture session).

## Reason for rule

Document session is not threadsafe.

## How to fix violations

Construct sessions per consuming site.

## Examples

### Violates

```csharp
ThreadPool.QueueUserWorkItem(_ => {
    var issues = session.LoadMany<Issue>("Issue-1", "Issue-2");
})
```

### Does not violate

```csharp
ThreadPool.QueueUserWorkItem(_ => {
    using (var session = store.OpenSession())
    {
        var issues = session.LoadMany<Issue>("Issue-1", "Issue-2");
    }
});
```
