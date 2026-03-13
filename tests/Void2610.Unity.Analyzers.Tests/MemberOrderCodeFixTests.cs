using System.Threading.Tasks;
using Xunit;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Void2610.Unity.Analyzers.MemberOrderAnalyzer,
    Void2610.Unity.Analyzers.MemberOrderCodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Void2610.Unity.Analyzers.Tests
{
    public class MemberOrderCodeFixTests
    {
        [Fact]
        public async Task PublicPropertyAfterPrivateField_Reordered()
        {
            // publicプロパティがprivateフィールドより後 → 順序修正
            var test = @"
public class TestClass
{
    private int _count;

    public int {|#0:Value|} { get; set; }
}";
            var fixedCode = @"
public class TestClass
{

    public int Value { get; set; }
    private int _count;
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("Value", "public properties", "private fields");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task ConstructorAfterPublicMethod_Reordered()
        {
            // コンストラクタがpublicメソッドの後 → 順序修正
            var test = @"
public class TestClass
{
    private int _count;

    public int GetValue() => _count;

    public {|#0:TestClass|}(int count)
    {
        _count = count;
    }
}";
            var fixedCode = @"
public class TestClass
{
    private int _count;

    public TestClass(int count)
    {
        _count = count;
    }

    public int GetValue() => _count;
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("TestClass", "constructors", "public methods (one line)");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task UnityEventAfterCleanup_Reordered()
        {
            // Unity eventがcleanupの後 → 順序修正
            var test = @"
public class TestClass
{
    private int _count;

    private void OnDestroy()
    {
        _count = 0;
    }

    private void {|#0:Awake|}()
    {
        _count = 1;
    }
}";
            var fixedCode = @"
public class TestClass
{
    private int _count;

    private void Awake()
    {
        _count = 1;
    }

    private void OnDestroy()
    {
        _count = 0;
    }
}";
            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("Awake", "Unity events", "cleanup");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task ProtectedMethodAfterPrivateMethod_Reordered()
        {
            var test = @"
public class TestClass
{
    private int _count;

    private void Helper()
    {
        _count = 0;
    }

    protected virtual void {|#0:Hook|}()
    {
        _count = 1;
    }
}";

            var fixedCode = @"
public class TestClass
{
    private int _count;

    protected virtual void Hook()
    {
        _count = 1;
    }

    private void Helper()
    {
        _count = 0;
    }
}";

            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("Hook", "protected methods (multi line)", "private methods (multi line)");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task ProtectedOneLineAfterProtectedMultiLine_Reordered()
        {
            var test = @"
public class TestClass
{
    private int _count;

    protected virtual void Hook()
    {
        _count = 1;
    }

    protected virtual int {|#0:GetValue|}() => _count;
}";

            var fixedCode = @"
public class TestClass
{
    private int _count;

    protected virtual int GetValue() => _count;

    protected virtual void Hook()
    {
        _count = 1;
    }
}";

            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("GetValue", "protected methods (one line)", "protected methods (multi line)");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task ProtectedOneLineAfterPrivateOneLine_Reordered()
        {
            var test = @"
public class TestClass
{
    private int _count;

    private int GetPrivate() => _count;

    protected int {|#0:GetProtected|}() => _count;
}";

            var fixedCode = @"
public class TestClass
{
    private int _count;

    protected int GetProtected() => _count;

    private int GetPrivate() => _count;
}";

            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("GetProtected", "protected methods (one line)", "private methods (one line)");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task PrivateMethodWithIfDirective_ReorderedKeepingDirectiveBlock()
        {
            var test = @"
#define UNITY_EDITOR

public class TestClass
{
    public int Value() => 1;

#if UNITY_EDITOR
    private void DebugOnly()
    {
    }
#endif

    protected int {|#0:GetProtected|}() => 1;
}";

            var fixedCode = @"
#define UNITY_EDITOR

public class TestClass
{
    public int Value() => 1;
    protected int GetProtected() => 1;

#if UNITY_EDITOR
    private void DebugOnly()
    {
    }
#endif

}";

            var expected = Verify.Diagnostic("VUA3002")
                .WithLocation(0)
                .WithArguments("GetProtected", "protected methods (one line)", "private methods (multi line)");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task ConstAndPrivateFieldSpacing_Normalized()
        {
            var test = @"
public class TestClass
{
    private const int MaxValue = 1;
    private int {|#0:_count|};
}";

            var fixedCode = @"
public class TestClass
{
    private const int MaxValue = 1;

    private int _count;
}";

            var expected = Verify.Diagnostic("VUA3003")
                .WithLocation(0)
                .WithArguments("_count");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task MultipleBlankLinesSpacing_Normalized()
        {
            var test = @"
public class TestClass
{
    private int _a;


    private int {|#0:_b|};
}";

            var fixedCode = @"
public class TestClass
{
    private int _a;

    private int _b;
}";

            var expected = Verify.Diagnostic("VUA3003")
                .WithLocation(0)
                .WithArguments("_b");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

        [Fact]
        public async Task MultipleBlankLinesBeforeXmlDoc_XmlDocPreserved()
        {
            var test = @"
public class TestClass
{
    private int _a;


    /// <summary>doc</summary>
    private int {|#0:_b|};
}";

            var fixedCode = @"
public class TestClass
{
    private int _a;

    /// <summary>doc</summary>
    private int _b;
}";

            var expected = Verify.Diagnostic("VUA3003")
                .WithLocation(0)
                .WithArguments("_b");
            await Verify.VerifyCodeFixAsync(test, expected, fixedCode);
        }

    }
}
