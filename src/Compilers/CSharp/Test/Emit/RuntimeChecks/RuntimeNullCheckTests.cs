// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.RuntimeChecks
{
    public class RuntimeNullCheckTests : CompilingTestBase
    {
        [Fact]
        public void NullableDisable_ChecksNotEmitted()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        M(null);
    }

    static void M(string s)
    {
        Console.WriteLine(""ok"");
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
            verifier.VerifyIL("C.M", @"
{
  // Code size       11 (0xb)
  .maxstack  1
  IL_0000:  ldstr      ""ok""
  IL_0005:  call       ""void System.Console.WriteLine(string)""
  IL_000a:  ret
}");
        }

        [Fact]
        public void NullableParameter_NoCheck()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        M(null);
    }

    static void M(string? s)
    {
        Console.WriteLine(""ok"");
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
            verifier.VerifyIL("C.M", @"
{
  // Code size       11 (0xb)
  .maxstack  1
  IL_0000:  ldstr      ""ok""
  IL_0005:  call       ""void System.Console.WriteLine(string)""
  IL_000a:  ret
}");
        }

        [Fact]
        public void OutParameter_NoCheck()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        string s;
        M(out s);
        Console.WriteLine(s);
    }

    static void M(out string s)
    {
        s = ""ok"";
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
            verifier.VerifyIL("C.M", @"
{
  // Code size        8 (0x8)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldstr      ""ok""
  IL_0006:  stind.ref
  IL_0007:  ret
}");
        }

        [Fact]
        public void ValueType_NoCheck()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        M(42);
    }
    
    static void M(int n)
    {
        Console.WriteLine(n);
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "42");
            verifier.VerifyIL("C.M", expectedIL: @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""void System.Console.WriteLine(int)""
  IL_0006:  ret
}");
        }

        [Fact]
        public void EmptyMethod_RefTypeParameter()
        {
            const string source = @"
using System;
class C
{
    static void M(string s)
    {
    }

    static void Main()
    {
        M(""sample text"");
        Console.WriteLine(""ok"");
    }
}";
            var comp = CreateCompilation(source);
            CompileAndVerify(comp, expectedOutput: "ok");
        }

        [Fact]
        public void ReferenceTypeParameter()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        M(null);
    }

    static void M(string s)
    {
        Console.WriteLine(""unreachable"");
        Console.WriteLine(s.Length);
    }
}";

            var comp = CreateCompilation(source);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        public void ExpressionBodiedMember()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        M(null);
    }

    static void M(string s) => Console.WriteLine(s.Length);
}";

            var comp = CreateCompilation(source);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        public void IteratorMethod()
        {
            string source = @"
using System;
using System.Collections.Generic;

class C
{
    static IEnumerable<int> M(string s)
    {
        yield return s.Length;
    }
    static void Main()
    {
        _ = M(null);
        Console.WriteLine(""unreachable"");
    }
}";

            var comp = CreateCompilation(source);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        public void AsyncMethod()
        {
            const string source = @"
using System;
using System.Threading.Tasks;

class C
{
    static async Task M(string s)
    {
        await Task.Delay(42);
    }

    static async Task Main()
    {
        await M(null);
        Console.WriteLine(""unreachable"");
    }
}";

            var comp = CreateCompilation(source);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        public void SynthesizedMethod_SingleRetInstruction()
        {
            const string source = @"
using System;
Console.WriteLine(""ok"");

// It's an empty record: some of its synthesized members will consist
// of only one ret instruction.
record R;";
            var comp = CreateCompilation(source);
            CompileAndVerify(comp, expectedOutput: "ok");
        }

        [Fact]
        public void AsyncEnumerable()
        {
            const string source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class C
{
    static async Task Main()
    {
        M(null);
    }

    static async IAsyncEnumerable<int> M(string s)
    {
        await Task.Delay(10);
        yield return s.Length;
    }
}";

            var comp = CreateCompilation(source);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        public void LambdaExpression()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        Action<string> printLength = s => Console.WriteLine(s.Length);

        printLength(null);
        Console.WriteLine(""unreachable"");
    }
}";
            var comp = CreateCompilation(source);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        public void LocalFunction()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        void printLength(string s) => Console.WriteLine(s.Length);

        printLength(null);
    }
}";
            var comp = CreateCompilation(source);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        public void LocalFunction_Iterator()
        {
            const string source = @"
using System;
using System.Collections.Generic;

class C
{
    static void Main()
    {
        IEnumerable<int> goo(string s)
        {
            yield return s.Length;
        };

        goo(null);
    }
}";
            var comp = CreateCompilation(source);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        public void LocalFunction_Async()
        {
            const string source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class C
{
    static async Task Main()
    {
        static async Task goo(string s)
        {
            await Task.Delay(42);
        }

        await goo(null);
    }
}";
            var comp = CreateCompilation(source);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        public void AutoProperty_Setter()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        Prop = null;
    }

    static string Prop { get; set; }
}";

            var comp = CreateCompilation(source);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        public void Indexer()
        {
            const string source = @"
using System;
class C
{
    int this[string s] => s.Length;    

    static void Main()
    {
        var c = new C();
        Console.WriteLine(c[null]);
    }
}";

            var comp = CreateCompilation(source);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        public void MissingArgumentNullException()
        {
            const string source = @"
class C
{
    static void Main()
    {
    }

    static void Goo(string s)
    {
    }
}";
            var options = WithRuntimeChecks(nullableContext: true);
            var comp = CreateCompilation(source, options: options);
            comp.MakeTypeMissing(LessWellKnownType.System_ArgumentNullException);
            comp.VerifyEmitDiagnostics(
                // error CS0656: Missing compiler required member 'System.ArgumentNullException..ctor'
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember)
                    .WithArguments("System.ArgumentNullException", ".ctor")
                    .WithLocation(1, 1));
        }

        internal CompilationVerifier CompileAndVerifyException<T>(
            CSharpCompilation comp,
            string? expectedMessage = null,
            string expectedOutput = "",
            Verification verify = Verification.Passes) where T : Exception
        {
            try
            {
                CompileAndVerify(comp, expectedOutput: expectedOutput, verify: verify);
                Assert.False(true, string.Format("Expected exception {0}({1})", typeof(T).Name, expectedMessage));
            }
            catch (Exception x)
            {
                Exception? e = x.InnerException;
                Assert.IsType<T>(e);
                Debug.Assert(e != null);
                if (expectedMessage != null)
                {
                    Assert.Equal(expectedMessage, e.Message);
                }
            }

            return CompileAndVerify(comp, verify: verify);
        }

        private static CSharpCompilation CreateCompilation(string source, bool nullableContext = true)
        {
            return CreateCompilationWithTasksExtensions(new[] { source, AsyncStreamsTypes },
                options: WithRuntimeChecks(nullableContext));
        }

        private static CSharpCompilationOptions WithRuntimeChecks(bool nullableContext)
        {
            var options = TestOptions.ReleaseExe;
            var nrtOptions = nullableContext
                ? NullableContextOptions.Enable
                : NullableContextOptions.Disable;
            return options
                .WithNullableContextOptions(nrtOptions)
                .WithRuntimeChecks(true);
        }
    }
}
