// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Microsoft.Cci;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;
using Roslyn.Utilities;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp.RuntimeChecks
{
    internal abstract class SynthesizedThrowHelperMethod : SynthesizedInstanceMethodSymbol
    {
        protected SynthesizedThrowHelperMethod(SynthesizedThrowHelperType containingType)
        {
            ContainingSymbol = ContainingType = containingType;
        }

        public abstract override string Name { get; }
        public abstract override ImmutableArray<ParameterSymbol> Parameters { get; }

        public override Symbol ContainingSymbol { get; }
        public override NamedTypeSymbol ContainingType { get; }
        public override ImmutableArray<Location> Locations => ContainingType.Locations;

        public override Accessibility DeclaredAccessibility => Accessibility.Internal;
        public override bool IsStatic => true;
        public override bool IsVirtual => false;
        public override bool IsOverride => false;
        public override bool IsAbstract => false;
        public override bool IsSealed => false;
        public override bool IsExtern => false;
        public override MethodKind MethodKind => MethodKind.Ordinary;
        public override int Arity => 0;
        public override bool IsExtensionMethod => false;
        internal override bool HasSpecialName => false;
        internal override MethodImplAttributes ImplementationAttributes => MethodImplAttributes.NoInlining;
        internal override bool HasDeclarativeSecurity => false;

        public override DllImportData? GetDllImportData() => null;

        internal override IEnumerable<SecurityAttribute> GetSecurityInformation() => throw ExceptionUtilities.Unreachable;

        internal override MarshalPseudoCustomAttributeData? ReturnValueMarshallingInformation => null;
        internal override bool RequiresSecurityObject => false;
        public override bool HidesBaseMethodsByName => false;
        public override bool IsVararg => false;
        public override bool ReturnsVoid => true;
        public override bool IsAsync => false;
        public override RefKind RefKind => RefKind.None;

        public override TypeWithAnnotations ReturnTypeWithAnnotations
            => TypeWithAnnotations.Create(ContainingAssembly.GetSpecialType(SpecialType.System_Void));
        public override FlowAnalysisAnnotations ReturnTypeFlowAnalysisAnnotations => FlowAnalysisAnnotations.None;
        public override ImmutableHashSet<string> ReturnNotNullIfParameterNotNull => ImmutableHashSet<string>.Empty;
        public override ImmutableArray<TypeWithAnnotations> TypeArgumentsWithAnnotations => ImmutableArray<TypeWithAnnotations>.Empty;
        public override ImmutableArray<TypeParameterSymbol> TypeParameters => ImmutableArray<TypeParameterSymbol>.Empty;

        public override ImmutableArray<MethodSymbol> ExplicitInterfaceImplementations => ImmutableArray<MethodSymbol>.Empty;
        public override ImmutableArray<CustomModifier> RefCustomModifiers => ImmutableArray<CustomModifier>.Empty;
        public override Symbol? AssociatedSymbol => null;

        internal override ImmutableArray<string> GetAppliedConditionalSymbols() => ImmutableArray<string>.Empty;

        internal override CallingConvention CallingConvention => CallingConvention.Default;
        internal override bool GenerateDebugInfo => true;

        internal override bool IsMetadataNewSlot(bool ignoreInterfaceImplementationChanges = false) => false;
        internal override bool IsMetadataVirtual(bool ignoreInterfaceImplementationChanges = false) => false;

        internal override bool SynthesizesLoweredBoundBody => true;

        internal override void AddSynthesizedAttributes(PEModuleBuilder moduleBuilder, ref ArrayBuilder<SynthesizedAttributeData>? attributes)
        {
            var compilation = DeclaringCompilation;
            AddSynthesizedAttribute(ref attributes, compilation.TrySynthesizeAttribute(WellKnownMember.System_Diagnostics_DebuggerHiddenAttribute__ctor));
            AddSynthesizedAttribute(ref attributes, compilation.TrySynthesizeAttribute(WellKnownMember.System_Runtime_CompilerServices_CompilerGeneratedAttribute__ctor));
            base.AddSynthesizedAttributes(moduleBuilder, ref attributes);
        }

        internal abstract override void GenerateMethodBody(TypeCompilationState compilationState, BindingDiagnosticBag diagnostics);
    }
}
