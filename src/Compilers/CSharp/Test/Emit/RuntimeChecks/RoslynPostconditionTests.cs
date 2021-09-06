// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.RuntimeChecks
{
    public sealed class RoslynPostconditionTests : RuntimeCheckTestsBase
    {
        [Fact]
        public void ReturnValue_NotAnnotated()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        M();
    }

    static object M() => null;
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.M", @"
{
  // Code size       10 (0xa)
  .maxstack  2
  IL_0000:  ldnull
  IL_0001:  dup
  IL_0002:  brtrue.s   IL_0009
  IL_0004:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_0009:  ret
}");
        }

        [Fact]
        public void ReturnValue_NotAnnotated_NonConstExpr()
        {
            const string source = @"
using System;
using System.Diagnostics.CodeAnalysis;
class C
{
    static object s_field = null!;

    static void Main()
    {
        M();
    }

    static object M() => s_field?.ToString();
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.M", @"
{
  // Code size       26 (0x1a)
  .maxstack  2
  IL_0000:  ldsfld     ""object C.s_field""
  IL_0005:  dup
  IL_0006:  brtrue.s   IL_000c
  IL_0008:  pop
  IL_0009:  ldnull
  IL_000a:  br.s       IL_0011
  IL_000c:  callvirt   ""string object.ToString()""
  IL_0011:  dup
  IL_0012:  brtrue.s   IL_0019
  IL_0014:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_0019:  ret
}");
        }

        [Fact]
        public void ReturnValue_NotNull()
        {
            const string source = @"
using System;
using System.Diagnostics.CodeAnalysis;
class C
{
    static void Main()
    {
        M();
    }

    [return: NotNull]
    static object? M() => null;
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.M", @"
{
  // Code size       10 (0xa)
  .maxstack  2
  IL_0000:  ldnull
  IL_0001:  dup
  IL_0002:  brtrue.s   IL_0009
  IL_0004:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_0009:  ret
}");
        }

        [Fact]
        public void RefReturnValue()
        {
            const string source = @"
class C
{
    static object s_field = null!;

    static void Main()
    {
        M();
    }

    static ref object M() => ref s_field;
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.M", @"
{
  // Code size       15 (0xf)
  .maxstack  2
  IL_0000:  ldsflda    ""object C.s_field""
  IL_0005:  dup
  IL_0006:  ldind.ref
  IL_0007:  brtrue.s   IL_000e
  IL_0009:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_000e:  ret
}");
        }

        [Fact]
        public void RefParameter_NotAnnotated()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        string s = """";
        M(ref s);
    }

    static void M(ref string s)
    {
        s = null;
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentException>(comp);
            verifier.VerifyIL("C.M", @"
{
  // Code size       18 (0x12)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldnull
  IL_0002:  stind.ref
  IL_0003:  ldarg.0
  IL_0004:  ldind.ref
  IL_0005:  brtrue.s   IL_0011
  IL_0007:  ldstr      ""s""
  IL_000c:  call       ""void System.Runtime.CompilerServices.ThrowHelper.OutParameterNull(string)""
  IL_0011:  ret
}");
        }

        [Fact]
        public void RefParameter_NotNull()
        {
            const string source = @"
using System;
using System.Diagnostics.CodeAnalysis;

class C
{
    static void Main()
    {
        string s = """";
        M(ref s);
    }

    static void M([NotNull] ref string? s)
    {
        s = null;
    }
}";
            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentException>(comp);
            verifier.VerifyIL("C.M", @"
{
  // Code size       18 (0x12)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldnull
  IL_0002:  stind.ref
  IL_0003:  ldarg.0
  IL_0004:  ldind.ref
  IL_0005:  brtrue.s   IL_0011
  IL_0007:  ldstr      ""s""
  IL_000c:  call       ""void System.Runtime.CompilerServices.ThrowHelper.OutParameterNull(string)""
  IL_0011:  ret
}");
        }

        [Fact]
        public void MultipleRefParameters()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        string s1 = """";
        string s2 = """";
        M(ref s1, ref s2);
    }

    static void M(ref string s1, ref string s2)
    {
        s1 = ""not null"";
        s2 = null;
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentException>(comp);
            verifier.VerifyIL("C.M", @"
            {
  // Code size       39 (0x27)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldstr      ""not null""
  IL_0006:  stind.ref
  IL_0007:  ldarg.1
  IL_0008:  ldnull
  IL_0009:  stind.ref
  IL_000a:  ldarg.0
  IL_000b:  ldind.ref
  IL_000c:  brtrue.s   IL_0018
  IL_000e:  ldstr      ""s1""
  IL_0013:  call       ""void System.Runtime.CompilerServices.ThrowHelper.OutParameterNull(string)""
  IL_0018:  ldarg.1
  IL_0019:  ldind.ref
  IL_001a:  brtrue.s   IL_0026
  IL_001c:  ldstr      ""s2""
  IL_0021:  call       ""void System.Runtime.CompilerServices.ThrowHelper.OutParameterNull(string)""
  IL_0026:  ret
}");
        }

        [Fact]
        public void RefParameter_MultipleReturnPoints()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        string s = """";
        M(ref s);
    }

    static void M(ref string s)
    {
        if (s == """")
        {
            s = null;
            return;
        }
        s = null;
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentException>(comp);
            verifier.VerifyIL("C.M", @"
            {
  // Code size       50 (0x32)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldind.ref
  IL_0002:  ldstr      """"
  IL_0007:  call       ""bool string.op_Equality(string, string)""
  IL_000c:  brfalse.s  IL_0020
  IL_000e:  ldarg.0
  IL_000f:  ldnull
  IL_0010:  stind.ref
  IL_0011:  ldarg.0
  IL_0012:  ldind.ref
  IL_0013:  brtrue.s   IL_001f
  IL_0015:  ldstr      ""s""
  IL_001a:  call       ""void System.Runtime.CompilerServices.ThrowHelper.OutParameterNull(string)""
  IL_001f:  ret
  IL_0020:  ldarg.0
  IL_0021:  ldnull
  IL_0022:  stind.ref
  IL_0023:  ldarg.0
  IL_0024:  ldind.ref
  IL_0025:  brtrue.s   IL_0031
  IL_0027:  ldstr      ""s""
  IL_002c:  call       ""void System.Runtime.CompilerServices.ThrowHelper.OutParameterNull(string)""
  IL_0031:  ret
}");
        }

        [Fact]
        public void AutoPropertyGetter()
        {
            const string source = @"
using System;
class C
{
    static string Prop { get; } = null!;
    static void Main()
    {
        _ = Prop;
    }
}";
            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.Prop.get", @"
{
  // Code size       14 (0xe)
  .maxstack  2
  IL_0000:  ldsfld     ""string C.<Prop>k__BackingField""
  IL_0005:  dup
  IL_0006:  brtrue.s   IL_000d
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_000d:  ret
}");
        }

        [Fact]
        public void GetOnlyProperty()
        {
            const string source = @"
using System;
class C
{
    static string Prop => null!;
    static void Main()
    {
        _ = Prop;
    }
}";
            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.Prop.get", @"
{
  // Code size       10 (0xa)
  .maxstack  2
  IL_0000:  ldnull
  IL_0001:  dup
  IL_0002:  brtrue.s   IL_0009
  IL_0004:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_0009:  ret
}");
        }

        [Fact]
        public void Indexer()
        {
            const string source = @"
using System;
class C
{
    public string this[int i] => null!;

    static void Main()
    {
        var c = new C();
        _ = c[0];
    }
}";
            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.this[int].get", @"
            {
  // Code size       10 (0xa)
  .maxstack  2
  IL_0000:  ldnull
  IL_0001:  dup
  IL_0002:  brtrue.s   IL_0009
  IL_0004:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_0009:  ret
}");
        }

        [Fact]
        public void GenericT_ReturnValue()
        {
            const string source = @"
using System;

class Generic<T> where T : class
{
    private readonly T _field = default;

    public T Ret => _field;
}

class C
{
    static void Main()
    {
        var g = new Generic<string>();
        _ = g.Ret;
    }    
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
        }

        [Fact]
        public void GenericT_ReturnValue_Annotated_NoCheck()
        {
            const string source = @"
using System;

class Generic<T> where T : class
{
    private readonly T _field = default;

    public T? Ret => _field;
}

class C
{
    static void Main()
    {
        var g = new Generic<string>();
        _ = g.Ret;
        Console.WriteLine(""ok"");
    }    
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
        }

        [Fact]
        public void GenericRefT_Parameter()
        {
            const string source = @"
using System;
class Generic<T> where T : class
{
    public void Init(ref T value) => value = null;
}
class C
{
    static void Main()
    {
        var g = new Generic<string>();
        string s = string.Empty;
        g.Init(ref s);
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentException>(comp);
            verifier.VerifyIL("Generic<T>.Init(ref T)", @"
{
  // Code size       31 (0x1f)
  .maxstack  1
  IL_0000:  ldarg.1
  IL_0001:  initobj    ""T""
  IL_0007:  ldarg.1
  IL_0008:  ldobj      ""T""
  IL_000d:  box        ""T""
  IL_0012:  brtrue.s   IL_001e
  IL_0014:  ldstr      ""value""
  IL_0019:  call       ""void System.Runtime.CompilerServices.ThrowHelper.OutParameterNull(string)""
  IL_001e:  ret
}");
        }

        [Fact]
        public void GenericRefT_Parameter_Annotated_NoCheck()
        {
            const string source = @"
using System;
class Generic<T> where T : class
{
    public void Init(ref T? value) => value = null;
}
class C
{
    static void Main()
    {
        var g = new Generic<string>();
        string? s = string.Empty;
        g.Init(ref s);
        Console.WriteLine(""ok"");
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
            verifier.VerifyIL("Generic<T>.Init(ref T)", @"
{
  // Code size        8 (0x8)
  .maxstack  1
  IL_0000:  ldarg.1
  IL_0001:  initobj    ""T""
  IL_0007:  ret
}");
        }

        [Fact]
        public void GenericRefT_Parameter_NullableConstraint_NoCheck()
        {
            const string source = @"
using System;
class Generic<T> where T : class?
{
    public void Init(ref T value) => value = null;
}
class C
{
    static void Main()
    {
        var g = new Generic<string?>();
        string? s = null;
        g.Init(ref s);
        Console.WriteLine(""ok"");
    }
}";
            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
            verifier.VerifyIL("Generic<T>.Init(ref T)", @"
{
  // Code size        8 (0x8)
  .maxstack  1
  IL_0000:  ldarg.1
  IL_0001:  initobj    ""T""
  IL_0007:  ret
}");
        }

        [Fact]
        public void Iterator_NotAnnotatedT()
        {
            const string source = @"
using System;
using System.Collections.Generic;
class C
{
    static void Main()
    {
        foreach (string s in Iter())
        {
            Console.WriteLine(s);
        }
    }

    static IEnumerable<string> Iter()
    {
        yield return ""meow"";
        yield return null;
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp, expectedOutput: "meow");
            verifier.VerifyIL("C.<Iter>d__1.System.Collections.IEnumerator.MoveNext()", @"
{
  // Code size      106 (0x6a)
  .maxstack  2
  .locals init (int V_0,
                string V_1)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<Iter>d__1.<>1__state""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  switch    (
        IL_001b,
        IL_0040,
        IL_0061)
  IL_0019:  ldc.i4.0
  IL_001a:  ret
  IL_001b:  ldarg.0
  IL_001c:  ldc.i4.m1
  IL_001d:  stfld      ""int C.<Iter>d__1.<>1__state""
  IL_0022:  ldstr      ""meow""
  IL_0027:  stloc.1
  IL_0028:  ldloc.1
  IL_0029:  brtrue.s   IL_0030
  IL_002b:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_0030:  ldarg.0
  IL_0031:  ldloc.1
  IL_0032:  stfld      ""string C.<Iter>d__1.<>2__current""
  IL_0037:  ldarg.0
  IL_0038:  ldc.i4.1
  IL_0039:  stfld      ""int C.<Iter>d__1.<>1__state""
  IL_003e:  ldc.i4.1
  IL_003f:  ret
  IL_0040:  ldarg.0
  IL_0041:  ldc.i4.m1
  IL_0042:  stfld      ""int C.<Iter>d__1.<>1__state""
  IL_0047:  ldnull
  IL_0048:  stloc.1
  IL_0049:  ldloc.1
  IL_004a:  brtrue.s   IL_0051
  IL_004c:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_0051:  ldarg.0
  IL_0052:  ldloc.1
  IL_0053:  stfld      ""string C.<Iter>d__1.<>2__current""
  IL_0058:  ldarg.0
  IL_0059:  ldc.i4.2
  IL_005a:  stfld      ""int C.<Iter>d__1.<>1__state""
  IL_005f:  ldc.i4.1
  IL_0060:  ret
  IL_0061:  ldarg.0
  IL_0062:  ldc.i4.m1
  IL_0063:  stfld      ""int C.<Iter>d__1.<>1__state""
  IL_0068:  ldc.i4.0
  IL_0069:  ret
}");
        }

        [Fact]
        public void Iterator_NullableT_NoCheck()
        {
            const string source = @"
using System;
using System.Collections.Generic;
class C
{
    static void Main()
    {
        foreach (string s in Iter())
        {
            Console.WriteLine(""ok"");
        }
    }

    static IEnumerable<string?> Iter()
    {
        yield return null;
    }
}";

            var comp = CreateCompilation(source);
            CompileAndVerify(comp, expectedOutput: "ok");
        }

        [Fact]
        public void IEnumerable_ReturnLocal()
        {
            const string source = @"
using System;
using System.Collections.Generic;
using System.Linq;
class C
{
    static void Main()
    {
        _ = Iter();
    }

    static IEnumerable<string> Iter()
    {
        var res = new[] { 1, 2, 3 }.Select(x => x.ToString());
        return res;
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp);
        }

        [Fact]
        public void Async_ReturnValue_NotAnnotatedT()
        {
            const string source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await Async();
    }

    static async Task<string> Async()
    {
        await Task.Delay(10);
        return null;
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.<Main>d__0.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()", @"
{
  // Code size      143 (0x8f)
  .maxstack  3
  .locals init (int V_0,
                System.Runtime.CompilerServices.TaskAwaiter<string> V_1,
                System.Exception V_2)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<Main>d__0.<>1__state""
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldloc.0
    IL_0008:  brfalse.s  IL_003e
    IL_000a:  call       ""System.Threading.Tasks.Task<string> C.Async()""
    IL_000f:  callvirt   ""System.Runtime.CompilerServices.TaskAwaiter<string> System.Threading.Tasks.Task<string>.GetAwaiter()""
    IL_0014:  stloc.1
    IL_0015:  ldloca.s   V_1
    IL_0017:  call       ""bool System.Runtime.CompilerServices.TaskAwaiter<string>.IsCompleted.get""
    IL_001c:  brtrue.s   IL_005a
    IL_001e:  ldarg.0
    IL_001f:  ldc.i4.0
    IL_0020:  dup
    IL_0021:  stloc.0
    IL_0022:  stfld      ""int C.<Main>d__0.<>1__state""
    IL_0027:  ldarg.0
    IL_0028:  ldloc.1
    IL_0029:  stfld      ""System.Runtime.CompilerServices.TaskAwaiter<string> C.<Main>d__0.<>u__1""
    IL_002e:  ldarg.0
    IL_002f:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
    IL_0034:  ldloca.s   V_1
    IL_0036:  ldarg.0
    IL_0037:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<System.Runtime.CompilerServices.TaskAwaiter<string>, C.<Main>d__0>(ref System.Runtime.CompilerServices.TaskAwaiter<string>, ref C.<Main>d__0)""
    IL_003c:  leave.s    IL_008e
    IL_003e:  ldarg.0
    IL_003f:  ldfld      ""System.Runtime.CompilerServices.TaskAwaiter<string> C.<Main>d__0.<>u__1""
    IL_0044:  stloc.1
    IL_0045:  ldarg.0
    IL_0046:  ldflda     ""System.Runtime.CompilerServices.TaskAwaiter<string> C.<Main>d__0.<>u__1""
    IL_004b:  initobj    ""System.Runtime.CompilerServices.TaskAwaiter<string>""
    IL_0051:  ldarg.0
    IL_0052:  ldc.i4.m1
    IL_0053:  dup
    IL_0054:  stloc.0
    IL_0055:  stfld      ""int C.<Main>d__0.<>1__state""
    IL_005a:  ldloca.s   V_1
    IL_005c:  call       ""string System.Runtime.CompilerServices.TaskAwaiter<string>.GetResult()""
    IL_0061:  pop
    IL_0062:  leave.s    IL_007b
  }
  catch System.Exception
  {
    IL_0064:  stloc.2
    IL_0065:  ldarg.0
    IL_0066:  ldc.i4.s   -2
    IL_0068:  stfld      ""int C.<Main>d__0.<>1__state""
    IL_006d:  ldarg.0
    IL_006e:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
    IL_0073:  ldloc.2
    IL_0074:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetException(System.Exception)""
    IL_0079:  leave.s    IL_008e
  }
  IL_007b:  ldarg.0
  IL_007c:  ldc.i4.s   -2
  IL_007e:  stfld      ""int C.<Main>d__0.<>1__state""
  IL_0083:  ldarg.0
  IL_0084:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder C.<Main>d__0.<>t__builder""
  IL_0089:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder.SetResult()""
  IL_008e:  ret
}");
        }

        [Fact]
        public void Async_ReturnValue_NullableT_NoCheck()
        {
            const string source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
class C
{
    static async Task Main()
    {
        await Async();
        Console.WriteLine(""ok"");
        }

    static async Task<string?> Async()
    {
        await Task.Delay(10);
        return null;
    }
}";

            var comp = CreateCompilation(source);
            CompileAndVerify(comp, expectedOutput: "ok");
        }

        [Fact]
        public void LambdaReturn()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        Func<string> lambda = () => null;
        lambda();
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);

        }

        private CSharpCompilation CreateCompilation(string source, bool nullableContext = true, bool useAsyncStreams = false)
            => CreateCompilation(source, RuntimeChecksMode.PostconditionsOnly, nullableContext, useAsyncStreams);
    }
}
