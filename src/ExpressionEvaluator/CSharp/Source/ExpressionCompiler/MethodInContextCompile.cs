// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.ExpressionEvaluator;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.DiaSymReader;
using Roslyn.Utilities;
namespace Microsoft.CodeAnalysis.CSharp.ExpressionEvaluator;

public static class MethodInContextCompile
{
    private const string TypeName = "<>x";
    private const string MethodName = "<>m0";

    private static CSharpCompileResult CompileExpression(CSharpCompilation Compilation, BlockSyntax methodBody, PEMethodSymbol currentMethod, MethodDebugInfo<TypeSymbol, LocalSymbol> methodDebugInfo, DiagnosticBag diagnostics)
    {
        var namespaceBinder = CompilationContext.CreateBinderChain(
            Compilation,
            currentMethod.ContainingNamespace,
            methodDebugInfo.ImportRecordGroups,
            methodDebugInfo.ContainingDocumentName is { } documentName ? FileIdentifier.Create(documentName) : null);

        var objectType = Compilation.GetSpecialType(SpecialType.System_Object);

        var synthesizedType = new EENamedTypeSymbol(
            Compilation.SourceModule.GlobalNamespace,
            objectType,
            // methodBody,
            currentMethod,
            TypeName,
            // MethodName,
            (symbol, container) =>
            {
                GenerateMethodBody generateMethodBody = delegate(EEMethodSymbol method, DiagnosticBag diagnostics, out ImmutableArray<LocalSymbol> locals, out ResultProperties properties)
                {
                    locals = ImmutableArray<LocalSymbol>.Empty;
                    properties = default;
                    var binder = ExtendBinderChain(
                        methodBody,
                        method,
                        namespaceBinder,
                        out var declaredLocals);

                    var result = binder.BindEmbeddedBlock(methodBody, new BindingDiagnosticBag(diagnostics));
                    if (result.HasErrors)
                        throw new InvalidOperationException("Has errors");
                    return result;
                };

                var method = new EEMethodSymbol(
                    container,
                    MethodName,
                    methodBody.Location,
                    currentMethod,
                    ImmutableArray<LocalSymbol>.Empty,
                    ImmutableArray<LocalSymbol>.Empty,
                    ImmutableDictionary<string, DisplayClassVariable>.Empty,
                    generateMethodBody, true);

                return ImmutableArray.Create<MethodSymbol>(method);
            }
        );

        var testData = new CompilationTestData();
        var moduleBuilder = CompilationContext.CreateModuleBuilder(
            Compilation,
            additionalTypes: ImmutableArray.Create((NamedTypeSymbol)synthesizedType),
            testData,
            diagnostics);

        Compilation.Compile(
            moduleBuilder,
            emittingPdb: false,
            diagnostics,
            filterOpt: null,
            CancellationToken.None);

        if (diagnostics.HasAnyErrors())
        {
            throw new Exception("Exception after compilation");
        }

        using var stream = new MemoryStream();
        var synthesizedMethod = CompilationContext.GetSynthesizedMethod(synthesizedType);

        Cci.PeWriter.WritePeToStream(
            new EmitContext(moduleBuilder, null, diagnostics, metadataOnly: false, includePrivateMembers: true),
            Compilation.MessageProvider,
            () => stream,
            getPortablePdbStreamOpt: null,
            nativePdbWriterOpt: null,
            pdbPathOpt: null,
            metadataOnly: false,
            isDeterministic: false,
            emitTestCoverageData: false,
            privateKeyOpt: null,
            CancellationToken.None);

        if (diagnostics.HasAnyErrors())
        {
            return null;
        }

        Debug.Assert(synthesizedMethod.ContainingType.MetadataName == TypeName);
        Debug.Assert(synthesizedMethod.MetadataName == MethodName);

        return new CSharpCompileResult(
            stream.ToArray(),
            synthesizedMethod,
            formatSpecifiers: new ReadOnlyCollection<string>(new List<string>()));
    }

    private static Binder ExtendBinderChain(
        CSharpSyntaxNode syntax,
        // ImmutableArray<Alias> aliases,
        EEMethodSymbol method,
        Binder binder,
        // bool hasDisplayClassThis,
        // bool methodNotType,
        out ImmutableArray<LocalSymbol> declaredLocals)
    {
        var substitutedSourceMethod = CompilationContext.GetSubstitutedSourceMethod(method.SubstitutedSourceMethod, false);
        var substitutedSourceType = substitutedSourceMethod.ContainingType;

        var stack = ArrayBuilder<NamedTypeSymbol>.GetInstance();
        for (var type = substitutedSourceType; type is object; type = type.ContainingType)
        {
            stack.Add(type);
        }

        while (stack.Count > 0)
        {
            substitutedSourceType = stack.Pop();

            binder = new InContainerBinder(substitutedSourceType, binder);
            if (substitutedSourceType.Arity > 0)
            {
                binder = new WithTypeArgumentsBinder(substitutedSourceType.TypeArgumentsWithAnnotationsNoUseSiteDiagnostics, binder);
            }
        }

        stack.Free();

        if (substitutedSourceMethod.Arity > 0)
        {
            binder = new WithTypeArgumentsBinder(substitutedSourceMethod.TypeArgumentsWithAnnotations, binder);
        }

        // // Method locals and parameters shadow pseudo-variables.
        // // That is why we place PlaceholderLocalBinder and ExecutableCodeBinder before EEMethodBinder.
        // if (methodNotType)
        // {
        //     var typeNameDecoder = new EETypeNameDecoder(binder.Compilation, (PEModuleSymbol)substitutedSourceMethod.ContainingModule);
        //     binder = new PlaceholderLocalBinder(
        //         syntax,
        //         aliases,
        //         method,
        //         typeNameDecoder,
        //         binder);
        // }

        binder = new EEMethodBinder(method, substitutedSourceMethod, binder, true);

        // if (methodNotType)
        // {
        //     binder = new SimpleLocalScopeBinder(method.LocalsForBinding, binder);
        // }

        Binder? actualRootBinder = null;
        SyntaxNode? declaredLocalsScopeDesignator = null;

        var executableBinder = new ExecutableCodeBinder(syntax, substitutedSourceMethod, binder,
            (rootBinder, declaredLocalsScopeDesignatorOpt) =>
            {
                actualRootBinder = rootBinder;
                declaredLocalsScopeDesignator = declaredLocalsScopeDesignatorOpt;
            });

        // We just need to trigger the process of building the binder map
        // so that the lambda above was executed.
        executableBinder.GetBinder(syntax);

        RoslynDebug.AssertNotNull(actualRootBinder);

        if (declaredLocalsScopeDesignator != null)
        {
            declaredLocals = actualRootBinder.GetDeclaredLocalsForScope(declaredLocalsScopeDesignator);
        }
        else
        {
            declaredLocals = ImmutableArray<LocalSymbol>.Empty;
        }

        return actualRootBinder;
    }
}
