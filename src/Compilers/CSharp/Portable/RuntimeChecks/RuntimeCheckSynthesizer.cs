// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp.RuntimeChecks
{
    internal class RuntimeCheckSynthesizer
    {
        private readonly SuppressedNullAssignmentCollector _suppressedNullAssignmentCollector = new();

        public void ProcessConstructors(NamedTypeSymbol typeSymbol, BindingDiagnosticBag diagnostics)
        {
            foreach (var ctor in typeSymbol.Constructors)
            {
                if (ctor is SourceConstructorSymbol and SourceMemberMethodSymbol member)
                {
                    var binder = member.TryGetBodyBinder();
                    var body = binder.BindMethodBody(member.SyntaxNode, diagnostics);
                    _suppressedNullAssignmentCollector.Visit(body);
                }
            }
        }

        public BoundStatement GenerateArgumentNullChecks(
            MethodSymbol methodSymbol,
            BoundStatement body,
            TypeCompilationState compilationState,
            BindingDiagnosticBag diagnostics)
        {
            Debug.Assert(body.Kind is BoundKind.Block or BoundKind.StatementList, $"Unexpected BoundKind: {body.Kind}");
            if (methodSymbol.Parameters.IsEmpty || body is not BoundStatementList originalStmts
                || compilationState.ModuleBuilderOpt is not PEAssemblyBuilderBase assemblyBuilder
                || IsExcluded(methodSymbol))
            {
                return body;
            }

            var parameters = methodSymbol.Parameters;
            var baseParameters = methodSymbol.Parameters;
            if (methodSymbol is SynthesizedClosureMethod rewrittenLambdaOrLocalFunction)
            {
                // Rewritten lambdas and local functions have synthesized parameters which no longer
                // have references to the original syntax needed to generate sequence points.
                // BaseMethod represents the original symbol.
                baseParameters = rewrittenLambdaOrLocalFunction.BaseMethod.Parameters;
            }
            else if (methodSymbol is SourcePropertyAccessorSymbol { AssociatedSymbol: SourcePropertySymbol { IsIndexer: true } prop })
            {
                // Indexers need special treatment cause their get accessors have "cloned" parameters
                // with no references to the original syntax.
                baseParameters = prop.Parameters;
            }

            var F = new SyntheticBoundNodeFactory(methodSymbol, body.Syntax, compilationState, diagnostics);
            MethodSymbol exceptionCtor;
            try
            {
                exceptionCtor = (MethodSymbol)F.LessWellKnownMember(LessWellKnownMember.System_ArgumentNullException__ctor);
            }
            catch (SyntheticBoundNodeFactory.MissingPredefinedMember ex)
            {
                diagnostics.Add(ex.Diagnostic);
                return body;
            }

            var stmts = ArrayBuilder<BoundStatement>.GetInstance();
            bool producedAnySequencePoints = false;
            for (int i = 0; i < baseParameters.Length; i++)
            {
                var param = parameters[i];
                var baseParam = baseParameters[i];
                if (NeedsChecking(baseParam))
                {
                    var syntaxReferences = baseParam.DeclaringSyntaxReferences;
                    // Assuming parameters can have either 1 or 0 syntax references
                    var syntax = syntaxReferences.Length == 1
                        ? (CSharpSyntaxNode)syntaxReferences[0].GetSyntax()
                        : null;
                    stmts.Add(GenerateArgumentCheck(param, syntax, F, assemblyBuilder));
                    producedAnySequencePoints |= syntax is not null;
                }
            }

            if (stmts.Count == 0)
            {
                return body;
            }

            // If we couldn't produce any sequence points, but there's an existing one in the original body,
            // we can expand its span to include the generated runtime checks as well.
            if (!producedAnySequencePoints && !originalStmts.Statements.IsEmpty &&
                originalStmts.Statements[0].Kind is BoundKind.SequencePoint or BoundKind.SequencePointWithSpan)
            {
                var originalSequencePoint = originalStmts.Statements[0];
                BoundStatement updatedSequencePoint;
                if (originalSequencePoint is BoundSequencePoint sp)
                {
                    stmts.AddIfNotNull(sp.StatementOpt);
                    updatedSequencePoint = F.SequencePoint(
                        (CSharpSyntaxNode)sp.Syntax,
                        F.Block(stmts.ToImmutableAndClear()));
                }
                else
                {
                    var spannedSp = (BoundSequencePointWithSpan)originalSequencePoint;
                    stmts.AddIfNotNull(spannedSp.StatementOpt);
                    updatedSequencePoint = F.SequencePointWithSpan(
                        (CSharpSyntaxNode)spannedSp.Syntax,
                        spannedSp.Span,
                        F.Block(stmts.ToImmutableAndClear()));
                }

                stmts.Add(updatedSequencePoint);
                for (int i = 1; i < originalStmts.Statements.Length; i++)
                {
                    stmts.Add(originalStmts.Statements[i]);
                }
            }
            else
            {
                stmts.AddRange(originalStmts.Statements);
            }

            return body is BoundBlock block
                ? block.Update(block.Locals, block.LocalFunctions, stmts.ToImmutableAndFree())
                : F.Block(stmts.ToImmutableAndFree());
        }

        private static bool NeedsChecking(ParameterSymbol parameter)
        {
            return parameter is
            {
                RefKind: not RefKind.Out,
                TypeWithAnnotations:
                {
                    NullableAnnotation: NullableAnnotation.NotAnnotated,
                    Type: { IsValueType: false } and not TypeParameterSymbol { IsNotNullable: not true }
                }
            } && (parameter.FlowAnalysisAnnotations & FlowAnalysisAnnotations.AllowNull) != FlowAnalysisAnnotations.AllowNull;
        }

        private bool IsExcluded(MethodSymbol method)
        {
            return method.ContainingNamespace.ToString() == "System.Runtime.CompilerServices"
                || (method is SourcePropertyAccessorSymbol { AssociatedSymbol: SourcePropertySymbol prop }
                   && _suppressedNullAssignmentCollector.CollectedProperties.Contains(prop));
        }

        private static BoundStatement GenerateArgumentCheck(
            ParameterSymbol parameter,
            CSharpSyntaxNode? syntax,
            SyntheticBoundNodeFactory factory,
            PEAssemblyBuilderBase assemblyBuilder)
        {
            var F = factory;
            MethodSymbol throwMethod = assemblyBuilder.GetEmbeddedThrowHelper().ThrowArgumentNullMethod;

            var args = ImmutableArray.Create<BoundExpression>(F.StringLiteral(parameter.Name));
            var throwStmt = F.ExpressionStatement(F.StaticCall(throwMethod, args));

            BoundExpression left = F.Parameter(parameter);
            BoundExpression right = F.Null(F.Compilation.ObjectType);
            if (parameter.Type.Kind == SymbolKind.TypeParameter)
            {
                left = F.Convert(F.Compilation.ObjectType, left);
            }

            var nullcheck = F.If(F.ObjectEqual(left, right), throwStmt);
            if (syntax is not null)
            {
                nullcheck = F.SequencePointWithSpan(syntax, syntax.Span, nullcheck);
            }
            return nullcheck;
        }
    }
}
