// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.RuntimeChecks
{
    public sealed class ReSharperPostconditionTests : RuntimeCheckTestsBase
    {
        [Fact]
        public void NotNull_ReturnValue()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class C
{
    static void Main()
    {
        M();
    }

    [NotNull] static object M() => null;
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
        }

        [Fact]
        public void ReturnValue_NotNull_InheritedFromBase()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
abstract class Base
{
    [NotNull] protected abstract object M();
}
class C : Base
{
    static void Main()
    {
        var c = new C();
        c.M();
    }

    protected override object M() => null;
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
        }

        [Fact]
        public void NotNull_GetOnlyProperty()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class C
{
    static void Main()
    {
        var c = new C();
        _ = c.Prop;
    }

    [NotNull] private object Prop => null;
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
        }

        [Fact]
        public void NotNull_GetOnlyProperty_InheritedFromBase()
        {
            const string source = @"
using System;
using JetBrains.Annotations;

abstract class Base
{
    [NotNull] protected abstract object Prop { get; }
}

class C : Base
{
    static void Main()
    {
        var c = new C();
        _ = c.Prop;
    }

    protected override object Prop => null;
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
        }

        [Fact]
        public void NotNull_RefParameter()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class C
{
    static void Main()
    {
        string s = string.Empty;
        M(ref s);
    }

    static void M([NotNull] ref string s) => s = null;
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentException>(comp);
        }

        private CSharpCompilation CreateCompilation(string source, bool nullableContext = false, bool useAsyncStreams = false)
            => CreateCompilation(source, RuntimeChecksMode.PostconditionsOnly, nullableContext, useAsyncStreams);
    }
}
