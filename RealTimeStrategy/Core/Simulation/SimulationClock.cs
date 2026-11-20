namespace RealTimeStrategy.Core.Simulation;

/// <summary>
/// Converts real frame time into deterministic fixed simulation steps and an interpolation factor.
/// Lives in <c>Core</c> so the timing rules can be tested without a graphics device.
/// </summary>
public sealed class SimulationClock
{
    public const float DefaultTickRate = 20f;

    private const int DefaultMaxStepsPerFrame = 5;
    private const double Epsilon = 1e-9;

    private readonly double _tickInterval;
    private double _accumulator;

    /// <param name="tickRate">Simulation ticks per second. Must be positive.</param>
    /// <param name="maxStepsPerFrame">
    /// Upper bound for catch-up steps in a single <see cref="Advance"/> call. Prevents the
    /// "spiral of death" after a debugger pause, window drag or a slow frame.
    /// </param>
    public SimulationClock(float tickRate = DefaultTickRate, int maxStepsPerFrame = DefaultMaxStepsPerFrame)
    {
        if (tickRate <= 0 || float.IsNaN(tickRate) || float.IsInfinity(tickRate))
        {
            throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "Tick rate must be finite and positive.");
        }

        if (maxStepsPerFrame < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStepsPerFrame), maxStepsPerFrame, "At least one step per frame is required.");
        }

        _tickInterval = 1.0 / tickRate;
        TickInterval = (float)_tickInterval;
        MaxStepsPerFrame = maxStepsPerFrame;
    }

    /// <summary>Fixed simulation delta, in seconds (0.05 s at 20 Hz).</summary>
    public float TickInterval { get; }

    /// <summary>Total number of simulation ticks executed so far.</summary>
    public long TickCount { get; private set; }

    /// <summary>Fraction (0–1) between the previous and current simulation tick, for render interpolation.</summary>
    public float Alpha { get; private set; }

    public int MaxStepsPerFrame { get; }

    /// <summary>Steps returned by the last <see cref="Advance"/> call.</summary>
    public int StepsLastAdvance { get; private set; }

    /// <summary>
    /// Adds elapsed real time and returns how many fixed simulation steps should run now.
    /// Invalid or non-positive input is ignored. Excess time beyond the catch-up budget is dropped.
    /// </summary>
    public int Advance(double elapsedSeconds)
    {
        StepsLastAdvance = 0;

        if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds <= 0)
        {
            return 0;
        }

        var maxElapsed = _tickInterval * MaxStepsPerFrame;
        if (elapsedSeconds > maxElapsed)
        {
            elapsedSeconds = maxElapsed;
        }

        _accumulator += elapsedSeconds;

        var steps = (int)((_accumulator + Epsilon) / _tickInterval);
        if (steps > MaxStepsPerFrame)
        {
            steps = MaxStepsPerFrame;
        }

        if (steps > 0)
        {
            _accumulator -= steps * _tickInterval;
            if (_accumulator < 0)
            {
                _accumulator = 0;
            }

            TickCount += steps;
        }

        Alpha = (float)(_accumulator / _tickInterval);
        StepsLastAdvance = steps;
        return steps;
    }
}
