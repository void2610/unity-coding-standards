using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Void2610.Unity.Analyzers.ExpressionBodyAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Void2610.Unity.Analyzers.Tests
{
    public class ExpressionBodyAnalyzerTests
    {
        [Fact]
        public async Task PublicMethodSingleStatement_VUA3001()
        {
            var test = @"
public class TestClass
{
    public int {|#0:GetValue|}()
    {
        return 42;
    }
}";
            var expected = Verify.Diagnostic("VUA3001")
                .WithLocation(0)
                .WithArguments("GetValue");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task PublicVoidMethodSingleStatement_VUA3001()
        {
            var test = @"
public class TestClass
{
    private int _value;
    public void {|#0:SetValue|}(int v)
    {
        _value = v;
    }
}";
            var expected = Verify.Diagnostic("VUA3001")
                .WithLocation(0)
                .WithArguments("SetValue");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task PublicMethodExpressionBody_NoDiagnostic()
        {
            // 既に式本体のため除外
            var test = @"
public class TestClass
{
    public int GetValue() => 42;
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PublicMethodMultiLineExpressionBody_VUA3001()
        {
            var test = @"
public class TestClass
{
    public int {|#0:GetValue|}() =>
        42;
}";
            var expected = Verify.Diagnostic("VUA3001")
                .WithLocation(0)
                .WithArguments("GetValue");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task PublicMethodMultiLineSignatureExpressionBody_NoDiagnostic()
        {
            // パラメータが複数行に分かれている場合は除外
            var test = @"
public class TestClass
{
    public int GetValue(
        int value) => value;
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ProtectedMethodMultiLineExpressionBody_VUA3001()
        {
            var test = @"
public class TestClass
{
    protected int {|#0:GetValue|}() =>
        42;
}";
            var expected = Verify.Diagnostic("VUA3001")
                .WithLocation(0)
                .WithArguments("GetValue");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task PublicMethodMultipleStatements_NoDiagnostic()
        {
            // 複数ステートメントのため除外
            var test = @"
public class TestClass
{
    public int GetValue()
    {
        var x = 42;
        return x;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PrivateMethodSingleStatement_NoDiagnostic()
        {
            // privateメソッドのため除外
            var test = @"
public class TestClass
{
    private int GetValue()
    {
        return 42;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ProtectedMethodSingleStatement_NoDiagnostic()
        {
            // protectedメソッドのため除外
            var test = @"
public class TestClass
{
    protected int GetValue()
    {
        return 42;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task Constructor_NoDiagnostic()
        {
            // コンストラクタはMethodDeclarationではないため対象外
            var test = @"
public class TestClass
{
    private int _value;
    public TestClass(int value)
    {
        _value = value;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PublicMethodSingleIfStatement_NoDiagnostic()
        {
            // if文は式に変換できないため除外
            var test = @"
public class TestClass
{
    private int _value;
    public void SetValue(int v)
    {
        if (v > 0) _value = v;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PublicMethodSingleForeachStatement_NoDiagnostic()
        {
            // foreach文は式に変換できないため除外
            var test = @"
using System.Collections.Generic;
public class TestClass
{
    private List<int> _items = new List<int>();
    public void Process(List<int> items)
    {
        foreach (var item in items) _items.Add(item);
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PublicMethodSingleReturnWithSwitchExpression_NoDiagnostic()
        {
            // switch式を含むreturn文は複雑になるため除外
            var test = @"
public class TestClass
{
    public string GetName(int x)
    {
        return x switch
        {
            1 => ""One"",
            2 => ""Two"",
            _ => ""Other""
        };
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PublicMethodWithAttributeSingleLineExpressionBody_NoDiagnostic()
        {
            // 属性付きメソッドで式本体が1行の場合は除外
            var test = @"
using System;
public class TestClass
{
    [Obsolete]
    public int GetValue() => 42;
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PublicMethodExpressionBodyWithSwitchExpression_NoDiagnostic()
        {
            // 式本体にswitch式を含む場合は複数行でも除外
            var test = @"
public class TestClass
{
    public string GetName(int x) => x switch
    {
        1 => ""One"",
        2 => ""Two"",
        _ => ""Other""
    };
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PublicMethodExpressionBodyWithInitializer_NoDiagnostic()
        {
            // 式本体にコレクション初期化子を含む場合は複数行でも除外
            var test = @"
using System.Collections.Generic;
public class TestClass
{
    public List<int> GetValues() => new List<int>
    {
        1,
        2,
        3
    };
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PublicDisposeMethod_NoDiagnostic()
        {
            // IDisposable.Disposeメソッドは除外
            var test = @"
public class TestClass
{
    private object _resource;
    public void Dispose()
    {
        _resource = null;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task PublicMethodWithDirective_NoDiagnostic()
        {
            var test = @"
#define UNITY_EDITOR

public class TestClass
{
    public void RegisterAll()
    {
#if UNITY_EDITOR
        DoWork();
#endif
    }

    private void DoWork() { }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }
    }
}
