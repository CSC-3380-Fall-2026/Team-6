using Godot;

// butterfly flaps and drifts around spot it was placed at
// picks random point nearby, flutters towards it, then picks another
// bobs up and down while flying

public partial class Butterfly : AnimatedSprite2D
{
    [Export] private float _wanderRadius = 220f; // how far from starting point it wanders
    [Export] private float _speed = 70f; // pixels per sec
    [Export] private float _bobHeight = 6f; // pixels it bobs up and down while flying
    [Export] private float _bobSpeed = 6f; // how fast it bobs
    [Export] private bool _rotateToFace = true; // butterfly rotates towards where it flies
    [Export] private float _turnSpeed = 4f; // how quickly it turns toward where it's going

    private const float ArriveDistance = 8f; // close enough to point to pick the next one
    private const float FacingOffset = Mathf.Pi / 2f; // top-down sprite faces up, but angle 0 points right

    private Vector2 _home; // where it was placed in the scene
    private Vector2 _flightPosition; // where it is, not counting the bob
    private Vector2 _target; 
    private float _time;

    public override void _Ready()
    {
        _home = Position;
        _flightPosition = Position;
        _time = (float)GD.RandRange(0.0, Mathf.Tau); // start each butterfly at different point in its bob
        PickNewTarget();
        Play();
    }

    public override void _Process(double delta)
    {
        float step = (float)delta;
        _time += step;

        Vector2 toTarget = _target - _flightPosition;
        if (toTarget.Length() < ArriveDistance)
        {
            PickNewTarget();
            return;
        }

        _flightPosition = _flightPosition.MoveToward(_target, _speed * step);
        Position = _flightPosition + Vector2.Up * Mathf.Sin(_time * _bobSpeed) * _bobHeight;

        if (_rotateToFace)
        {
            Rotation = Mathf.LerpAngle(Rotation, toTarget.Angle() * FacingOffset, _turnSpeed * step);
        }
        else
        {
            bool goingRight = toTarget.X > 0f;
        }
    }

    // random point within wander radius of its starting spot
    private void PickNewTarget()
    {
        float angle = (float)GD.RandRange(0.0, Mathf.Tau);
        float distance = Mathf.Sqrt(GD.Randf()) * _wanderRadius; // square root spreads points evenly over the circle
        _target = _home + Vector2.FromAngle(angle) * distance;
    }
}