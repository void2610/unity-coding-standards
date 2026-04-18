using System.Threading.Tasks;
using Xunit;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Void2610.Unity.Analyzers.GuardClauseAnalyzer,
    Void2610.Unity.Analyzers.GuardClauseCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Void2610.Unity.Analyzers.Tests
{
    public class GuardClauseCodeFixTests
    {
        [Fact]
        public async Task ブロック付きreturnガード節_1行に変換()
        {
            var test = @"
public class TestClass
{
    public void Method(object x)
    {
        {|#0:if|} (x == null)
        {
            return;
        }
    }
}";
            var fixedCode = @"
public class TestClass
{
    public void Method(object x)
    {
        if (x == null) return;
    }
}";
            var expected = Verify.Diagnostic("VUA3004")
                .WithLocation(0)
                .WithArguments("x == null");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task ブロックなしreturnガード節_1行に変換()
        {
            var test = @"
public class TestClass
{
    public void Method(object x)
    {
        {|#0:if|} (x == null)
            return;
    }
}";
            var fixedCode = @"
public class TestClass
{
    public void Method(object x)
    {
        if (x == null) return;
    }
}";
            var expected = Verify.Diagnostic("VUA3004")
                .WithLocation(0)
                .WithArguments("x == null");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task 戻り値付きreturnガード節_1行に変換()
        {
            var test = @"
public class TestClass
{
    public int Method(int x)
    {
        {|#0:if|} (x <= 0)
        {
            return 0;
        }
        return x * 2;
    }
}";
            var fixedCode = @"
public class TestClass
{
    public int Method(int x)
    {
        if (x <= 0) return 0;
        return x * 2;
    }
}";
            var expected = Verify.Diagnostic("VUA3004")
                .WithLocation(0)
                .WithArguments("x <= 0");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task throwガード節_1行に変換()
        {
            var test = @"
using System;
public class TestClass
{
    public void Method(object x)
    {
        {|#0:if|} (x == null)
        {
            throw new ArgumentNullException();
        }
    }
}";
            var fixedCode = @"
using System;
public class TestClass
{
    public void Method(object x)
    {
        if (x == null) throw new ArgumentNullException();
    }
}";
            var expected = Verify.Diagnostic("VUA3004")
                .WithLocation(0)
                .WithArguments("x == null");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }
    }
}
