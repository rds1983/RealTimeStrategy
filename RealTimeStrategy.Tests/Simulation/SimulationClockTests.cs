using RealTimeStrategy.Core.Simulation;
using Xunit;

namespace RealTimeStrategy.Tests.Simulation;

public sealed class SimulationClockTests
{
    private const double Tolerance = 1e-4;

    [Fact]
    public void Default_interval_is_twenty_hertz()
    {
        var clock = new SimulationClock();

        Assert.Equal(20f, SimulationClock.DefaultTickRate);
        Assert.Equal(0.05f, clock.TickInterval, 4);
        Assert.Equal(5, clock.MaxStepsPerFrame);
    }

    [Fact]
    public void One_second_of_sixty_fps_frames_runs_twenty_ticks()
    {
        var clock = new SimulationClock();

        for (var i = 0; i < 60; i++)
        {
            clock.Advance(1.0 / 60.0);
        }

        Assert.Equal(20, clock.TickCount);
        Assert.InRange(clock.Alpha, 0f, 1f);
    }

    [Fact]
    public void Partial_ticks_accumulate()
    {
        var clock = new SimulationClock();

        Assert.Equal(0, clock.Advance(0.02));
        Assert.InRange(clock.Alpha, 0.4f - (float)Tolerance, 0.4f + (float)Tolerance);

        Assert.Equal(0, clock.Advance(0.02));
        Assert.InRange(clock.Alpha, 0.8f - (float)Tolerance, 0.8f + (float)Tolerance);

        Assert.Equal(1, clock.Advance(0.02));
        Assert.Equal(1, clock.TickCount);
        Assert.InRange(clock.Alpha, 0.2f - (float)Tolerance, 0.2f + (float)Tolerance);
    }

    [Fact]
    public void Half_a_tick_exposes_half_alpha()
    {
        var clock = new SimulationClock();

        Assert.Equal(0, clock.Advance(0.025));
        Assert.InRange(clock.Alpha, 0.5f - (float)Tolerance, 0.5f + (float)Tolerance);
    }

    [Fact]
    public void Large_frame_is_clamped_to_max_steps()
    {
        var clock = new SimulationClock();

        var steps = clock.Advance(10.0);

        Assert.Equal(clock.MaxStepsPerFrame, steps);
        Assert.Equal(clock.MaxStepsPerFrame, clock.TickCount);
        Assert.Equal(0f, clock.Alpha, 4);
    }

    [Fact]
    public void Clamped_time_is_dropped_not_banked()
    {
        var clock = new SimulationClock();

        clock.Advance(10.0);
        clock.Advance(10.0);

        Assert.Equal(clock.MaxStepsPerFrame * 2, clock.TickCount);
    }

    [Fact]
    public void Custom_max_steps_is_respected()
    {
        var clock = new SimulationClock(20f, maxStepsPerFrame: 2);

        Assert.Equal(2, clock.Advance(5.0));
        Assert.Equal(2, clock.StepsLastAdvance);
    }

    [Fact]
    public void Custom_tick_rate_changes_interval()
    {
        var clock = new SimulationClock(10f);

        Assert.Equal(0.1f, clock.TickInterval, 4);

        for (var i = 0; i < 10; i++)
        {
            clock.Advance(0.1);
        }

        Assert.Equal(10, clock.TickCount);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Invalid_elapsed_time_is_ignored(double elapsed)
    {
        var clock = new SimulationClock();
        clock.Advance(0.03);

        var alphaBefore = clock.Alpha;

        Assert.Equal(0, clock.Advance(elapsed));
        Assert.Equal(0, clock.TickCount);
        Assert.Equal(alphaBefore, clock.Alpha);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Invalid_tick_rate_throws(float tickRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationClock(tickRate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_max_steps_throws(int maxSteps)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationClock(20f, maxSteps));
    }

    [Fact]
    public void Tick_count_is_independent_of_frame_rate()
    {
        var atSixtyFps = new SimulationClock();
        for (var i = 0; i < 60; i++)
        {
            atSixtyFps.Advance(1.0 / 60.0);
        }

        var atTwentyFps = new SimulationClock();
        for (var i = 0; i < 20; i++)
        {
            atTwentyFps.Advance(0.05);
        }

        Assert.Equal(20, atSixtyFps.TickCount);
        Assert.Equal(20, atTwentyFps.TickCount);
    }
}
