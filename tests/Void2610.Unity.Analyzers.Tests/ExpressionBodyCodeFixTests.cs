using System.Threading.Tasks;
using Xunit;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Void2610.Unity.Analyzers.ExpressionBodyAnalyzer,
    Void2610.Unity.Analyzers.ExpressionBodyCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Void2610.Unity.Analyzers.Tests
{
    public class ExpressionBodyCodeFixTests
    {
        [Fact]
        public async Task ReturnStatement_ConvertedToExpressionBody()
        {
            // return文 → 式本体に変換
            var test = @"
public class TestClass
{
    public int {|#0:GetValue|}()
    {
        return 42;
    }
}";
            var fixedCode = @"
public class TestClass
{
    public int GetValue() => 42;
}";
            var expected = Verify.Diagnostic("VUA3001")
                .WithLocation(0)
                .WithArguments("GetValue");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task ExpressionStatement_ConvertedToExpressionBody()
        {
            // 式文 → 式本体に変換
            var test = @"
public class TestClass
{
    private int _value;
    public void {|#0:SetValue|}(int v)
    {
        _value = v;
    }
}";
            var fixedCode = @"
public class TestClass
{
    private int _value;
    public void SetValue(int v) => _value = v;
}";
            var expected = Verify.Diagnostic("VUA3001")
                .WithLocation(0)
                .WithArguments("SetValue");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task MethodCallStatement_ConvertedToExpressionBody()
        {
            // メソッド呼び出し文 → 式本体に変換
            var test = @"
public class TestClass
{
    private void DoWork() { }
    public void {|#0:Execute|}()
    {
        DoWork();
    }
}";
            var fixedCode = @"
public class TestClass
{
    private void DoWork() { }
    public void Execute() => DoWork();
}";
            var expected = Verify.Diagnostic("VUA3001")
                .WithLocation(0)
                .WithArguments("Execute");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task MultiLineExpressionBody_ConvertedToSingleLine()
        {
            var test = @"
public class TestClass
{
    public int {|#0:GetValue|}() =>
        42;
}";
            var fixedCode = @"
public class TestClass
{
    public int GetValue() => 42;
}";
            var expected = Verify.Diagnostic("VUA3001")
                .WithLocation(0)
                .WithArguments("GetValue");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task MultiLineSignatureExpressionBody_NoDiagnostic()
        {
            // パラメータが複数行に分かれている場合は除外（警告なし・変換なし）
            var test = @"
public class TestClass
{
    public int GetValue(
        int value) => value;
}";
            await Verify.VerifyCodeFixAsync(test, test);
        }
    }
}
