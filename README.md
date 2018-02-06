# Marten.Analyzers [![Build status](https://ci.appveyor.com/api/projects/status/hea16xybd755txv7?svg=true)](https://ci.appveyor.com/project/jokokko/marten-analyzers) [![NuGet Version](http://img.shields.io/nuget/v/Marten.Analyzers.svg?style=flat)](https://www.nuget.org/packages/Marten.Analyzers/)
Code Analyzers for [Marten](http://jasperfx.github.io/marten/).

**Package** [Marten.Analyzers](https://www.nuget.org/packages/Marten.Analyzers) | **Platforms** .NET 4.6, .NET Standard 1.3

For rules, see https://jokokko.github.io/marten.analyzers/.

Remarks: The current analyzers exist to identify Select N+1 candidates to aid myself in code reviews.

![Warning on a SELECT N+1 issue candidate](assets/selectn1sample.png)

Note: This is a contributor project.

Some of the structure (docs, unit test helper) is copied from [xunit.analyzers](https://github.com/xunit/xunit.analyzers).