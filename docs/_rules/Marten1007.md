---
title: Marten1007
description: Tasks closes over session
category: Usage
severity: Warning
---

## Cause

Task (`new Task(...)`, `Task task = new(...)`, `Task.Factory.StartNew`, `Task.Run`) closes over session (`Marten.IQuerySession` or anything deriving from it): a local variable, a parameter, or a field or a property used by the task.

Not reported: a method passed in place of a lambda (`Task.Run(Work)`).

## Reason for rule

Document session is not threadsafe.

## How to fix violations

Construct sessions per consuming site.

## Examples

### Violates

```csharp
var issues = Task.Run(async () => {
    return await session.LoadManyAsync<Issue>("Issue-1", "Issue-2");
});
```

```csharp
public class IssueHandler(IQuerySession session)
{
    public Task<IReadOnlyList<Issue>> Handle()
    {
        return Task.Run(async () => {
            return await session.LoadManyAsync<Issue>("Issue-1", "Issue-2");
        });
    }
}
```

```csharp
public class IssueHandler
{
    private readonly IQuerySession session;

    public IssueHandler(IQuerySession session)
    {
        this.session = session;
    }

    public Task<IReadOnlyList<Issue>> Handle()
    {
        return Task.Run(async () => {
            return await session.LoadManyAsync<Issue>("Issue-1", "Issue-2");
        });
    }
}
```

### Does not violate

```csharp
var issues = Task.Run(async () => {
    await using (var session = store.LightweightSession())
    {
        return await session.LoadManyAsync<Issue>("Issue-1", "Issue-2");
    }
});
```
