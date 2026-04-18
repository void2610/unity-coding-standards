using System.Threading.Tasks;
using Xunit;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Void2610.Unity.Analyzers.GuardClauseAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Void2610.Unity.Analyzers.Tests
{
    public class GuardClauseAnalyzerTests
    {
        // ---- 警告が出るケース ----

        [Fact]
        public async Task ブロック付きreturnガード節_VUA3004()
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
            var expected = Verify.Diagnostic("VUA3004")
                .WithLocation(0)
                .WithArguments("x == null");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task ブロックなしreturnガード節_VUA3004()
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
            var expected = Verify.Diagnostic("VUA3004")
                .WithLocation(0)
                .WithArguments("x == null");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task throwガード節_VUA3004()
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
            var expected = Verify.Diagnostic("VUA3004")
                .WithLocation(0)
                .WithArguments("x == null");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task 戻り値付きreturnガード節_VUA3004()
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
            var expected = Verify.Diagnostic("VUA3004")
                .WithLocation(0)
                .WithArguments("x <= 0");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task continueガード節_VUA3004()
        {
            var test = @"
public class TestClass
{
    public void Method(int[] items)
    {
        foreach (var item in items)
        {
            {|#0:if|} (item <= 0)
            {
                continue;
            }
        }
    }
}";
            var expected = Verify.Diagnostic("VUA3004")
                .WithLocation(0)
                .WithArguments("item <= 0");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task breakガード節_VUA3004()
        {
            var test = @"
public class TestClass
{
    public void Method(int[] items)
    {
        foreach (var item in items)
        {
            {|#0:if|} (item < 0)
            {
                break;
            }
        }
    }
}";
            var expected = Verify.Diagnostic("VUA3004")
                .WithLocation(0)
                .WithArguments("item < 0");
            await Verify.VerifyAnalyzerAsync(test, expected);
        }

        // ---- 警告が出ないケース ----

        [Fact]
        public async Task 単一行ガード節_NoDiagnostic()
        {
            var test = @"
public class TestClass
{
    public void Method(object x)
    {
        if (x == null) return;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task 単一行戻り値付きガード節_NoDiagnostic()
        {
            var test = @"
public class TestClass
{
    public int Method(int x)
    {
        if (x <= 0) return 0;
        return x * 2;
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task elseあり_NoDiagnostic()
        {
            var test = @"
public class TestClass
{
    public int Method(int x)
    {
        if (x > 0)
        {
            return x;
        }
        else
        {
            return -x;
        }
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task 複数文のif_NoDiagnostic()
        {
            var test = @"
public class TestClass
{
    private int _count;
    public void Method(int x)
    {
        if (x <= 0)
        {
            _count++;
            return;
        }
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task 通常のif文_NoDiagnostic()
        {
            var test = @"
public class TestClass
{
    private int _value;
    public void Method(int x)
    {
        if (x > 0)
        {
            _value = x;
        }
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task elseIfチェーン_NoDiagnostic()
        {
            var test = @"
public class TestClass
{
    public int Method(int x)
    {
        if (x > 0)
        {
            return 1;
        }
        else if (x < 0)
        {
            return -1;
        }
        else
        {
            return 0;
        }
    }
}";
            await Verify.VerifyAnalyzerAsync(test);
        }
    }
}
