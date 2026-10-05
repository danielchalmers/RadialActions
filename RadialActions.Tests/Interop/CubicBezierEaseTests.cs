namespace RadialActions.Tests;

public class CubicBezierEaseTests
{
    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0.55, 0.55, 0, 1)]
    [InlineData(0.32, 0, 0.67, 0)]
    [InlineData(0, 0, 1, 1)]
    public void Ease_StartsAtZeroAndEndsAtOne(double x1, double y1, double x2, double y2)
    {
        var ease = new CubicBezierEase(x1, y1, x2, y2);

        Assert.Equal(0, ease.Ease(0), 3);
        Assert.Equal(1, ease.Ease(1), 3);
    }

    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0.55, 0.55, 0, 1)]
    [InlineData(0.32, 0, 0.67, 0)]
    public void Ease_IsMonotonic(double x1, double y1, double x2, double y2)
    {
        var ease = new CubicBezierEase(x1, y1, x2, y2);

        var previous = ease.Ease(0);
        for (var t = 0.05; t <= 1.0001; t += 0.05)
        {
            var value = ease.Ease(t);
            Assert.True(value >= previous - 1e-9, $"Ease dropped at t={t}");
            previous = value;
        }
    }

    [Fact]
    public void Ease_DecelerateCurve_CoversMostOfTheDistanceEarly()
    {
        var decelerate = new CubicBezierEase(0, 0, 0, 1);

        Assert.True(decelerate.Ease(0.5) > 0.85);
    }

    [Fact]
    public void Ease_LinearControlPoints_AreLinear()
    {
        var linear = new CubicBezierEase(0.25, 0.25, 0.75, 0.75);

        Assert.Equal(0.3, linear.Ease(0.3), 2);
        Assert.Equal(0.7, linear.Ease(0.7), 2);
    }

    [Fact]
    public void Ease_OutOfRangeControlPoints_AreClampedInsteadOfThrowing()
    {
        var overshoot = new CubicBezierEase(0.3, 1.6, 0.6, -0.4);

        var value = overshoot.Ease(0.5);

        Assert.InRange(value, 0, 1);
    }

    [Fact]
    public void ChangingAControlPoint_RebuildsTheCurve()
    {
        var ease = new CubicBezierEase(0.25, 0.25, 0.75, 0.75);
        var before = ease.Ease(0.5);

        ease.Y1 = 1;
        ease.Y2 = 1;

        Assert.True(ease.Ease(0.5) > before + 0.1);
    }

    [Fact]
    public void Clone_KeepsControlPoints()
    {
        var ease = new CubicBezierEase(0, 0, 0, 1);

        var clone = (CubicBezierEase)ease.Clone();

        Assert.Equal(ease.Ease(0.3), clone.Ease(0.3), 6);
    }
}
