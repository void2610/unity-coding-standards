using System.Threading.Tasks;
using Xunit;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Void2610.Unity.Analyzers.OneMemberPerLineAnalyzer,
    Void2610.Unity.Analyzers.OneMemberPerLineCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Void2610.Unity.Analyzers.Tests
{
    public class OneMemberPerLineTests
    {
        [Fact]
        public async Task 別の行のメンバー_警告なし()
        {
            var test = @"
public class TestClass
{
    private int _a;
    private int _b;

    public int Value { get; private set; }
    public void Method() { }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task 同じ行に連結されたフィールド_次の行へ移す()
        {
            var test = @"
using System;

public class TestClass
{
    [Obsolete] private int _a; {|#0:[|}Obsolete] private readonly int _b = 1;
}";
            var fixedCode = @"
using System;

public class TestClass
{
    [Obsolete] private int _a;
    [Obsolete] private readonly int _b = 1;
}";
            var expected = Verify.Diagnostic("VUA3005").WithLocation(0).WithArguments("_b");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task コメント行の後ろに連結されたプロパティ_次の行へ移す()
        {
            var test = @"
public class TestClass
{
    // 説明
    private int _a; {|#0:public|} int Slots => _a;
}";
            var fixedCode = @"
public class TestClass
{
    // 説明
    private int _a;
    public int Slots => _a;
}";
            var expected = Verify.Diagnostic("VUA3005").WithLocation(0).WithArguments("Slots");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task 三つ連結_すべて分割()
        {
            var test = @"
public class TestClass
{
    private int _a; {|#0:private|} int _b; {|#1:private|} int _c;
}";
            var fixedCode = @"
public class TestClass
{
    private int _a;
    private int _b;
    private int _c;
}";
            var expected = new[]
            {
                Verify.Diagnostic("VUA3005").WithLocation(0).WithArguments("_b"),
                Verify.Diagnostic("VUA3005").WithLocation(1).WithArguments("_c"),
            };
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }
    }
}
