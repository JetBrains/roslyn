// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp.RuntimeChecks
{
    internal static class RuntimeCheckSynthesizer
    {
        public static BoundStatement GenerateArgumentNullChecks(
            MethodSymbol methodSymbol,
            BoundStatement body,
            TypeCompilationState compilationState,
            BindingDiagnosticBag diagnostics)
        {
            Debug.Assert(body.Kind is BoundKind.Block or BoundKind.StatementList, $"Unexpected BoundKind: {body.Kind}");
            if (methodSymbol.Parameters.IsEmpty || body is not BoundStatementList originalStmts)
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
                    stmts.Add(GenerateArgumentCheck(param, syntax, F, exceptionCtor));
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
                originalStmts.Statements[0] is BoundSequencePoint sequencePoint)
            {
                if (sequencePoint.StatementOpt is BoundStatement firstStmt)
                {
                    stmts.Add(firstStmt);
                }
                var updatedSequencePoint = F.SequencePoint(
                    (CSharpSyntaxNode)sequencePoint.Syntax,
                    F.Block(stmts.ToImmutableAndClear()));
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
            return parameter.Type.IsReferenceType
                   && !parameter.IsMetadataOut
                   && parameter.TypeWithAnnotations.NullableAnnotation == NullableAnnotation.NotAnnotated;
        }

        private static BoundStatement GenerateArgumentCheck(
            ParameterSymbol parameter,
            CSharpSyntaxNode? syntax,
            SyntheticBoundNodeFactory factory,
            MethodSymbol exceptionCtor)
        {
            var F = factory;
            var throwStmt = F.Throw(F.New(exceptionCtor, F.StringLiteral(parameter.Name)));
            var nullcheck = F.If(
                F.ObjectEqual(F.Parameter(parameter), F.Null(parameter.Type)),
                throwStmt);

            if (syntax is not null)
            {
                nullcheck = F.SequencePointWithSpan(syntax, syntax.Span, nullcheck);
            }

            return nullcheck;
        }
    }
}
