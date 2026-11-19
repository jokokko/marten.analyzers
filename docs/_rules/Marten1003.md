---
title: Marten1003
description: Possible site for SQL injection
category: Usage
severity: Warning
---

## Cause

String concatenation, string interpolation or `string.Format` is used to build SQL (`IQuerySession.QueryAsync`, `IQuerySession.StreamJson`, `IAdvancedSql.QueryAsync`, `IAdvancedSql.StreamAsync`, `IBatchedQuery.Query`, `IDocumentOperations.QueueSqlCommand`, `MatchesSql`, `MatchesJsonPath`, `OrderBySql`, `ThenBySql`, `ExplicitSql`) from a value not known at compile time. Local variables are followed.

Not reported: SQL made of compile time values only, i.e. literals, constants, `nameof`, enum members and local variables holding nothing else.

## Reason for rule

Building a SQL query through string concatenation can create a site for [SQL injection](https://en.wikipedia.org/wiki/SQL_injection) whenever user input is used. String concatenation can have adverse performance effects in execution of the query (execution plan caching).

## How to fix violations

Use parameterized queries. See [Querying with Postgresql SQL](https://martendb.io/documents/querying/sql.html) for more.

## Examples

### Violates

```csharp
var assigneeId = FromUserInput();
// assigneeId = "'dontcare' or 1 = 1 ; drop table mt_doc_issue ;";
var issues = await session.QueryAsync<Issue>("where data ->> 'AssigneeId' = " + assigneeId);
```

```csharp
var assigneeId = FromUserInput();
var issues = await session.QueryAsync<Issue>($"where data ->> 'AssigneeId' = {assigneeId}");
```

```csharp
var assigneeId = FromUserInput();
var sql = "where true";

if (assigneeId != null)
{
    sql += " and data ->> 'AssigneeId' = " + assigneeId;
}

var issues = await session.QueryAsync<Issue>(sql);
```

### Does not violate

```csharp
var assigneeId = FromUserInput();
var issues = await session.QueryAsync<Issue>("where data ->> 'AssigneeId' = ?", assigneeId);
```

```csharp
var assigneeId = FromUserInput();
var issues = await session.QueryAsync<Issue>($"where data ->> '{nameof(Issue.AssigneeId)}' = ?", assigneeId);
```

```csharp
var assigneeId = FromUserInput();
var sql = "where true";

if (assigneeId != null)
{
    sql += " and data ->> 'AssigneeId' = ?";
}

var issues = await session.QueryAsync<Issue>(sql, assigneeId);
```
