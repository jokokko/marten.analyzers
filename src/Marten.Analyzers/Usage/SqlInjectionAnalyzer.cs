using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Marten.Analyzers.Infrastructure;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Marten.Analyzers.Usage
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class SqlInjectionAnalyzer : MartenInvocationAnalyzer
	{
		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
			ImmutableArray.Create(Descriptors.Marten1003SqlInjection);

		public SqlInjectionAnalyzer() : base(
			"IQuerySession.QueryAsync",
			"IQuerySession.StreamJson",
			// QueryAsync(Type type, string sql, ...)
			"QuerySessionExtensions.QueryAsync",
			"IAdvancedSql.QueryAsync",
			"IAdvancedSql.StreamAsync",
			"IBatchedQuery.Query",
			"IDocumentOperations.QueueSqlCommand",
			"MatchesSqlExtensions.MatchesSql",
			"MatchesSqlExtensions.MatchesJsonPath",
			"QueryableExtensions.OrderBySql",
			"QueryableExtensions.ThenBySql",
			"QueryableExtensions.ExplicitSql")
		{
		}

		protected override void Analyze(SyntaxNodeAnalysisContext context,
			InvocationExpressionSyntax invocationExpressionSyntax,
			IMethodSymbol methodSymbol)
		{
			// Not always the first: QueryAsync<T>(char placeholder, string sql, ...)
			var p = methodSymbol.Parameters.FirstOrDefault(x => x.Type.SpecialType == SpecialType.System_String && x.Name.Equals("sql", StringComparison.Ordinal));

			if (p == null)
			{
				return;
			}

			var arg = invocationExpressionSyntax.ArgumentFor(p);

			if (arg == null || (new Composition(context).Describe(arg.Expression) & Sql.Built) == 0 || context.CancellationToken.IsCancellationRequested)
			{
				return;
			}

			context.ReportDiagnostic(Diagnostic.Create(
				SupportedDiagnostics[0],
				invocationExpressionSyntax.GetLocation(), SymbolDisplay.ToDisplayString(methodSymbol,
					SymbolDisplayFormat.CSharpShortErrorMessageFormat.WithParameterOptions(
						SymbolDisplayParameterOptions.None))));

		}

		[Flags]
		private enum Sql
		{
			None = 0,
			// Known at compile time
			Text = 1,
			// Not known at compile time
			Input = 2,
			// Text combined with input
			Built = 4
		}

		private sealed class Composition
		{
			private readonly SyntaxNodeAnalysisContext context;

			// Locals followed so far. Guards against cycles: var a = b; b = a;
			private readonly Dictionary<ISymbol, Sql> locals = new Dictionary<ISymbol, Sql>(SymbolEqualityComparer.Default);

			public Composition(SyntaxNodeAnalysisContext context)
			{
				this.context = context;
			}

			public Sql Describe(ExpressionSyntax expression)
			{
				expression = Unwrap(expression);

				if (IsConstant(expression))
				{
					return HasText(expression) ? Sql.Text : Sql.None;
				}

				switch (expression)
				{
					// $"{sql}"
					case InterpolatedStringExpressionSyntax interpolated when interpolated.Contents.Count == 1 && interpolated.Contents[0] is InterpolationSyntax only:
						return Describe(only.Expression);
					case InterpolatedStringExpressionSyntax interpolated:
						return Combined(interpolated.Contents.Select(x => x is InterpolationSyntax hole ? Describe(hole.Expression) : Sql.Text));
					case BinaryExpressionSyntax concatenation when concatenation.IsKind(SyntaxKind.AddExpression):
						return Combined(Operands(concatenation).Select(Describe));
					case BinaryExpressionSyntax coalesce when coalesce.IsKind(SyntaxKind.CoalesceExpression):
						return Either(new[] { Describe(coalesce.Left), Describe(coalesce.Right) });
					case ConditionalExpressionSyntax conditional:
						return Either(new[] { Describe(conditional.WhenTrue), Describe(conditional.WhenFalse) });
					case InvocationExpressionSyntax invocation when IsFormat(invocation, out var formatted):
						return Combined(formatted.Select(Describe));
					case IdentifierNameSyntax identifier when context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol is ILocalSymbol local:
						return DescribeLocal(local);
					default:
						return Sql.Input;
				}
			}

			// Parts of one text: text next to input is built from it
			private static Sql Combined(IEnumerable<Sql> parts)
			{
				const Sql both = Sql.Text | Sql.Input;
				var all = Either(parts);

				return (all & both) == both ? all | Sql.Built : all;
			}

			// Alternatives of the text
			private static Sql Either(IEnumerable<Sql> parts)
			{
				return parts.Aggregate(Sql.None, (all, x) => all | x);
			}

			private bool IsConstant(ExpressionSyntax expression)
			{
				expression = Unwrap(expression);

				if (context.SemanticModel.GetConstantValue(expression, context.CancellationToken).HasValue)
				{
					return true;
				}

				switch (expression)
				{
					// $"limit {Limit}" is no constant to the compiler, Limit being a const int
					case InterpolatedStringExpressionSyntax interpolated:
						return interpolated.Contents.OfType<InterpolationSyntax>().All(x => IsConstant(x.Expression));
					case ConditionalExpressionSyntax conditional:
						return IsConstant(conditional.WhenTrue) && IsConstant(conditional.WhenFalse);
					default:
						return false;
				}
			}

			private bool HasText(ExpressionSyntax expression)
			{
				return context.SemanticModel.GetConstantValue(expression, context.CancellationToken).Value is string
				       || expression.DescendantNodesAndSelf().Any(x => x is InterpolatedStringTextSyntax || x.IsKind(SyntaxKind.StringLiteralExpression));
			}

			// Arguments of string.Format, save for a format provider
			private bool IsFormat(InvocationExpressionSyntax invocation, out ExpressionSyntax[] formatted)
			{
				formatted = null;

				var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;

				if (method?.ContainingType == null || method.ContainingType.SpecialType != SpecialType.System_String || !method.Name.Equals("Format", StringComparison.Ordinal))
				{
					return false;
				}

				var provider = method.Parameters.FirstOrDefault(x => x.Name.Equals("provider", StringComparison.Ordinal));
				var providerArgument = provider == null ? null : invocation.ArgumentFor(provider);

				formatted = invocation.ArgumentList.Arguments.Where(x => x != providerArgument).Select(x => x.Expression).ToArray();

				return true;
			}

			private Sql DescribeLocal(ILocalSymbol local)
			{
				if (locals.TryGetValue(local, out var known))
				{
					return known;
				}

				locals[local] = Sql.None;

				return locals[local] = Follow(local);
			}

			private Sql Follow(ILocalSymbol local)
			{
				var declarator = local.DeclaringSyntaxReferences.Select(x => x.GetSyntax(context.CancellationToken)).OfType<VariableDeclaratorSyntax>().FirstOrDefault();

				// E.g. the variable of a foreach or of a pattern
				if (declarator == null)
				{
					return Sql.Input;
				}

				// The declaring member, or the file for top level statements
				var scope = declarator.Ancestors().FirstOrDefault(x => x is MemberDeclarationSyntax && !(x is GlobalStatementSyntax))
				            ?? declarator.SyntaxTree.GetRoot(context.CancellationToken);

				bool IsLocal(ExpressionSyntax expression)
				{
					return Unwrap(expression) is IdentifierNameSyntax name
					       && name.Identifier.ValueText.Equals(local.Name, StringComparison.Ordinal)
					       && SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol, local);
				}

				bool Reads(ExpressionSyntax expression)
				{
					return expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(IsLocal);
				}

				var assigned = new List<ExpressionSyntax>();
				var appended = new List<ExpressionSyntax>();
				// Build(out sql), (sql, count) = Build()
				var given = false;

				if (declarator.Initializer != null)
				{
					assigned.Add(declarator.Initializer.Value);
				}

				foreach (var node in scope.DescendantNodes())
				{
					switch (node)
					{
						case AssignmentExpressionSyntax assignment when IsLocal(assignment.Left):
							switch (assignment.Kind())
							{
								// sql = sql + ... appends
								case SyntaxKind.SimpleAssignmentExpression:
									(Reads(assignment.Right) ? appended : assigned).Add(assignment.Right);
									break;
								case SyntaxKind.AddAssignmentExpression:
									appended.Add(assignment.Right);
									break;
								case SyntaxKind.CoalesceAssignmentExpression:
									assigned.Add(assignment.Right);
									break;
								default:
									given = true;
									break;
							}

							break;
						case AssignmentExpressionSyntax deconstruction when deconstruction.Left is TupleExpressionSyntax tuple && Reads(tuple):
							given = true;
							break;
						case ArgumentSyntax argument when (argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword) || argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)) && IsLocal(argument.Expression):
							given = true;
							break;
					}
				}

				var all = Either(assigned.Concat(appended).Select(Describe).Concat(new[] { given ? Sql.Input : Sql.None }));

				// var sql = "where x = "; sql += input;
				return appended.Count > 0 ? Combined(new[] { all }) : all;
			}

			private static IEnumerable<ExpressionSyntax> Operands(ExpressionSyntax expression)
			{
				expression = Unwrap(expression);

				return expression is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.AddExpression)
					? Operands(binary.Left).Concat(Operands(binary.Right))
					: new[] { expression };
			}

			// (sql), sql!
			private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
			{
				while (true)
				{
					switch (expression)
					{
						case ParenthesizedExpressionSyntax parenthesized:
							expression = parenthesized.Expression;
							continue;
						case PostfixUnaryExpressionSyntax suppressed when suppressed.IsKind(SyntaxKind.SuppressNullableWarningExpression):
							expression = suppressed.Operand;
							continue;
						default:
							return expression;
					}
				}
			}
		}
	}
}
