---
title: Marten1003
description: Possible site for SQL injection
category: Usage
severity: Warning
---

## Cause

String concatenation is used to build a SQL query (`IQuerySession.Query`, `IQuerySession.QueryAsync`, `IBatchedQuery.Query`).

## Reason for rule

Building a SQL query through string concatenation can create a site for [SQL injection](https://en.wikipedia.org/wiki/SQL_injection) whenever user input is used. String concatenation can have adverse performance effects in execution of the query (execution plan caching).

## How to fix violations

Use parameterized queries. See [Querying with Postgresql SQL](http://jasperfx.github.io/marten/documentation/documents/querying/sql/) for more.

## Examples

### Violates

```csharp
var assigneeId = FromUserInput();
// assigneeId = "'dontcare' or 1 = 1 ; drop table mt_doc_issue ;";
var user = session.Query<Issue>("where data ->> 'AssigneeId' = " + assigneeId);
```

### Does not violate

```csharp
var assigneeId = FromUserInput();
var user = session.Query<Issue>("where data ->> 'AssigneeId' = ?", assigneeId);
```
