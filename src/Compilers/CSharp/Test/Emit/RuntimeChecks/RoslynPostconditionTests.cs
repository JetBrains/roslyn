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
        public void ReturnNullConst()
        {
            const string source = @"
using System;
class C
{
    private const string Const = null!;

    static void Main()
    {
        M();
    }

    static string M() => Const;
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
        public void ReturnNonNullConst_NoCheck()
        {
            const string source = @"
using System;
class C
{
    private const string Const = ""meow"";

    static void Main()
    {
        Console.WriteLine(M());
    }

    static string M() => Const;
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "meow");
            verifier.VerifyIL("C.M", @"
{
  // Code size        6 (0x6)
  .maxstack  1
  IL_0000:  ldstr      ""meow""
  IL_0005:  ret
}");
        }

        [Fact]
        public void ReturnValue_This_ImplicitConversion()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        var c = new C();
        _ = c.Return();
    }

    public static implicit operator string(C c) => null!;
    public string Return() => this;
}";
            var comp = CreateCompilation(source);
            var diags = comp.GetEmitDiagnostics();
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.Return", @"
{
  // Code size       15 (0xf)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  call       ""string C.op_Implicit(C)""
  IL_0006:  dup
  IL_0007:  brtrue.s   IL_000e
  IL_0009:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_000e:  ret
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
        public void RefReadonlyReturnValue()
        {
            const string source = @"
class C
{
    static object s_field = null!;

    static void Main()
    {
        M();
    }

    static ref readonly object M() => ref s_field;
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
        s = null!;
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
        s = null!;
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
        s2 = null!;
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
            s = null!;
            return;
        }
        s = null!;
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
        public void OutParameter_NotAnnotated()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        string s;
        M(out s);
    }

    static void M(out string s)
    {
        s = null!;
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
        public void Indexer_ReturnValue()
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
            verifier.VerifyIL("Generic<T>.Ret.get", @"
{
  // Code size       20 (0x14)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""T Generic<T>._field""
  IL_0006:  dup
  IL_0007:  box        ""T""
  IL_000c:  brtrue.s   IL_0013
  IL_000e:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_0013:  ret
}");
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
            verifier.VerifyIL("Generic<T>.Ret.get", @"
{
  // Code size        7 (0x7)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""T Generic<T>._field""
  IL_0006:  ret
}");
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
        public void Iterator_YieldReturn_NotAnnotatedT()
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
  // Code size       96 (0x60)
  .maxstack  2
  .locals init (int V_0,
                string V_1)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<Iter>d__1.<>1__state""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  switch    (
        IL_001b,
        IL_0036,
        IL_0057)
  IL_0019:  ldc.i4.0
  IL_001a:  ret
  IL_001b:  ldarg.0
  IL_001c:  ldc.i4.m1
  IL_001d:  stfld      ""int C.<Iter>d__1.<>1__state""
  IL_0022:  ldarg.0
  IL_0023:  ldstr      ""meow""
  IL_0028:  stfld      ""string C.<Iter>d__1.<>2__current""
  IL_002d:  ldarg.0
  IL_002e:  ldc.i4.1
  IL_002f:  stfld      ""int C.<Iter>d__1.<>1__state""
  IL_0034:  ldc.i4.1
  IL_0035:  ret
  IL_0036:  ldarg.0
  IL_0037:  ldc.i4.m1
  IL_0038:  stfld      ""int C.<Iter>d__1.<>1__state""
  IL_003d:  ldnull
  IL_003e:  stloc.1
  IL_003f:  ldloc.1
  IL_0040:  brtrue.s   IL_0047
  IL_0042:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_0047:  ldarg.0
  IL_0048:  ldloc.1
  IL_0049:  stfld      ""string C.<Iter>d__1.<>2__current""
  IL_004e:  ldarg.0
  IL_004f:  ldc.i4.2
  IL_0050:  stfld      ""int C.<Iter>d__1.<>1__state""
  IL_0055:  ldc.i4.1
  IL_0056:  ret
  IL_0057:  ldarg.0
  IL_0058:  ldc.i4.m1
  IL_0059:  stfld      ""int C.<Iter>d__1.<>1__state""
  IL_005e:  ldc.i4.0
  IL_005f:  ret
}");
        }

        [Fact]
        public void Iterator_YieldReturn_NullableT_NoCheck()
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
        return null!;
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.<Async>d__1.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()", @"
{
  // Code size       56 (0x38)
  .maxstack  2
  .locals init (string V_0,
                System.Exception V_1)
  .try
  {
    IL_0000:  ldnull
    IL_0001:  dup
    IL_0002:  brtrue.s   IL_0009
    IL_0004:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
    IL_0009:  stloc.0
    IL_000a:  leave.s    IL_0023
  }
  catch System.Exception
  {
    IL_000c:  stloc.1
    IL_000d:  ldarg.0
    IL_000e:  ldc.i4.s   -2
    IL_0010:  stfld      ""int C.<Async>d__1.<>1__state""
    IL_0015:  ldarg.0
    IL_0016:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string> C.<Async>d__1.<>t__builder""
    IL_001b:  ldloc.1
    IL_001c:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string>.SetException(System.Exception)""
    IL_0021:  leave.s    IL_0037
  }
  IL_0023:  ldarg.0
  IL_0024:  ldc.i4.s   -2
  IL_0026:  stfld      ""int C.<Async>d__1.<>1__state""
  IL_002b:  ldarg.0
  IL_002c:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string> C.<Async>d__1.<>t__builder""
  IL_0031:  ldloc.0
  IL_0032:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string>.SetResult(string)""
  IL_0037:  ret
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
        return null;
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerify(comp, expectedOutput: "ok");
            verifier.VerifyIL("C.<Async>d__1.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()", @"
{
  // Code size       48 (0x30)
  .maxstack  2
  .locals init (string V_0,
                System.Exception V_1)
  .try
  {
    IL_0000:  ldnull
    IL_0001:  stloc.0
    IL_0002:  leave.s    IL_001b
  }
  catch System.Exception
  {
    IL_0004:  stloc.1
    IL_0005:  ldarg.0
    IL_0006:  ldc.i4.s   -2
    IL_0008:  stfld      ""int C.<Async>d__1.<>1__state""
    IL_000d:  ldarg.0
    IL_000e:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string> C.<Async>d__1.<>t__builder""
    IL_0013:  ldloc.1
    IL_0014:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string>.SetException(System.Exception)""
    IL_0019:  leave.s    IL_002f
  }
  IL_001b:  ldarg.0
  IL_001c:  ldc.i4.s   -2
  IL_001e:  stfld      ""int C.<Async>d__1.<>1__state""
  IL_0023:  ldarg.0
  IL_0024:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string> C.<Async>d__1.<>t__builder""
  IL_0029:  ldloc.0
  IL_002a:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string>.SetResult(string)""
  IL_002f:  ret
}");
        }

        [Fact]
        public void AsyncEnumerable_YieldReturn_NotAnnotatedT()
        {
            const string source = @"
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class C
{
    static async Task Main()
    {
        await foreach (string s in M())
        {
        }
    }

    static async IAsyncEnumerable<string> M()
    {
        yield return null!;
    }
}";

            var comp = CreateCompilation(source, useAsyncStreams: true);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.<M>d__1.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()", @"
{
  // Code size      162 (0xa2)
  .maxstack  3
  .locals init (int V_0,
                string V_1,
                System.Exception V_2)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<M>d__1.<>1__state""
  IL_0006:  stloc.0
  .try
  {
    IL_0007:  ldloc.0
    IL_0008:  ldc.i4.s   -4
    IL_000a:  beq.s      IL_0041
    IL_000c:  ldloc.0
    IL_000d:  ldc.i4.s   -3
    IL_000f:  pop
    IL_0010:  pop
    IL_0011:  ldarg.0
    IL_0012:  ldfld      ""bool C.<M>d__1.<>w__disposeMode""
    IL_0017:  brfalse.s  IL_001b
    IL_0019:  leave.s    IL_0075
    IL_001b:  ldarg.0
    IL_001c:  ldc.i4.m1
    IL_001d:  dup
    IL_001e:  stloc.0
    IL_001f:  stfld      ""int C.<M>d__1.<>1__state""
    IL_0024:  ldnull
    IL_0025:  stloc.1
    IL_0026:  ldloc.1
    IL_0027:  brtrue.s   IL_002e
    IL_0029:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
    IL_002e:  ldarg.0
    IL_002f:  ldloc.1
    IL_0030:  stfld      ""string C.<M>d__1.<>2__current""
    IL_0035:  ldarg.0
    IL_0036:  ldc.i4.s   -4
    IL_0038:  dup
    IL_0039:  stloc.0
    IL_003a:  stfld      ""int C.<M>d__1.<>1__state""
    IL_003f:  leave.s    IL_0095
    IL_0041:  ldarg.0
    IL_0042:  ldc.i4.m1
    IL_0043:  dup
    IL_0044:  stloc.0
    IL_0045:  stfld      ""int C.<M>d__1.<>1__state""
    IL_004a:  ldarg.0
    IL_004b:  ldfld      ""bool C.<M>d__1.<>w__disposeMode""
    IL_0050:  pop
    IL_0051:  leave.s    IL_0075
  }
  catch System.Exception
  {
    IL_0053:  stloc.2
    IL_0054:  ldarg.0
    IL_0055:  ldc.i4.s   -2
    IL_0057:  stfld      ""int C.<M>d__1.<>1__state""
    IL_005c:  ldarg.0
    IL_005d:  ldflda     ""System.Runtime.CompilerServices.AsyncIteratorMethodBuilder C.<M>d__1.<>t__builder""
    IL_0062:  call       ""void System.Runtime.CompilerServices.AsyncIteratorMethodBuilder.Complete()""
    IL_0067:  ldarg.0
    IL_0068:  ldflda     ""System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore<bool> C.<M>d__1.<>v__promiseOfValueOrEnd""
    IL_006d:  ldloc.2
    IL_006e:  call       ""void System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore<bool>.SetException(System.Exception)""
    IL_0073:  leave.s    IL_00a1
  }
  IL_0075:  ldarg.0
  IL_0076:  ldc.i4.s   -2
  IL_0078:  stfld      ""int C.<M>d__1.<>1__state""
  IL_007d:  ldarg.0
  IL_007e:  ldflda     ""System.Runtime.CompilerServices.AsyncIteratorMethodBuilder C.<M>d__1.<>t__builder""
  IL_0083:  call       ""void System.Runtime.CompilerServices.AsyncIteratorMethodBuilder.Complete()""
  IL_0088:  ldarg.0
  IL_0089:  ldflda     ""System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore<bool> C.<M>d__1.<>v__promiseOfValueOrEnd""
  IL_008e:  ldc.i4.0
  IL_008f:  call       ""void System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore<bool>.SetResult(bool)""
  IL_0094:  ret
  IL_0095:  ldarg.0
  IL_0096:  ldflda     ""System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore<bool> C.<M>d__1.<>v__promiseOfValueOrEnd""
  IL_009b:  ldc.i4.1
  IL_009c:  call       ""void System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore<bool>.SetResult(bool)""
  IL_00a1:  ret
}");
        }

        [Fact]
        public void Lambda_ReturnValue()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        Func<string> f = () => null;
        f();
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.<>c.<Main>b__0_0", @"
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
        public void LocalFunction_ReturnValue()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        static string localFunc() => null!;
        _ = localFunc();
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<InvalidOperationException>(comp);
            verifier.VerifyIL("C.<Main>g__localFunc|0_0()", @"
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
        public void LocalFunction_RefParameter()
        {
            const string source = @"
using System;
class C
{
    static void Main()
    {
        static void init(ref string s) => s = null!;
        string s = string.Empty;
        init(ref s);
    }
}";

            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentException>(comp);
            verifier.VerifyIL("C.<Main>g__init|0_0(ref string)", @"
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

        private CSharpCompilation CreateCompilation(string source, bool nullableContext = true, bool useAsyncStreams = false)
            => CreateCompilation(source, RuntimeChecksMode.PostconditionsOnly, nullableContext, useAsyncStreams);
    }
}
