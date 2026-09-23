namespace iSpectrum.Z80.Tests.Fuse;

/// <summary>Checks the parser on its own, independently of the CPU.</summary>
public class FuseTestFileTests
{
    private static readonly string Directory = Path.Combine(AppContext.BaseDirectory, "Fuse");

    [Fact]
    public void BothFiles_ListTheSameTestsInTheSameOrder()
    {
        var inputs = FuseTestFile.ParseInput(Path.Combine(Directory, "tests.in"));
        var expected = FuseTestFile.ParseExpected(Path.Combine(Directory, "tests.expected"));

        Assert.NotEmpty(inputs);
        Assert.Equal(inputs.Select(t => t.Name), expected.Select(t => t.Name));
        Assert.Equal(inputs.Count, inputs.Select(t => t.Name).Distinct().Count());
    }

    [Fact]
    public void Test01_IsParsed()
    {
        var input = FuseTestFile.ParseInput(Path.Combine(Directory, "tests.in")).Single(t => t.Name == "01");
        var expected = FuseTestFile.ParseExpected(Path.Combine(Directory, "tests.expected")).Single(t => t.Name == "01");

        Assert.Equal(1, input.State.TStates);
        var block = Assert.Single(input.Memory);
        Assert.Equal(0x0000, block.Address);
        Assert.Equal(new byte[] { 0x01, 0x12, 0x34 }, block.Bytes);

        Assert.Equal(6, expected.Events.Count);
        Assert.Equal(new FuseEvent(10, "MR", 0x0002, 0x34), expected.Events[5]);
        Assert.Equal(0x3412, expected.State.BC);
        Assert.Equal(0x0003, expected.State.PC);
        Assert.Equal(10, expected.State.TStates);
        Assert.Empty(expected.ChangedMemory);
    }

    [Fact]
    public void Test02_ReportsChangedMemory()
    {
        var expected = FuseTestFile.ParseExpected(Path.Combine(Directory, "tests.expected")).Single(t => t.Name == "02");

        Assert.Equal(0x5602, expected.State.MEMPTR);
        var block = Assert.Single(expected.ChangedMemory);
        Assert.Equal(0x0001, block.Address);
        Assert.Equal(new byte[] { 0x56 }, block.Bytes);
    }
}
