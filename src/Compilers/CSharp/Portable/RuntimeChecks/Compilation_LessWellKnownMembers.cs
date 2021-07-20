// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.RuntimeMembers;

#nullable enable

namespace Microsoft.CodeAnalysis.CSharp
{
    public partial class CSharpCompilation
    {
        private NamedTypeSymbol?[]? _lazyLessWellKnownTypes;
        private Symbol?[]? _lazyLessWellKnownTypeMembers;

        /// <summary>
        /// Used for test purposes only to emulate missing types.
        /// </summary>
        private bool[]? _lazyMakeLessWellKnownTypeMissingMap;
        /// <summary>
        /// Used for test purposes only to emulate missing members.
        /// </summary>
        private bool[]? _lazyMakeLessWellKnownMemberMissingMap;

        internal NamedTypeSymbol GetLessWellKnownType(LessWellKnownType type)
        {
            bool ignoreCorLibraryDuplicatedTypes = this.Options.TopLevelBinderFlags.Includes(BinderFlags.IgnoreCorLibraryDuplicatedTypes);

            int index = (int)type;
            if (_lazyLessWellKnownTypes == null || _lazyLessWellKnownTypes[index] is null)
            {
                if (_lazyLessWellKnownTypes == null)
                {
                    Interlocked.CompareExchange(ref _lazyLessWellKnownTypes, new NamedTypeSymbol[(int)LessWellKnownType.Count], null);
                }

                string mdName = type.GetMetadataName();
                var warnings = DiagnosticBag.GetInstance();
                NamedTypeSymbol? result;

                if (IsTypeMissing(type))
                {
                    result = null;
                }
                else
                {
                    DiagnosticBag? legacyWarnings = warnings;
                    result = this.Assembly.GetTypeByMetadataName(
                        mdName, includeReferences: true, useCLSCompliantNameArityEncoding: true, isWellKnownType: true, conflicts: out _,
                        warnings: legacyWarnings, ignoreCorLibraryDuplicatedTypes: ignoreCorLibraryDuplicatedTypes);
                }

                if (result is null)
                {
                    MetadataTypeName emittedName = MetadataTypeName.FromFullName(mdName, useCLSCompliantNameArityEncoding: true);
                    result = new MissingMetadataTypeSymbol.TopLevel(this.Assembly.Modules[0], ref emittedName);
                }

                if (Interlocked.CompareExchange(ref _lazyLessWellKnownTypes[index], result, null) is object)
                {
                    Debug.Assert(
                        TypeSymbol.Equals(result, _lazyLessWellKnownTypes[index], TypeCompareKind.ConsiderEverything2) || (_lazyLessWellKnownTypes[index]!.IsErrorType() && result.IsErrorType())
                    );
                }
                else
                {
                    AdditionalCodegenWarnings.AddRange(warnings);
                }

                warnings.Free();
            }

            return _lazyLessWellKnownTypes[index]!;
        }

        internal Symbol? GetLessWellKnownTypeMember(LessWellKnownMember member)
        {
            if (IsMemberMissing(member)) return null;

            if (_lazyLessWellKnownTypeMembers == null || ReferenceEquals(_lazyLessWellKnownTypeMembers[(int)member], ErrorTypeSymbol.UnknownResultType))
            {
                if (_lazyLessWellKnownTypeMembers == null)
                {
                    var lessWellKnownTypeMembers = new Symbol[(int)LessWellKnownMember.Count];

                    for (int i = 0; i < lessWellKnownTypeMembers.Length; i++)
                    {
                        lessWellKnownTypeMembers[i] = ErrorTypeSymbol.UnknownResultType;
                    }

                    Interlocked.CompareExchange(ref _lazyLessWellKnownTypeMembers, lessWellKnownTypeMembers, null);
                }

                MemberDescriptor descriptor = LessWellKnownMembers.GetDescriptor(member);
                NamedTypeSymbol type = GetLessWellKnownType((LessWellKnownType)descriptor.DeclaringTypeId);
                Symbol? result = null;

                if (!type.IsErrorType())
                {
                    result = GetRuntimeMember(type, descriptor, WellKnownMemberSignatureComparer, accessWithinOpt: this.Assembly);
                }

                Interlocked.CompareExchange(ref _lazyLessWellKnownTypeMembers[(int)member], result, ErrorTypeSymbol.UnknownResultType);
            }

            return _lazyLessWellKnownTypeMembers[(int)member];
        }

        internal void MakeTypeMissing(LessWellKnownType type)
        {
            _lazyMakeLessWellKnownTypeMissingMap ??= new bool[(int)LessWellKnownType.Count];
            _lazyMakeLessWellKnownTypeMissingMap[(int)type] = true;
        }

        internal void MakeMemberMissing(LessWellKnownMember member)
        {
            _lazyMakeLessWellKnownMemberMissingMap ??= new bool[(int)LessWellKnownMember.Count];
            _lazyMakeLessWellKnownMemberMissingMap[(int)member] = true;
        }

        private bool IsTypeMissing(LessWellKnownType type)
        {
            return _lazyMakeLessWellKnownTypeMissingMap?[(int)type] == true;
        }

        private bool IsMemberMissing(LessWellKnownMember member)
        {
            return _lazyMakeLessWellKnownMemberMissingMap?[(int)member] == true;
        }
    }
}
