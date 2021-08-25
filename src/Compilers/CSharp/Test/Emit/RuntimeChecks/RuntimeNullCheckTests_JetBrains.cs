// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.RuntimeChecks
{
    public partial class RuntimeNullCheckTests
    {
        private const string JetBrainsAnnotations = @"
using System;
namespace JetBrains.Annotations
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Parameter | AttributeTargets.Delegate | AttributeTargets.GenericParameter)]
    public sealed class NotNullAttribute : Attribute
    {
    }
}";

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_ReferenceTypeParameter()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class C
{
    static void Main()
    {
        M(null);
    }

    static void M([NotNull] string s)
    {
        Console.WriteLine(s.Length);
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_MultipleReferenceTypeParameters()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class C
{
    static void Main()
    {
        M(""meow"", null);
    }

    static void M([NotNull] string s1, [NotNull] string s2)
    {
        Console.WriteLine(s1.Length);
        Console.WriteLine(s2.Length);
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp, expectedOutput: "4");
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_InheritedFromBase()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class Base
{
    public virtual void Goo([NotNull] string s) { }
}
class Derived : Base
{
    static void Main()
    {
        var d = new Derived();
        d.Goo(null);
    }

    public override void Goo(string s)
    {
        Console.WriteLine(s.Length);
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_InheritedFromBase_DeeperHierarchy()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class B1
{
    public virtual void Goo([NotNull] string s) { }
}

class B2 : B1 {}
class B3 : B2 {}

class D : B3
{
    static void Main()
    {
        var d = new D();
        d.Goo(null);
    }

    public override void Goo(string s)
    {
        Console.WriteLine(s.Length);
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_InheritedFromInterface()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
interface I
{
    void Goo([NotNull] string s);
}
class C : I
{
    static void Main()
    {
        var c = new C();
        c.Goo(null);
    }

    public void Goo(string s)
    {
        Console.WriteLine(s.Length);
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }
        
        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_InheritedFromInterface_ImplementedInBaseType()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
interface I
{
    void Goo([NotNull] string s);
}

class B : I
{
    public void Goo(string s) { }
}

class C : B
{
    static void Main()
    {
        var c = new C();
        c.Goo(null);
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_InheritedFromInterface_DeeperHierarchy()
        {
            const string source = @"
using System;
using JetBrains.Annotations;

interface I
{
    void Goo([NotNull] string s);
}
interface I2 : I {}
interface I3 : I2 {}

class C : I3
{
    static void Main()
    {
        var c = new C();
        c.Goo(null);
    }

    public void Goo(string s)
    {
        Console.WriteLine(s.Length);
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }
        
        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_InheritedFromInterface_ExplicitImplementation()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
interface I
{
    void Goo([NotNull] string s);
}
class C : I
{
    static void Main()
    {
        var c = new C();
        ((I)c).Goo(null);
    }

    void I.Goo(string s)
    {
        Console.WriteLine(s.Length);
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_Generics_UnconstrainedType()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class C
{
    static void Main()
    {
        M<string>(null);
    }

    static void M<T>([NotNull] T t)
    {
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_Generics_StructConstraint_NoCheck()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class C
{
    static void Main()
    {
    }

    static void M<T>([NotNull] T t) where T : struct
    {
        Console.WriteLine(""ok"");
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            var verifier = CompileAndVerify(comp);
            verifier.VerifyIL("C.M<T>(T)", @"
{
  // Code size       11 (0xb)
  .maxstack  1
  IL_0000:  ldstr      ""ok""
  IL_0005:  call       ""void System.Console.WriteLine(string)""
  IL_000a:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_AutoProperty_WithSetter()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class C
{
    [NotNull] static string Prop { get; set; }

    static void Main()
    {
        Prop = null;
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_AutoProperty_WithSetter_InheritedFromBase()
        {
            const string source = @"
using System;
using JetBrains.Annotations;

var d = new D();
d.Prop = null;

class B
{
    [NotNull] public virtual string Prop { get; set; }
}

class D : B
{
    public override string Prop { get; set; }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_AutoProperty_WithSetter_InheritedFromInterface()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
interface I
{
    [NotNull] string Prop { get; set; }
}

class D : I
{
    public string Prop { get; set; }

    static void Main()
    {
        var d = new D();
        d.Prop = null;
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }
        
        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_GenericAutoProperty_WithSetter()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class Generic<T>
{
    [NotNull] public T Prop { get; set; }
}

class C
{
    static void Main()
    {
        var g = new Generic<string>();
        g.Prop = null;
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_GenericAutoProperty_StructConstraint_NoCheck()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class Generic<T> where T : struct
{
    [NotNull] public T Prop { get; set; }
}

class C
{
    static void Main()
    {
        var g = new Generic<int>();
        g.Prop = 42;
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            var verifier = CompileAndVerify(comp);
            verifier.VerifyIL("Generic<T>.Prop.set", @"
{
  // Code size        8 (0x8)
  .maxstack  2
  IL_0000:  ldarg.0
  IL_0001:  ldarg.1
  IL_0002:  stfld      ""T Generic<T>.<Prop>k__BackingField""
  IL_0007:  ret
}");
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_IndexerParameter()
        {
            const string source = @"
using System;
using JetBrains.Annotations;
class C
{
    public int this[[NotNull] string s] => s.Length;

    static void Main()
    {
        var c = new C();
        _ = c[null];
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }

        [Fact]
        [Trait("Annotations", "JetBrains")]
        public void NotNull_IndexerParameter_InheritedFromBase()
        {
            const string source = @"
using System;
using JetBrains.Annotations;

abstract class B
{
    public abstract int this[[NotNull] string s] { get; }
}

class D : B
{
    public override int this[string s] => s.Length;

    static void Main()
    {
        var d = new D();
        _ = d[null];
    }
}";

            var comp = CreateCompilation(source, nullableContext: false);
            CompileAndVerifyException<ArgumentNullException>(comp);
        }
    }
}
