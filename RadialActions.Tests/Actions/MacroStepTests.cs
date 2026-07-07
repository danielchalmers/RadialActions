namespace RadialActions.Tests;

public class MacroStepTests
{
    [Fact]
    public void GetValidationError_NullStep_ReturnsError()
    {
        Assert.NotNull(MacroStep.GetValidationError(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DefinitelyNotAHotkey")]
    public void GetValidationError_InvalidShortcut_ReturnsError(string value)
    {
        var step = new MacroStep { Type = MacroStepType.Shortcut, Value = value };

        Assert.NotNull(MacroStep.GetValidationError(step));
    }

    [Theory]
    [InlineData("Ctrl+C")]
    [InlineData("Alt+Shift+F4")]
    [InlineData("PlayPause")]
    [InlineData("VolumeUp")]
    public void GetValidationError_ValidShortcut_ReturnsNull(string value)
    {
        var step = new MacroStep { Type = MacroStepType.Shortcut, Value = value };

        Assert.Null(MacroStep.GetValidationError(step));
    }

    [Fact]
    public void GetValidationError_EmptyText_ReturnsError()
    {
        var step = new MacroStep { Type = MacroStepType.Text, Value = "" };

        Assert.NotNull(MacroStep.GetValidationError(step));
    }

    [Fact]
    public void GetValidationError_NonEmptyText_ReturnsNull()
    {
        var step = new MacroStep { Type = MacroStepType.Text, Value = "hello world" };

        Assert.Null(MacroStep.GetValidationError(step));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(MacroStep.MaxDelayMilliseconds + 1)]
    public void GetValidationError_OutOfRangeDelay_ReturnsError(int delay)
    {
        var step = new MacroStep { Type = MacroStepType.Delay, DelayMilliseconds = delay };

        Assert.NotNull(MacroStep.GetValidationError(step));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500)]
    [InlineData(MacroStep.MaxDelayMilliseconds)]
    public void GetValidationError_ValidDelay_ReturnsNull(int delay)
    {
        var step = new MacroStep { Type = MacroStepType.Delay, DelayMilliseconds = delay };

        Assert.Null(MacroStep.GetValidationError(step));
    }

    [Fact]
    public void GetValidationError_UndefinedType_ReturnsError()
    {
        var step = new MacroStep { Type = (MacroStepType)999 };

        Assert.NotNull(MacroStep.GetValidationError(step));
    }

    [Fact]
    public void NormalizeAfterLoad_RepairsInvalidValues()
    {
        var step = new MacroStep
        {
            Type = (MacroStepType)999,
            Value = null,
            DelayMilliseconds = -5,
        };

        step.NormalizeAfterLoad();

        Assert.Equal(MacroStepType.Shortcut, step.Type);
        Assert.Equal(string.Empty, step.Value);
        Assert.Equal(MacroStep.DefaultDelayMilliseconds, step.DelayMilliseconds);
    }

    [Fact]
    public void NormalizeAfterLoad_KeepsValidValues()
    {
        var step = new MacroStep
        {
            Type = MacroStepType.Delay,
            Value = "unused",
            DelayMilliseconds = 250,
        };

        step.NormalizeAfterLoad();

        Assert.Equal(MacroStepType.Delay, step.Type);
        Assert.Equal("unused", step.Value);
        Assert.Equal(250, step.DelayMilliseconds);
    }

    [Fact]
    public void Clone_CopiesAllFields()
    {
        var step = new MacroStep
        {
            Type = MacroStepType.Text,
            Value = "hello",
            DelayMilliseconds = 42,
        };

        var clone = step.Clone();

        Assert.NotSame(step, clone);
        Assert.Equal(step.Type, clone.Type);
        Assert.Equal(step.Value, clone.Value);
        Assert.Equal(step.DelayMilliseconds, clone.DelayMilliseconds);
    }

    [Fact]
    public void ValidationError_UpdatesWhenPropertiesChange()
    {
        var step = new MacroStep { Type = MacroStepType.Shortcut, Value = "" };
        var notified = false;
        step.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MacroStep.ValidationError))
                notified = true;
        };

        Assert.NotNull(step.ValidationError);

        step.Value = "Ctrl+C";

        Assert.True(notified);
        Assert.Null(step.ValidationError);
    }
}
