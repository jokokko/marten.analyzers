---
title: Marten1007
description: Tasks closes over session
category: Usage
severity: Warning
---

## Cause

Task (`System.Threading.Tasks.Task.Task`) closes over session (`Marten.IDocumentSession`).

## Reason for rule

Document session is not threadsafe.

## How to fix violations

Construct sessions per consuming site.

## Examples

### Violates

```csharp
var issues = new Task<IEnumerable<Issue>>(() => {		
    return session.LoadMany<Issue>("Issue-1", "Issue-2");
});
```

### Does not violate

```csharp
var issues = new Task<IEnumerable<Issue>>(() => {		
    using (var session = store.OpenSession())
    {
        return session.LoadMany<Issue>("Issue-1", "Issue-2");
    }
});
```
