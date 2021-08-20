// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.RuntimeChecks
{
    public partial class RuntimeNullCheckTests : CompilingTestBase
    {
        [Fact]
        [Trait("Annotations", "Roslyn")]
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
        [Trait("Annotations", "Roslyn")]
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
        [Trait("Annotations", "Roslyn")]
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
        [Trait("Annotations", "Roslyn")]
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
        [Trait("Annotations", "Roslyn")]
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
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
            verifier.VerifyIL("C.M(string)", @"
{
  // Code size       14 (0xe)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""s""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
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
        Console.WriteLine(s.Length);
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.M(string)", @"
{
  // Code size       25 (0x19)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""s""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldarg.0
  IL_000e:  callvirt   ""int string.Length.get""
  IL_0013:  call       ""void System.Console.WriteLine(int)""
  IL_0018:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
        public void MulptipleReferenceTypeParameters()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        M(""meow"", null);
    }

    static void M(string s1, string s2)
    {
        Console.WriteLine(s1.Length);
        Console.WriteLine(s2.Length);
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.M(string, string)", @"
{
  // Code size       49 (0x31)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""s1""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldarg.1
  IL_000e:  brtrue.s   IL_001a
  IL_0010:  ldstr      ""s2""
  IL_0015:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_001a:  ldarg.0
  IL_001b:  callvirt   ""int string.Length.get""
  IL_0020:  call       ""void System.Console.WriteLine(int)""
  IL_0025:  ldarg.1
  IL_0026:  callvirt   ""int string.Length.get""
  IL_002b:  call       ""void System.Console.WriteLine(int)""
  IL_0030:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
        public void Generics_ClassConstraint()
        {
            const string source = @"
using System;
class Generic<T> where T : class
{
    public Generic(T value) { }
}
class C
{
    static void Main()
    {
        _ = new Generic<string>(null);
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("Generic<T>..ctor(T)", @"
{
  // Code size       25 (0x19)
  .maxstack  1
  IL_0000:  ldarg.1
  IL_0001:  box        ""T""
  IL_0006:  brtrue.s   IL_0012
  IL_0008:  ldstr      ""value""
  IL_000d:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_0012:  ldarg.0
  IL_0013:  call       ""object..ctor()""
  IL_0018:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
        public void Generics_ClassConstraint_NullableT_NoCheck()
        {
            const string source = @"
using System;
class Generic<T> where T : class
{
    public Generic(T? value) { }
}
class C
{
    static void Main()
    {
        _ = new Generic<string>(null);
        Console.WriteLine(""ok"");
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
            verifier.VerifyIL("Generic<T>..ctor(T)", @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""object..ctor()""
  IL_0006:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
        public void Generics_NotNullConstraint()
        {
            const string source = @"
using System;
class Generic<T> where T : notnull
{
    public Generic(T value) { }
}
class C
{
    static void Main()
    {
        _ = new Generic<string>(null);
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("Generic<T>..ctor(T)", @"
{
      // Code size       25 (0x19)
      .maxstack  1
      IL_0000:  ldarg.1
      IL_0001:  box        ""T""
      IL_0006:  brtrue.s   IL_0012
      IL_0008:  ldstr      ""value""
      IL_000d:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
      IL_0012:  ldarg.0
      IL_0013:  call       ""object..ctor()""
      IL_0018:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
        public void Generics_Unconstrained_NoCheck()
        {
            const string source = @"
using System;
class Generic<T>
{
    public Generic(T value) { }
}
class C
{
    static void Main()
    {
        _ = new Generic<string>(null);
        Console.WriteLine(""ok"");
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
            verifier.VerifyIL("Generic<T>..ctor(T)", @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""object..ctor()""
  IL_0006:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
        public void Generics_StructConstraint_NoCheck()
        {
            const string source = @"
using System;
class Generic<T> where T : struct
{
    public Generic(T value) { }
}
class C
{
    static void Main()
    {
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp);
            verifier.VerifyIL("Generic<T>..ctor(T)", @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""object..ctor()""
  IL_0006:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
        public void Generics_NullableClassConstraint_NoCheck()
        {
            const string source = @"
using System;
class Generic<T> where T : class?
{
    public Generic(T value) { }
}
class C
{
    static void Main()
    {
        _ = new Generic<string>(null);
        Console.WriteLine(""ok"");
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
            verifier.VerifyIL("Generic<T>..ctor(T)", @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  call       ""object..ctor()""
  IL_0006:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
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
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.M(string)", @"
{
  // Code size       25 (0x19)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""s""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldarg.0
  IL_000e:  callvirt   ""int string.Length.get""
  IL_0013:  call       ""void System.Console.WriteLine(int)""
  IL_0018:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
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
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.M(string)", @"
{
  // Code size       28 (0x1c)
  .maxstack  3
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""s""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldc.i4.s   -2
  IL_000f:  newobj     ""C.<M>d__0..ctor(int)""
  IL_0014:  dup
  IL_0015:  ldarg.0
  IL_0016:  stfld      ""string C.<M>d__0.<>3__s""
  IL_001b:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
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
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.<M>d__0.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()", @"
{
  // Code size      162 (0xa2)
  .maxstack  3
  .locals init (int V_0,
                System.Runtime.CompilerServices.TaskAwaiter V_1,
                System.Exception V_2)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<M>d__0.<>1__state""
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldloc.0
    IL_0008:  brfalse.s  IL_0052
    IL_000a:  ldarg.0
    IL_000b:  ldfld      ""string C.<M>d__0.s""
    IL_0010:  brtrue.s   IL_001c
    IL_0012:  ldstr      ""s""
    IL_0017:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
    IL_001c:  ldc.i4.s   42
    IL_001e:  call       ""System.Threading.Tasks.Task System.Threading.Tasks.Task.Delay(int)""
    IL_0023:  callvirt   ""System.Runtime.CompilerServices.TaskAwaiter System.Threading.Tasks.Task.GetAwaiter()""
    IL_0028:  stloc.1
    IL_0029:  ldloca.s   V_1
    IL_002b:  call       ""bool System.Runtime.CompilerServices.TaskAwaiter.IsCompleted.get""
    IL_0030:  brtrue.s   IL_006e
    IL_0032:  ldarg.0
    IL_0033:  ldc.i4.0
    IL_0034:  dup
    IL_0035:  stloc.0
    IL_0036:  stfld      ""int C.<M>d__0.<>1__state""
    IL_003b:  ldarg.0
    IL_003c:  ldloc.1
    IL_003d:  stfld      ""System.Runtime.CompilerServices.TaskAwaiter C.<M>d__0.<>u__1""
    IL_0042:  ldarg.0
    IL_0043:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<M>d__0.<>t__builder""
    IL_0048:  ldloca.s   V_1
    IL_004a:  ldarg.0
    IL_004b:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter, C.<M>d__0>(ref System.Runtime.CompilerServices.TaskAwaiter, ref C.<M>d__0)""
    IL_0050:  leave.s    IL_00a1
    IL_0052:  ldarg.0
    IL_0053:  ldfld      ""System.Runtime.CompilerServices.TaskAwaiter C.<M>d__0.<>u__1""
    IL_0058:  stloc.1
    IL_0059:  ldarg.0
    IL_005a:  ldflda     ""System.Runtime.CompilerServices.TaskAwaiter C.<M>d__0.<>u__1""
    IL_005f:  initobj    ""System.Runtime.CompilerServices.TaskAwaiter""
    IL_0065:  ldarg.0
    IL_0066:  ldc.i4.m1
    IL_0067:  dup
    IL_0068:  stloc.0
    IL_0069:  stfld      ""int C.<M>d__0.<>1__state""
    IL_006e:  ldloca.s   V_1
    IL_0070:  call       ""void System.Runtime.CompilerServices.TaskAwaiter.GetResult()""
    IL_0075:  leave.s    IL_008e
  }
  catch System.Exception
  {
    IL_0077:  stloc.2
    IL_0078:  ldarg.0
    IL_0079:  ldc.i4.s   -2
    IL_007b:  stfld      ""int C.<M>d__0.<>1__state""
    IL_0080:  ldarg.0
    IL_0081:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<M>d__0.<>t__builder""
    IL_0086:  ldloc.2
    IL_0087:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetException(System.Exception)""
    IL_008c:  leave.s    IL_00a1
  }
  IL_008e:  ldarg.0
  IL_008f:  ldc.i4.s   -2
  IL_0091:  stfld      ""int C.<M>d__0.<>1__state""
  IL_0096:  ldarg.0
  IL_0097:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<M>d__0.<>t__builder""
  IL_009c:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetResult()""
  IL_00a1:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
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
        [Trait("Annotations", "Roslyn")]
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

            var comp = CreateCompilation(source, useAsyncStreams: true);
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.M(string)", @"
{
  // Code size       28 (0x1c)
  .maxstack  3
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""s""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldc.i4.s   -2
  IL_000f:  newobj     ""C.<M>d__1..ctor(int)""
  IL_0014:  dup
  IL_0015:  ldarg.0
  IL_0016:  stfld      ""string C.<M>d__1.<>3__s""
  IL_001b:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
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
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.<>c.<Main>b__0_0(string)", @"
{
  // Code size       25 (0x19)
  .maxstack  1
  IL_0000:  ldarg.1
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""s""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldarg.1
  IL_000e:  callvirt   ""int string.Length.get""
  IL_0013:  call       ""void System.Console.WriteLine(int)""
  IL_0018:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
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
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.<Main>g__printLength|0_0(string)", @"
{
  // Code size       25 (0x19)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""s""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldarg.0
  IL_000e:  callvirt   ""int string.Length.get""
  IL_0013:  call       ""void System.Console.WriteLine(int)""
  IL_0018:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
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
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.<Main>g__goo|0_0(string)", @"
{
  // Code size       28 (0x1c)
  .maxstack  3
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""s""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldc.i4.s   -2
  IL_000f:  newobj     ""C.<<Main>g__goo|0_0>d..ctor(int)""
  IL_0014:  dup
  IL_0015:  ldarg.0
  IL_0016:  stfld      ""string C.<<Main>g__goo|0_0>d.<>3__s""
  IL_001b:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
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
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.<<Main>g__goo|0_0>d.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()", @"
{
  // Code size      162 (0xa2)
  .maxstack  3
  .locals init (int V_0,
                System.Runtime.CompilerServices.TaskAwaiter V_1,
                System.Exception V_2)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<<Main>g__goo|0_0>d.<>1__state""
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldloc.0
    IL_0008:  brfalse.s  IL_0052
    IL_000a:  ldarg.0
    IL_000b:  ldfld      ""string C.<<Main>g__goo|0_0>d.s""
    IL_0010:  brtrue.s   IL_001c
    IL_0012:  ldstr      ""s""
    IL_0017:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
    IL_001c:  ldc.i4.s   42
    IL_001e:  call       ""System.Threading.Tasks.Task System.Threading.Tasks.Task.Delay(int)""
    IL_0023:  callvirt   ""System.Runtime.CompilerServices.TaskAwaiter System.Threading.Tasks.Task.GetAwaiter()""
    IL_0028:  stloc.1
    IL_0029:  ldloca.s   V_1
    IL_002b:  call       ""bool System.Runtime.CompilerServices.TaskAwaiter.IsCompleted.get""
    IL_0030:  brtrue.s   IL_006e
    IL_0032:  ldarg.0
    IL_0033:  ldc.i4.0
    IL_0034:  dup
    IL_0035:  stloc.0
    IL_0036:  stfld      ""int C.<<Main>g__goo|0_0>d.<>1__state""
    IL_003b:  ldarg.0
    IL_003c:  ldloc.1
    IL_003d:  stfld      ""System.Runtime.CompilerServices.TaskAwaiter C.<<Main>g__goo|0_0>d.<>u__1""
    IL_0042:  ldarg.0
    IL_0043:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<<Main>g__goo|0_0>d.<>t__builder""
    IL_0048:  ldloca.s   V_1
    IL_004a:  ldarg.0
    IL_004b:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter, C.<<Main>g__goo|0_0>d>(ref System.Runtime.CompilerServices.TaskAwaiter, ref C.<<Main>g__goo|0_0>d)""
    IL_0050:  leave.s    IL_00a1
    IL_0052:  ldarg.0
    IL_0053:  ldfld      ""System.Runtime.CompilerServices.TaskAwaiter C.<<Main>g__goo|0_0>d.<>u__1""
    IL_0058:  stloc.1
    IL_0059:  ldarg.0
    IL_005a:  ldflda     ""System.Runtime.CompilerServices.TaskAwaiter C.<<Main>g__goo|0_0>d.<>u__1""
    IL_005f:  initobj    ""System.Runtime.CompilerServices.TaskAwaiter""
    IL_0065:  ldarg.0
    IL_0066:  ldc.i4.m1
    IL_0067:  dup
    IL_0068:  stloc.0
    IL_0069:  stfld      ""int C.<<Main>g__goo|0_0>d.<>1__state""
    IL_006e:  ldloca.s   V_1
    IL_0070:  call       ""void System.Runtime.CompilerServices.TaskAwaiter.GetResult()""
    IL_0075:  leave.s    IL_008e
  }
  catch System.Exception
  {
    IL_0077:  stloc.2
    IL_0078:  ldarg.0
    IL_0079:  ldc.i4.s   -2
    IL_007b:  stfld      ""int C.<<Main>g__goo|0_0>d.<>1__state""
    IL_0080:  ldarg.0
    IL_0081:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<<Main>g__goo|0_0>d.<>t__builder""
    IL_0086:  ldloc.2
    IL_0087:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetException(System.Exception)""
    IL_008c:  leave.s    IL_00a1
  }
  IL_008e:  ldarg.0
  IL_008f:  ldc.i4.s   -2
  IL_0091:  stfld      ""int C.<<Main>g__goo|0_0>d.<>1__state""
  IL_0096:  ldarg.0
  IL_0097:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<<Main>g__goo|0_0>d.<>t__builder""
  IL_009c:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetResult()""
  IL_00a1:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
        public void PropertySetter_BlockBody()
        {
            const string source = @"
class C
{
    static string s_backingField;

    static void Main()
    {
        Prop = null;
    }

    static string Prop
    {
        get
        {
            return s_backingField;
        }
        set
        {
            s_backingField = value;
        }
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.Prop.set", @"
{
  // Code size       20 (0x14)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""value""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldarg.0
  IL_000e:  stsfld     ""string C.s_backingField""
  IL_0013:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
        public void PropertySetter_ExpressionBody()
        {
            const string source = @"
class C
{
    static string s_backingField;

    static void Main()
    {
        Prop = null;
    }

    static string Prop
    {
        get => s_backingField;
        set => s_backingField = value;
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.Prop.set", @"
{
  // Code size       20 (0x14)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""value""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldarg.0
  IL_000e:  stsfld     ""string C.s_backingField""
  IL_0013:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
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
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.Prop.set", @"
{
  // Code size       20 (0x14)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""value""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldarg.0
  IL_000e:  stsfld     ""string C.<Prop>k__BackingField""
  IL_0013:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
        public void Ctor_WithFieldInitializers()
        {
            const string source = @"
using System;
class C
{
    private int _field = 42;

    public C(string s)
    {
    }

    static void Main()
    {
        var c = new C(null);
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C..ctor(string)", @"
 {
  // Code size       28 (0x1c)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldc.i4.s   42
  IL_0003:  stfld      ""int C._field""
  IL_0008:  ldarg.1
  IL_0009:  brtrue.s   IL_0015
  IL_000b:  ldstr      ""s""
  IL_0010:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_0015:  ldarg.0
  IL_0016:  call       ""object..ctor()""
  IL_001b:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
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
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.this[string].get", @"
{
  // Code size       20 (0x14)
  .maxstack  1
  IL_0000:  ldarg.1
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""s""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldarg.1
  IL_000e:  callvirt   ""int string.Length.get""
  IL_0013:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "Roslyn")]
        public void AllowNull_Property_NoCheck()
        {
            const string source = @"
using System;
using System.Diagnostics.CodeAnalysis;
class C
{
    static string s_field;

    static void Main()
    {
        Prop = null;
        Console.WriteLine(""ok"");
    }

    [AllowNull]
    static string Prop
    {
        get => s_field;
        set
        {
            s_field = value ?? """";
        }
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
            verifier.VerifyIL("C.Prop.set", expectedIL: @"
{
  // Code size       16 (0x10)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  dup
  IL_0002:  brtrue.s   IL_000a
  IL_0004:  pop
  IL_0005:  ldstr      """"
  IL_000a:  stsfld     ""string C.s_field""
  IL_000f:  ret
}");
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

        private static CSharpCompilation CreateCompilation(string source, bool nullableContext = true, bool useAsyncStreams = false)
        {
            var sources = new List<string> { source, AllowNullAttributeDefinition, JetBrainsAnnotations };
            if (useAsyncStreams)
            {
                sources.Add(AsyncStreamsTypes);
            }
            return CreateCompilationWithTasksExtensions(sources.ToArray(), options: WithRuntimeChecks(nullableContext));
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
