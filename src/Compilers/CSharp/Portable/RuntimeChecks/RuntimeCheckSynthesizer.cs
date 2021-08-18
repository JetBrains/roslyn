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
                || IsBlocklisted(methodSymbol))
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
            var stmts = ArrayBuilder<BoundStatement>.GetInstance();
            bool producedAnySequencePoints = false;
            for (int i = 0; i < baseParameters.Length; i++)
            {
                var param = parameters[i];
                var baseParam = baseParameters[i];
                if (NeedsChecking(methodSymbol, baseParam, F.Compilation))
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

        private bool IsBlocklisted(MethodSymbol method)
        {
            return method.ContainingNamespace.ToString() == "System.Runtime.CompilerServices"
                || (method is SourcePropertyAccessorSymbol { AssociatedSymbol: SourcePropertySymbol prop }
                   && _suppressedNullAssignmentCollector.CollectedProperties.Contains(prop));
        }

        private static bool NeedsChecking(MethodSymbol method, ParameterSymbol parameter, CSharpCompilation compilation)
        {
            if (parameter.Type is { IsValueType: true } or TypeParameterSymbol { IsNotNullable: false }
                || parameter.RefKind == RefKind.Out)
            {
                return false;
            }

            if (parameter.TypeWithAnnotations.NullableAnnotation == NullableAnnotation.NotAnnotated)
            {
                return (parameter.FlowAnalysisAnnotations & FlowAnalysisAnnotations.AllowNull) != FlowAnalysisAnnotations.AllowNull;
            }

            return parameter.TypeWithAnnotations.NullableAnnotation == NullableAnnotation.Oblivious
                && CheckJetBrainsAnnotations(method, parameter, compilation);
        }

        private static bool CheckJetBrainsAnnotations(MethodSymbol method, ParameterSymbol parameter, CSharpCompilation compilation)
        {
            static bool isAnnotated(ParameterSymbol parameter, NamedTypeSymbol attributeType)
            {
                Symbol symbol = parameter;
                if (parameter.ContainingSymbol is SourcePropertyAccessorSymbol { AssociatedSymbol: PropertySymbol { IsIndexer: false } prop })
                {
                    symbol = prop;
                }
                foreach (var attr in symbol.GetAttributes())
                {
                    if (attr.AttributeClass?.Equals(attributeType) == true)
                    {
                        return true;
                    }
                }
                return false;
            }

            var notNullAttribute = compilation.GetLessWellKnownType(LessWellKnownType.JetBrains_Annotations_NotNullAttribute);
            // Annotation inheritance: check base methods
            MethodSymbol? curOverride = method;
            do
            {
                parameter = curOverride.Parameters[parameter.Ordinal];
                if (isAnnotated(parameter, notNullAttribute))
                {
                    return true;
                }
            } while ((curOverride = curOverride?.OverriddenMethod) is not null);

            // Annotation inheritance: check whether this method implements an (annotated) interface member
            var type = method.ContainingType;
            foreach (var @interface in type.AllInterfacesNoUseSiteDiagnostics)
            {
                Symbol? implementingMember;
                foreach (var interfaceMember in @interface.GetMembersUnordered())
                {
                    if (interfaceMember.Kind is not (SymbolKind.Method or SymbolKind.Property or SymbolKind.Event)
                        || !interfaceMember.IsImplementableInterfaceMember())
                    {
                        continue;
                    }
                    implementingMember = type.FindImplementationForInterfaceMember(interfaceMember);
                    if (implementingMember is not null && interfaceMember is MethodSymbol interfaceMethod
                        && interfaceMethod.ParameterCount == method.ParameterCount)
                    {
                        parameter = interfaceMethod.Parameters[parameter.Ordinal];
                        if (isAnnotated(parameter, notNullAttribute))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
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
