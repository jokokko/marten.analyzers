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
    public sealed class UseBatchedQueryAnalyzer : MartenAnalyzer, IOnMethodInvocation
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Descriptors.Marten1005UseBatchedQuery);

        public HashSet<string> OnMethods { get; } = new HashSet<string>(new[]
        {
            "IQuerySession.LoadAsync",
            "IQuerySession.LoadManyAsync",
            "IQuerySession.Query",
            "IQuerySession.QueryAsync",
            // Overloads declared in JasperFx.Events, e.g. LoadManyAsync(ids, token)
            "IDocumentReadOperations.LoadAsync",
            "IDocumentReadOperations.LoadManyAsync",
            "IDocumentReadOperations.Query"
        });

        private static readonly string[] EvaluateFollowingInvocationsOn = {
            "Query",
            "QueryAsync"
        };

        protected override void AnalyzeCompilation(CompilationStartAnalysisContext ctx, MartenContext martenCtx)
        {
            ctx.RegisterCodeBlockAction(BlockAnalyzer);
        }

        private sealed class InvocationsInSession
        {
            public InvocationExpressionSyntax Invocation;            
            public ISymbol[] ReadInside;
            public ISymbol Assignment;
        }

        // Index invocations by associated session
        // List sites with no dependent arguments on previous invocation results within the same session.
        private void BlockAnalyzer(CodeBlockAnalysisContext context)
        {
            var invocationsPerSession = context.CodeBlock.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .TakeWhile(_ => !context.CancellationToken.IsCancellationRequested)
                .Aggregate(new Dictionary<ISymbol, List<InvocationsInSession>>(SymbolEqualityComparer.Default),
                    (agrCtx, invocation) =>
                    {
                        var semanticModel = context.SemanticModel;

                        var symbol = semanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
                        if (symbol == null || symbol.Kind != SymbolKind.Method)
                        {
                            return agrCtx;
                        }

                        IMethodSymbol method = (IMethodSymbol)symbol;
                        if (!this.MatchInvocation(method))
                        {
                            return agrCtx;
                        }

                        // The nearest declaration or assignment: either can be nested in the other
                        ISymbol assignmentSym = null;

                        switch (invocation.Ancestors().FirstOrDefault(x => x is VariableDeclarationSyntax || x is AssignmentExpressionSyntax))
                        {
                            case VariableDeclarationSyntax variableDeclaration when variableDeclaration.Variables.Count > 0:
                                assignmentSym = context.SemanticModel.GetDeclaredSymbol(variableDeclaration.Variables[0], context.CancellationToken);
                                break;
                            case AssignmentExpressionSyntax assignment:
                                assignmentSym = context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol;
                                break;
                        }

                        if (assignmentSym != null && invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                        {
                            var accessSym = context.SemanticModel.GetSymbolInfo(memberAccess.Expression, context.CancellationToken);

                            // E.g. (await OpenSessionAsync()).LoadAsync<T>(id)
                            if (accessSym.Symbol == null)
                            {
                                return agrCtx;
                            }

                            // ReSharper disable once AccessToModifiedClosure
                            var readInside = invocation.ArgumentList.Arguments
                                .Select(x => context.SemanticModel.AnalyzeDataFlow(x.Expression))
                                .Where(x => x.Succeeded).SelectMany(x => x.ReadInside).ToArray();

                            if (EvaluateFollowingInvocationsOn.Contains(method.Name))
                            {
                                var followingInvocations = invocation.Parent.Ancestors().OfType<InvocationExpressionSyntax>().ToList();

                                // ReSharper disable once AccessToModifiedClosure
                                readInside = readInside.Concat(followingInvocations.SelectMany(n => n.ArgumentList.Arguments                                    
                                    .Select(x => context.SemanticModel.AnalyzeDataFlow(x.Expression))
                                    .Where(x => x.Succeeded).SelectMany(x => x.ReadInside))).ToArray();
                            }

                            // Declared in another file (primary constructor of a partial type): not of this semantic model
                            IEnumerable<SyntaxNode> Declarations(ISymbol declared)
                            {
                                return declared.DeclaringSyntaxReferences
                                    .Where(x => x.SyntaxTree == context.SemanticModel.SyntaxTree)
                                    .Select(x => x.GetSyntax(context.CancellationToken));
                            }

                            // https://github.com/dotnet/roslyn/issues/3394
                            // Elementary...
                            IEnumerable<ISymbol> Get(SyntaxNode node)
                            {
                                IEnumerable<ISymbol> Filter(SyntaxNode nodeToFilter)
                                {
                                    return nodeToFilter.DescendantNodes()
                                        .TakeWhile(_ => !context.CancellationToken.IsCancellationRequested).Select(n =>
                                        {
                                            switch (n)
                                            {
                                                case IdentifierNameSyntax i:
                                                    {
                                                        var identifier = context.SemanticModel
                                                            .GetSymbolInfo(i, context.CancellationToken).Symbol;

                                                        return identifier?.Kind == SymbolKind.Local ? identifier : null;
                                                    }
                                            }

                                            return null;
                                        }).Where(x => x != null);
                                }

                                var refChain = new List<ISymbol>(Filter(node));
                                var toTraverse = new Queue<ISymbol>(refChain);

                                while (toTraverse.Any() && !context.CancellationToken.IsCancellationRequested)
                                {
                                    var item = toTraverse.Dequeue();
                                    var items = Declarations(item).SelectMany(Filter).ToArray();
                                    var toAnalyze = items.Except(refChain, SymbolEqualityComparer.Default).ToArray();
                                    refChain.AddRange(toAnalyze);
                                    foreach (var t in toAnalyze)
                                    {
                                        toTraverse.Enqueue(t);
                                    }
                                }

                                return refChain;
                            }

                            var dependentSymbols = new List<ISymbol>(readInside);

                            var declaringReferences = readInside.SelectMany(s => Declarations(s).SelectMany(Get));
                            dependentSymbols.AddRange(declaringReferences);

                            if (!agrCtx.TryGetValue(accessSym.Symbol, out var list))
                            {
                                agrCtx[accessSym.Symbol] = list = new List<InvocationsInSession>();
                            }

                            var invocations = new InvocationsInSession
                            {
                                Invocation = invocation,                                
                                Assignment = assignmentSym,
                                ReadInside = dependentSymbols.ToArray()
                            };
                            list.Add(invocations);
                        }

                        return agrCtx;
                    });

            void AnalyzeInvocationsPerSession(KeyValuePair<ISymbol, List<InvocationsInSession>> invocations)
            {
                var toDiagnostic = invocations.Value.Aggregate(new List<InvocationsInSession>(), (list, invocation) =>
                {
                    if (!invocation.ReadInside.Any(x => invocations.Value.Except(new[] { invocation }).Any(s => SymbolEqualityComparer.Default.Equals(s.Assignment, x))))
                    {
                        list.Add(invocation);
                    }

                    return list;
                });

                if (toDiagnostic.Count < 2)
                {
                    return;
                }

                foreach (var invocation in toDiagnostic)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        SupportedDiagnostics[0],
                        invocation.Invocation.GetLocation(), invocations.Key.Name));
                }
            }

            foreach (var invocations in invocationsPerSession.Where(x => x.Value.Count > 1 && !context.CancellationToken.IsCancellationRequested))
            {
                AnalyzeInvocationsPerSession(invocations);
            }
        }
    }
}