// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.RuntimeChecks
{
    public sealed class AllChecksEnabledTests : RuntimeCheckTestsBase
    {
        [Fact]
        public void Iterator()
        {
            const string source = @"
using System;
using System.Collections.Generic;

class C
{
    static IEnumerable<string> M(string s)
    {
        yield return null!;
    }
    static void Main()
    {
        _ = M(null);
    }
}";
            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.M(string)", @"
{
  // Code size       21 (0x15)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""s""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldc.i4.s   -2
  IL_000f:  newobj     ""C.<M>d__0..ctor(int)""
  IL_0014:  ret
}");
            verifier.VerifyIL("C.<M>d__0.System.Collections.IEnumerator.MoveNext()", @"
{
  // Code size       58 (0x3a)
  .maxstack  2
  .locals init (int V_0,
                string V_1)
  IL_0000:  ldarg.0
  IL_0001:  ldfld      ""int C.<M>d__0.<>1__state""
  IL_0006:  stloc.0
  IL_0007:  ldloc.0
  IL_0008:  brfalse.s  IL_0010
  IL_000a:  ldloc.0
  IL_000b:  ldc.i4.1
  IL_000c:  beq.s      IL_0031
  IL_000e:  ldc.i4.0
  IL_000f:  ret
  IL_0010:  ldarg.0
  IL_0011:  ldc.i4.m1
  IL_0012:  stfld      ""int C.<M>d__0.<>1__state""
  IL_0017:  ldnull
  IL_0018:  stloc.1
  IL_0019:  ldloc.1
  IL_001a:  brtrue.s   IL_0021
  IL_001c:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
  IL_0021:  ldarg.0
  IL_0022:  ldloc.1
  IL_0023:  stfld      ""string C.<M>d__0.<>2__current""
  IL_0028:  ldarg.0
  IL_0029:  ldc.i4.1
  IL_002a:  stfld      ""int C.<M>d__0.<>1__state""
  IL_002f:  ldc.i4.1
  IL_0030:  ret
  IL_0031:  ldarg.0
  IL_0032:  ldc.i4.m1
  IL_0033:  stfld      ""int C.<M>d__0.<>1__state""
  IL_0038:  ldc.i4.0
  IL_0039:  ret
}");
        }

        [Fact]
        public void Async()
        {
            const string source = @"
using System;
using System.Threading.Tasks;

class C
{
    static async Task<string> M(string s)
    {
        return null!;
    }

    static async Task Main()
    {
        await M(null);
    }
}";
            var comp = CreateCompilation(source);
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.<M>d__0.System.Runtime.CompilerServices.IAsyncStateMachine.MoveNext()", @"
{
  // Code size       74 (0x4a)
  .maxstack  2
  .locals init (string V_0,
                System.Exception V_1)
  .try
  {
    IL_0000:  ldarg.0
    IL_0001:  ldfld      ""string C.<M>d__0.s""
    IL_0006:  brtrue.s   IL_0012
    IL_0008:  ldstr      ""s""
    IL_000d:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
    IL_0012:  ldnull
    IL_0013:  dup
    IL_0014:  brtrue.s   IL_001b
    IL_0016:  call       ""void System.Runtime.CompilerServices.ThrowHelper.NullReturn()""
    IL_001b:  stloc.0
    IL_001c:  leave.s    IL_0035
  }
  catch System.Exception
  {
    IL_001e:  stloc.1
    IL_001f:  ldarg.0
    IL_0020:  ldc.i4.s   -2
    IL_0022:  stfld      ""int C.<M>d__0.<>1__state""
    IL_0027:  ldarg.0
    IL_0028:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string> C.<M>d__0.<>t__builder""
    IL_002d:  ldloc.1
    IL_002e:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string>.SetException(System.Exception)""
    IL_0033:  leave.s    IL_0049
  }
  IL_0035:  ldarg.0
  IL_0036:  ldc.i4.s   -2
  IL_0038:  stfld      ""int C.<M>d__0.<>1__state""
  IL_003d:  ldarg.0
  IL_003e:  ldflda     ""System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string> C.<M>d__0.<>t__builder""
  IL_0043:  ldloc.0
  IL_0044:  call       ""void System.Runtime.CompilerServices.AsyncTaskMethodBuilder<string>.SetResult(string)""
  IL_0049:  ret
}");
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
        _ = M(null!);
    }

    static async IAsyncEnumerable<string> M(object param)
    {
        yield return null!;
    }
}";

            var comp = CreateCompilation(source, useAsyncStreams: true);
            var verifier = CompileAndVerifyException<ArgumentNullException>(comp);
            verifier.VerifyIL("C.M", @"
{
  // Code size       21 (0x15)
  .maxstack  1
  IL_0000:  ldarg.0
  IL_0001:  brtrue.s   IL_000d
  IL_0003:  ldstr      ""param""
  IL_0008:  call       ""void System.Runtime.CompilerServices.ThrowHelper.ArgumentNull(string)""
  IL_000d:  ldc.i4.s   -2
  IL_000f:  newobj     ""C.<M>d__1..ctor(int)""
  IL_0014:  ret
}");
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

        private CSharpCompilation CreateCompilation(string source, bool nullableContext = true, bool useAsyncStreams = false)
            => CreateCompilation(source, RuntimeChecksMode.Enable, nullableContext, useAsyncStreams);
    }
}
