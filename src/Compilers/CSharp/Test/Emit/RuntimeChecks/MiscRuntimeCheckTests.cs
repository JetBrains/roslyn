// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.RuntimeChecks
{
    public sealed class MiscRuntimeCheckTests : RuntimeCheckTestsBase
    {
        [Fact]
        public void MissingArgumentNullException()
        {
            const string source = @"
class C
{
    static void Main()
    {
    }
}";
            var options = WithRuntimeChecks(RuntimeChecksMode.Enable, nullableContext: true);
            var comp = CreateCompilation(source, options: options);
            comp.MakeTypeMissing(LessWellKnownType.System_ArgumentNullException);
            comp.VerifyEmitDiagnostics(
                // error CS0656: Missing compiler required member 'System.ArgumentNullException..ctor'
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember)
                    .WithArguments("System.ArgumentNullException", ".ctor")
                    .WithLocation(1, 1));
        }
    }
}
