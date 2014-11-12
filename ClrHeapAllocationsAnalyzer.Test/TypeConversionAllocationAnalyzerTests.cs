﻿using ClrHeapAllocationAnalyzer;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Immutable;

namespace ClrHeapAllocationsAnalyzer.Test
{
    [TestClass]
    public class TypeConversionAllocationAnalyzerTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void TypeConversionAllocation_ArgumentSyntax()
        {
            var sampleProgram =
@"using System;

var result = fooObjCall(10); // Allocation
var temp = new MyObject(10); // Allocation

private string fooObjCall(object obj)
{
    return obj.ToString();
}

public class MyObject
{
    private Object Obj;

    public MyObject(object obj)
    {
        this.Obj = obj;
    }
}";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.Argument));

            Assert.AreEqual(2, info.Allocations.Count);
            // Diagnostic: (3,25): warning HeapAnalyzerBoxingRule: Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable ***
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.ValueTypeToReferenceTypeConversionRule.Id, line: 3, character: 25);
            // Diagnostic: (4,25): warning HeapAnalyzerBoxingRule: Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable ***
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.ValueTypeToReferenceTypeConversionRule.Id, line: 4, character: 25);
        }

        [TestMethod]
        public void TypeConversionAllocation_ArgumentSyntax_WithDelegates()
        {
            var sampleProgram =
@"using System;

public class MyClass
{
    public void Testing()
    {
        var @class = new MyClass();
        @class.ProcessFunc(fooObjCall); // implicit, so Allocation
        @class.ProcessFunc(new Func<object, string>(fooObjCall)); // Explicit, so NO Allocation
    }

    public void ProcessFunc(Func<object, string> func)
    {
    }

    private string fooObjCall(object obj)
    {
        return obj.ToString();
    }
}

public struct MyStruct
{
    public void Testing()
    {
        var @struct = new MyStruct();
        @struct.ProcessFunc(fooObjCall); // implicit, so Allocation
        @struct.ProcessFunc(new Func<object, string>(fooObjCall)); // Explicit, so NO Allocation
    }

    public void ProcessFunc(Func<object, string> func)
    {
    }

    private string fooObjCall(object obj)
    {
        return obj.ToString();
    }
}";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.Argument));

            Assert.AreEqual(3, info.Allocations.Count);
            // Diagnostic: (8,28): warning HeapAnalyzerMethodGroupAllocationRule: This will allocate a delegate instance
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.MethodGroupAllocationRule.Id, line: 8, character: 28);
            // Diagnostic: (27,29): warning HeapAnalyzerMethodGroupAllocationRule: This will allocate a delegate instance
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.MethodGroupAllocationRule.Id, line: 27, character: 29);
            // Diagnostic: (27,29): warning HeapAnalyzerDelegateOnStructRule: Struct instance method being used for delegate creation, this will result in a boxing instruction
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.DelegateOnStructInstanceRule.Id, line: 27, character: 29);
        }

        [TestMethod]
        public void TypeConversionAllocation_ReturnStatementSyntax()
        {
            var sampleProgram =
@"using System;

var result1 = new MyObject().Obj; // Allocation
var result2 = new MyObject().ObjNoAllocation; // Allocation

public class MyObject
{
    public Object Obj { get { return 0; } }

    public Object ObjNoAllocation { get { return 0.ToString(); } }
}";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.ReturnStatement));

            Assert.AreEqual(1, info.Allocations.Count);

            // Diagnostic: (7,38): warning HeapAnalyzerBoxingRule: Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.ValueTypeToReferenceTypeConversionRule.Id, line: 8, character: 38);
        }

        [TestMethod]
        public void TypeConversionAllocation_YieldStatementSyntax()
        {
            var sampleProgram =
@"using System;
using System.Collections.Generic;

foreach (var item in GetItems())
{
}

foreach (var item in GetItemsNoAllocation())
{
}

public IEnumerable<object> GetItems()
{
    yield return 0; // Allocation
    yield break;
}

public IEnumerable<int> GetItemsNoAllocation()
{
    yield return 0; // NO Allocation (IEnumerable<int>)
    yield break;
}";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.YieldReturnStatement));

            Assert.AreEqual(1, info.Allocations.Count);

            // Diagnostic: (14,18): warning HeapAnalyzerBoxingRule: Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.ValueTypeToReferenceTypeConversionRule.Id, line: 14, character: 18);
        }

        [TestMethod]
        public void TypeConversionAllocation_BinaryExpressionSyntax()
        {
            var sampleProgram =
@"using System;

object x = ""blah"";
object a1 = x ?? 0; // Allocation
object a2 = x ?? 0.ToString(); // No Allocation

var b1 = 10 as object; // Allocation
var b2 = 10.ToString() as object; // No Allocation
";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.CoalesceExpression, SyntaxKind.AsExpression));

            Assert.AreEqual(2, info.Allocations.Count);

            // Diagnostic: (4,17): warning HeapAnalyzerBoxingRule: Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.ValueTypeToReferenceTypeConversionRule.Id, line: 4, character: 18);
            // Diagnostic: (7,9): warning HeapAnalyzerBoxingRule: Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.ValueTypeToReferenceTypeConversionRule.Id, line: 7, character: 10);
        }

        [TestMethod]
        public void TypeConversionAllocation_BinaryExpressionSyntax_WithDelegates()
        {
            var sampleProgram =
@"using System;

public class MyClass
{
    public void Testing()
    {
        Func<object, string> temp = null;
        var result1 = temp ?? fooObjCall; // implicit, so Allocation
        var result2 = temp ?? new Func<object, string>(fooObjCall); // Explicit, so NO Allocation
    }

    private string fooObjCall(object obj)
    {
        return obj.ToString();
    }
}

public struct MyStruct
{
    public void Testing()
    {
        Func<object, string> temp = null;
        var result1 = temp ?? fooObjCall; // implicit, so Allocation
        var result2 = temp ?? new Func<object, string>(fooObjCall); // Explicit, so NO Allocation
    }

    private string fooObjCall(object obj)
    {
        return obj.ToString();
    }
}";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.CoalesceExpression, SyntaxKind.AsExpression));

            Assert.AreEqual(3, info.Allocations.Count);

            // Diagnostic: (8,31): warning HeapAnalyzerMethodGroupAllocationRule: This will allocate a delegate instance
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.MethodGroupAllocationRule.Id, line: 8, character: 31);
            // Diagnostic: (23,31): warning HeapAnalyzerMethodGroupAllocationRule: This will allocate a delegate instance
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.MethodGroupAllocationRule.Id, line: 23, character: 31);
            // Diagnostic: (23,31): warning HeapAnalyzerDelegateOnStructRule: Struct instance method being used for delegate creation, this will result in a boxing instruction
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.DelegateOnStructInstanceRule.Id, line: 23, character: 31);
        }

        [TestMethod]
        public void TypeConversionAllocation_ForEachStatementSyntax()
        {
            var sampleProgram =
@"using System;
using System.Collections.Generic;
using System.Linq;

foreach (var a in new[] { 1, 2, 3 }) // Allocation (via Array code-path)
{
}

foreach (var a in (new[] { 1, 2, 3 }).ToList()) // Allocation (via List<T> code-path)
{
}

foreach (var a in new[] { ""1"", ""2"", ""3"" }) // No Allocation
{
}";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.ForEachStatement));

            Assert.AreEqual(2, info.Allocations.Count);

            // Diagnostic: (5,19): warning HeapAnalyzerBoxingRule: Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.ValueTypeToReferenceTypeConversionRule.Id, line: 5, character: 19);

            // Diagnostic: (9,19): warning HeapAnalyzerBoxingRule: Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.ValueTypeToReferenceTypeConversionRule.Id, line: 9, character: 19);
        }

        [TestMethod]
        public void TypeConversionAllocation_EqualsValueClauseSyntax()
        {
            // for (object i = 0;;)
            var sampleProgram =
@"using System;

for (object i = 0;;) // Allocation
{
}

for (int i = 0;;) // NO Allocation
{
}";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, analyser.SyntaxKindsOfInterest);

            Assert.AreEqual(1, info.Allocations.Count);

            // Diagnostic: (3,17): warning HeapAnalyzerBoxingRule: Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.ValueTypeToReferenceTypeConversionRule.Id, line: 3, character: 17);
        }

        [TestMethod]
        public void TypeConversionAllocation_EqualsValueClauseSyntax_WithDelegates()
        {
            var sampleProgram =
@"using System;

public class MyClass
{
    public void Testing()
    {
        Func<object, string> func2 = fooObjCall; // implicit, so Allocation
        Func<object, string> func1 = new Func<object, string>(fooObjCall); // Explicit, so NO Allocation
    }

    private string fooObjCall(object obj)
    {
        return obj.ToString();
    }
}

public struct MyStruct
{
    public void Testing()
    {
        Func<object, string> func2 = fooObjCall; // implicit, so Allocation
        Func<object, string> func1 = new Func<object, string>(fooObjCall); // Explicit, so NO Allocation
    }

    private string fooObjCall(object obj)
    {
        return obj.ToString();
    }
}";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.CoalesceExpression, SyntaxKind.EqualsValueClause));

            Assert.AreEqual(3, info.Allocations.Count);

            // Diagnostic: (7,38): warning HeapAnalyzerMethodGroupAllocationRule: This will allocate a delegate instance
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.MethodGroupAllocationRule.Id, line: 7, character: 38);
            // Diagnostic: (21,38): warning HeapAnalyzerMethodGroupAllocationRule: This will allocate a delegate instance
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.MethodGroupAllocationRule.Id, line: 21, character: 38);
            // Diagnostic: (21,38): warning HeapAnalyzerDelegateOnStructRule: Struct instance method being used for delegate creation, this will result in a boxing instruction
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.DelegateOnStructInstanceRule.Id, line: 21, character: 38);
        }

        [TestMethod]
        public void TypeConversionAllocation_EqualsValueClause_ExplicitMethodGroupAllocation_Bug()
        {
            // See https://github.com/mjsabby/RoslynClrHeapAllocationAnalyzer/issues/2
            var sampleProgram =
@"using System;

public class MyClass
{
    public void Testing()
    {
        Action methodGroup = this.Method;
    }

    private void Method()
    {
    }
}

public struct MyStruct
{
    public void Testing()
    {
        Action methodGroup = this.Method;
    }

    private void Method()
    {
    }
}";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.EqualsValueClause));

            Assert.AreEqual(3, info.Allocations.Count);
        }

        [TestMethod]
        public void TypeConversionAllocation_ConditionalExpressionSyntax()
        {
            var sampleProgram =
@"using System;

object obj = ""test"";
object test1 = true ? 0 : obj; // Allocation
object test2 = true ? 0.ToString() : obj; // NO Allocation
";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.ConditionalExpression));

            Assert.AreEqual(1, info.Allocations.Count);

            // Diagnostic: (4,23): warning HeapAnalyzerBoxingRule: Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.ValueTypeToReferenceTypeConversionRule.Id, line: 4, character: 23);
        }

        [TestMethod]
        public void TypeConversionAllocation_CastExpressionSyntax()
        {
            var sampleProgram =
@"using System;

var f1 = (object)5; // Allocation
var f2 = (object)""5""; // NO Allocation
";

            var analyser = new TypeConversionAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.CastExpression));

            Assert.AreEqual(1, info.Allocations.Count);

            // Diagnostic: (3,18): warning HeapAnalyzerBoxingRule: Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable
            AssertEx.ContainsDiagnostic(info.Allocations, id: TypeConversionAllocationAnalyzer.ValueTypeToReferenceTypeConversionRule.Id, line: 3, character: 18);
        }
    }
}