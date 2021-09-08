// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp.RuntimeChecks
{
    internal class RuntimeCheckSynthesizer
    {
        private enum ArgumentCheckKind
        {
            Input,
            Output
        }

        private enum ReturnStatementKind
        {
            Return,
            YieldReturn
        }

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

        public BoundStatement GenerateNullChecks(
            MethodSymbol methodSymbol,
            BoundStatement body,
            RuntimeChecksMode mode,
            TypeCompilationState compilationState,
            BindingDiagnosticBag diagnostics)
        {
            Debug.Assert(body.Kind is BoundKind.Block or BoundKind.StatementList, $"Unexpected BoundKind: {body.Kind}");
            if (mode == RuntimeChecksMode.Disable
                || methodSymbol.ContainingNamespace is null
                || body is not BoundStatementList originalStmts
                || compilationState.ModuleBuilderOpt is not PEAssemblyBuilderBase assemblyBuilder
                || IsBlocklisted(methodSymbol))
            {
                return body;
            }

            var nodeFactory = new SyntheticBoundNodeFactory(methodSymbol, body.Syntax, compilationState, diagnostics);
            switch (mode)
            {
                case RuntimeChecksMode.PreconditionsOnly:
                    return InsertPreconditionChecks(methodSymbol, originalStmts, assemblyBuilder, nodeFactory);
                case RuntimeChecksMode.PostconditionsOnly:
                    return InsertPostconditionChecks(methodSymbol, originalStmts, assemblyBuilder, nodeFactory);
                case RuntimeChecksMode.Enable:
                    return InsertPostconditionChecks(
                        methodSymbol,
                        body: InsertPreconditionChecks(methodSymbol, originalStmts, assemblyBuilder, nodeFactory),
                        assemblyBuilder, nodeFactory);
                default:
                    throw ExceptionUtilities.Unreachable;
            }
        }

        private static BoundStatement InsertPreconditionChecks(
            MethodSymbol methodSymbol,
            BoundStatementList body,
            PEAssemblyBuilderBase assemblyBuilder,
            SyntheticBoundNodeFactory nodeFactory)
        {
            if (methodSymbol.ParameterCount == 0) { return body; }
            var F = nodeFactory;
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

            var stmts = ArrayBuilder<BoundStatement>.GetInstance();
            bool producedAnySequencePoints = false;
            for (int i = 0; i < baseParameters.Length; i++)
            {
                var param = parameters[i];
                var baseParam = baseParameters[i];
                if (NeedsInputCheck(methodSymbol, baseParam, F.Compilation))
                {
                    var syntaxReferences = baseParam.DeclaringSyntaxReferences;
                    // Assuming parameters can have either 1 or 0 syntax references
                    var syntax = syntaxReferences.Length == 1
                        ? (CSharpSyntaxNode)syntaxReferences[0].GetSyntax()
                        : null;
                    stmts.Add(GenerateArgumentCheck(methodSymbol, param, ArgumentCheckKind.Input, syntax, F, assemblyBuilder));
                    producedAnySequencePoints |= syntax is not null;
                }
            }

            if (stmts.Count == 0)
            {
                return body;
            }

            // If we couldn't produce any sequence points, but there's an existing one in the original body,
            // we can expand its span to include the generated runtime checks as well.
            if (!producedAnySequencePoints && !body.Statements.IsEmpty &&
                body.Statements[0].Kind is BoundKind.SequencePoint or BoundKind.SequencePointWithSpan)
            {
                var originalSequencePoint = body.Statements[0];
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
                for (int i = 1; i < body.Statements.Length; i++)
                {
                    stmts.Add(body.Statements[i]);
                }
            }
            else
            {
                stmts.AddRange(body.Statements);
            }

            return body is BoundBlock block
                ? block.Update(block.Locals, block.LocalFunctions, stmts.ToImmutableAndFree())
                : F.Block(stmts.ToImmutableAndFree());
        }

        private static BoundStatement InsertPostconditionChecks(
            MethodSymbol methodSymbol,
            BoundStatement body,
            PEAssemblyBuilderBase assemblyBuilder,
            SyntheticBoundNodeFactory nodeFactory)
        {
            var compilation = nodeFactory.Compilation;
            var paramBuilder = ArrayBuilder<ParameterSymbol>.GetInstance();
            foreach (var parameter in methodSymbol.Parameters)
            {
                if (NeedsOutputCheck(parameter, compilation))
                {
                    paramBuilder.Add(parameter);
                }
            }

            var paramsToCheck = paramBuilder.ToImmutableAndFree();
            if (paramsToCheck.IsEmpty && !NeedsReturnValueCheck(methodSymbol, compilation))
            {
                return body;
            }

            var retStmtKind = !methodSymbol.IsIterator
                ? ReturnStatementKind.Return
                : ReturnStatementKind.YieldReturn;

            return ReturnPointRewriter.InsertChecks(
                body,
                retVal => GenerateReturnValueCheck(methodSymbol, retVal, retStmtKind, nodeFactory, assemblyBuilder),
                () => generateArgumentChecks(paramsToCheck));

            BoundStatement? generateArgumentChecks(ImmutableArray<ParameterSymbol> parameters)
            {
                if (parameters.IsEmpty) { return null; }
                var stmts = ArrayBuilder<BoundStatement>.GetInstance();
                foreach (var parameter in parameters)
                {
                    stmts.Add(GenerateArgumentCheck(methodSymbol, parameter, ArgumentCheckKind.Output, null, nodeFactory, assemblyBuilder));
                }
                return nodeFactory.StatementList(stmts.ToImmutableAndFree());
            }
        }

        private bool IsBlocklisted(MethodSymbol method)
        {
            return method.ContainingNamespace?.ToString() == "System.Runtime.CompilerServices"
                || (method is SourcePropertyAccessorSymbol { AssociatedSymbol: SourcePropertySymbol prop }
                   && _suppressedNullAssignmentCollector.CollectedProperties.Contains(prop));
        }

        private static bool NeedsInputCheck(MethodSymbol method, ParameterSymbol parameter, CSharpCompilation compilation)
        {
            if (parameter.Type is { IsValueType: true } or TypeParameterSymbol { IsNotNullable: false } || parameter.RefKind == RefKind.Out)
            {
                return false;
            }

            var flowAnalysisAnnotations = parameter.FlowAnalysisAnnotations;
            return parameter.TypeWithAnnotations.NullableAnnotation switch
            {
                NullableAnnotation.NotAnnotated => (flowAnalysisAnnotations & FlowAnalysisAnnotations.AllowNull) != FlowAnalysisAnnotations.AllowNull,
                NullableAnnotation.Annotated => (flowAnalysisAnnotations & FlowAnalysisAnnotations.DisallowNull) == FlowAnalysisAnnotations.DisallowNull,
                _ => ReSharperNeedsChecking(parameter, method, compilation)
            };
        }

        private static bool NeedsOutputCheck(ParameterSymbol parameter, CSharpCompilation compilation)
        {
            if (parameter.Type is { IsValueType: true } or TypeParameterSymbol { IsNotNullable: false }
                || parameter.RefKind is not (RefKind.Ref or RefKind.Out))
            {
                return false;
            }

            var method = (MethodSymbol)parameter.ContainingSymbol;
            var flowAnalysisAnnotations = parameter.FlowAnalysisAnnotations;
            return parameter.TypeWithAnnotations.NullableAnnotation switch
            {
                NullableAnnotation.NotAnnotated => (flowAnalysisAnnotations & FlowAnalysisAnnotations.MaybeNull) != FlowAnalysisAnnotations.MaybeNull,
                NullableAnnotation.Annotated => (flowAnalysisAnnotations & FlowAnalysisAnnotations.NotNull) == FlowAnalysisAnnotations.NotNull,
                _ => ReSharperNeedsChecking(parameter, method, compilation)
            };
        }

        private static bool NeedsReturnValueCheck(MethodSymbol method, CSharpCompilation compilation)
        {
            var returnValueType = GetReturnValueType(method);
            if (returnValueType.Type is { IsValueType: true } or TypeParameterSymbol { IsNotNullable: false })
            {
                return false;
            }

            var flowAnalysisAnnotations = method.ReturnTypeFlowAnalysisAnnotations;
            return returnValueType.NullableAnnotation switch
            {
                NullableAnnotation.NotAnnotated => (flowAnalysisAnnotations & FlowAnalysisAnnotations.MaybeNull) != FlowAnalysisAnnotations.MaybeNull,
                NullableAnnotation.Annotated => (flowAnalysisAnnotations & FlowAnalysisAnnotations.NotNull) == FlowAnalysisAnnotations.NotNull,
                _ => ReSharperNeedsChecking(method, method, compilation)
            };
        }

        private static TypeWithAnnotations GetReturnValueType(MethodSymbol method)
        {
            if ((method.IsIterator || method.IsAsync) && method.ReturnType is NamedTypeSymbol { Arity: 1 } namedType)
            {
                return namedType.TypeArgumentsWithAnnotationsNoUseSiteDiagnostics[0];
            }

            return method.ReturnTypeWithAnnotations;
        }

        private static bool ReSharperNeedsChecking(Symbol symbol, MethodSymbol containingMethod, CSharpCompilation compilation)
        {
            static bool isAnnotated(Symbol symbol, NamedTypeSymbol attributeType)
            {
                if (symbol is MethodSymbol { AssociatedSymbol: PropertySymbol { IsIndexer: false } getOnlyProp })
                {
                    symbol = getOnlyProp;
                }
                else if (symbol.ContainingSymbol is SourcePropertyAccessorSymbol { AssociatedSymbol: PropertySymbol { IsIndexer: false } prop })
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
            MethodSymbol? curOverride = containingMethod;
            do
            {
                Symbol curSymbol = symbol is ParameterSymbol p
                    ? curOverride.Parameters[p.Ordinal]
                    : curOverride;
                if (isAnnotated(curSymbol, notNullAttribute))
                {
                    return true;
                }
            } while ((curOverride = curOverride?.OverriddenMethod) is not null);

            // Annotation inheritance: check whether this method implements an (annotated) interface member
            var type = containingMethod.ContainingType;
            foreach (var @interface in type.AllInterfacesNoUseSiteDiagnostics)
            {
                foreach (var interfaceMember in @interface.GetMembersUnordered())
                {
                    if (interfaceMember.Kind is not (SymbolKind.Method or SymbolKind.Property or SymbolKind.Event)
                        || !interfaceMember.IsImplementableInterfaceMember())
                    {
                        continue;
                    }
                    Symbol? implementingMember = type.FindImplementationForInterfaceMember(interfaceMember);
                    if (implementingMember is not null && interfaceMember is MethodSymbol interfaceMethod
                        && interfaceMethod.ParameterCount == containingMethod.ParameterCount)
                    {
                        Symbol curSymbol = symbol is ParameterSymbol p
                            ? interfaceMethod.Parameters[p.Ordinal]
                            : interfaceMethod;
                        if (isAnnotated(curSymbol, notNullAttribute))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static BoundStatement GenerateArgumentCheck(
            MethodSymbol method,
            ParameterSymbol parameter,
            ArgumentCheckKind kind,
            CSharpSyntaxNode? syntax,
            SyntheticBoundNodeFactory factory,
            PEAssemblyBuilderBase assemblyBuilder)
        {
            var F = factory;
            F.CurrentFunction = method;

            var throwHelper = assemblyBuilder.GetEmbeddedThrowHelper();
            MethodSymbol throwMethod = kind == ArgumentCheckKind.Input
                ? throwHelper.ThrowArgumentNullMethod
                : throwHelper.ThrowOutParameterNull;
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

        private static BoundStatement? GenerateReturnValueCheck(
            MethodSymbol method,
            BoundExpression returnValue,
            ReturnStatementKind statementKind,
            SyntheticBoundNodeFactory factory,
            PEAssemblyBuilderBase assemblyBuilder)
        {
            if (returnValue is { Type: null } or { ConstantValue: { IsNull: false } })
            {
                return null;
            }
            var F = factory;
            F.CurrentFunction = method;
            // Looks like SynthesizedLocalKind.InstrumentationPayload is the only option when we need to introduce a ref variable
            // inside a synthesized method such as an auto property accessor. It prevents CalculateLocalSyntaxOffset from being called
            // for our synthesized local.
            var tempLocal = F.StoreToTemp(returnValue, out var assignment, method.RefKind, SynthesizedLocalKind.InstrumentationPayload);

            BoundExpression left = tempLocal;
            BoundExpression right = F.Null(F.Compilation.ObjectType);
            if (GetReturnValueType(method).Type.Kind == SymbolKind.TypeParameter)
            {
                left = F.Convert(F.Compilation.ObjectType, left);
            }

            MethodSymbol throwMethod = assemblyBuilder.GetEmbeddedThrowHelper().ThrowNullReturnMethod;
            var throwStmt = F.ExpressionStatement(F.StaticCall(throwMethod, ImmutableArray<BoundExpression>.Empty));
            var nullcheck = F.If(F.ObjectEqual(left, right), throwStmt);
            BoundStatement ret = statementKind == ReturnStatementKind.Return
                ? makeReturnStatement(F, tempLocal, method)
                : new BoundYieldReturnStatement(F.Syntax, tempLocal);
            var stmts = ArrayBuilder<BoundStatement>.GetInstance();
            stmts.Add(F.ExpressionStatement(assignment));
            stmts.Add(nullcheck);
            stmts.Add(ret);

            return F.Block(ImmutableArray.Create(tempLocal.LocalSymbol), stmts.ToImmutableAndFree());

            static BoundReturnStatement makeReturnStatement(SyntheticBoundNodeFactory F, BoundExpression expression, MethodSymbol method)
            {
                var useSiteInfo =
#if DEBUG
                    CompoundUseSiteInfo<AssemblySymbol>.DiscardedDependencies;
#else
                    CompoundUseSiteInfo<AssemblySymbol>.Discarded;
#endif
                var returnType = GetReturnValueType(method).Type;
                var conversion = F.Compilation.Conversions.ClassifyConversionFromType(expression.Type, returnType, ref useSiteInfo);
                Debug.Assert(useSiteInfo.Diagnostics.IsNullOrEmpty());
                Debug.Assert(conversion.Kind != ConversionKind.NoConversion);
                if (conversion.Kind != ConversionKind.Identity)
                {
                    Debug.Assert(method.RefKind == RefKind.None);
                    expression = BoundConversion.Synthesized(
                        F.Syntax,
                        expression,
                        conversion,
                        false,
                        explicitCastInCode: false,
                        conversionGroupOpt: null,
                        ConstantValue.NotAvailable,
                        returnType);
                }
                return new BoundReturnStatement(F.Syntax, method.RefKind, expression) { WasCompilerGenerated = true };
            }
        }
    }
}
