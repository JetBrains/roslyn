// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Symbols;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp.RuntimeChecks
{
    internal sealed class SynthesizedThrowArgumentNullMethod : SynthesizedThrowHelperMethod
    {
        public SynthesizedThrowArgumentNullMethod(SynthesizedThrowHelperType containingType) : base(containingType)
        {
            DeclaringCompilation.GetSpecialType(SpecialType.System_String);
            var param = SynthesizedParameterSymbol.Create(container: this,
                TypeWithAnnotations.Create(ContainingAssembly.GetSpecialType(SpecialType.System_String), NullableAnnotation.NotAnnotated),
                ordinal: 0, RefKind.None, "name");
            Parameters = ImmutableArray.Create(param);
        }

        public override string Name => "ArgumentNull";
        public override ImmutableArray<ParameterSymbol> Parameters { get; }

        internal override void GenerateMethodBody(TypeCompilationState compilationState, BindingDiagnosticBag diagnostics)
        {
            var F = new SyntheticBoundNodeFactory(this, this.GetNonNullSyntaxNode(), compilationState, diagnostics);
            F.CurrentFunction = this;
            try
            {
                MethodSymbol exceptionCtor = (MethodSymbol)F.LessWellKnownMember(LessWellKnownMember.System_ArgumentNullException__ctor);
                var throwStmt = F.Throw(F.New(exceptionCtor, F.Parameter(Parameters[0])));
                F.CloseMethod(throwStmt);
            }
            catch (SyntheticBoundNodeFactory.MissingPredefinedMember ex)
            {
                diagnostics.Add(ex.Diagnostic);
                F.CloseMethod(F.Block());
            }
        }
    }
}
