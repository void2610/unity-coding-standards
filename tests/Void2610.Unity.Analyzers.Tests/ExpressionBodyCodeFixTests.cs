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
                .WithArguments("GetValue", "メソッド 'GetValue' は1行の式本体 (=>) で記述してください");
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
                .WithArguments("SetValue", "メソッド 'SetValue' は1行の式本体 (=>) で記述してください");
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
                .WithArguments("Execute", "メソッド 'Execute' は1行の式本体 (=>) で記述してください");
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
                .WithArguments("GetValue", "メソッド 'GetValue' は1行の式本体 (=>) で記述してください");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task MultiLineMethodChainExpressionBody_NotJoined()
        {
            // 呼び出しごとに改行したメソッドチェーンは警告も変換もしない
            var test = @"
public class Builder
{
    public Builder Add(int value) => this;
}
public class TestClass
{
    public Builder Create() =>
        new Builder()
            .Add(1)
            .Add(2);
}";
            await Verify.VerifyCodeFixAsync(test, test);
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

        [Fact]
        public async Task PrivateExpressionBody_ConvertedToBlockBody()
        {
            var test = @"
public class TestClass
{
    private int {|#0:GetValue|}() => 42;
}";
            var fixedCode = @"
public class TestClass
{
    private int GetValue()
    {
        return 42;
    }
}";
            var expected = Verify.Diagnostic("VUA3001")
                .WithLocation(0)
                .WithArguments("GetValue", "メソッド 'GetValue' では式本体 (=>) を使用しないでください");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task DisposeExpressionBody_ConvertedToBlockBody()
        {
            var test = @"
public class TestClass
{
    public void {|#0:Dispose|}() => Cleanup();

    private void Cleanup() { }
}";
            var fixedCode = @"
public class TestClass
{
    public void Dispose()
    {
        Cleanup();
    }

    private void Cleanup() { }
}";
            var expected = Verify.Diagnostic("VUA3001")
                .WithLocation(0)
                .WithArguments("Dispose", "メソッド 'Dispose' では式本体 (=>) を使用しないでください");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }
    }
}
