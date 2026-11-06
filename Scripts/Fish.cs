using Godot;
using System; 

public partial class Fish : Area2D
{
    [Export]
    private float _speed = 50f;

    [Export]
    private float _rotationSpeed = 5f;

    [Export]
    private float _lifespan = 30f;
    private const float ArrivalDistanceSquared = 10.0f;

    private Vector2 _targetPosition;
    private float _timeAlive = 0f;
    public event Action<Fish> OnReachedWaypoint;
    public event Action<Fish> OnDespawn;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        _targetPosition = GlobalPosition;
    }

    public void SetTarget(Vector2 newTarget)
    {
        _targetPosition = newTarget;
    }

    public override void _Process(double delta)
    {
        _timeAlive += (float)delta;

        if (_timeAlive > _lifespan)
        {
            OnDespawn?.Invoke(this);
            QueueFree();
            return;
        }

        // Check if we are close enough to our target waypoint using our extracted constant
        if (GlobalPosition.DistanceSquaredTo(_targetPosition) < ArrivalDistanceSquared)
        {
            OnReachedWaypoint?.Invoke(this);
            return;
        }

        // Move linearly toward the target
        GlobalPosition = GlobalPosition.MoveToward(_targetPosition, _speed * (float)delta);

        // Calculate the angle to the target and smoothly rotate the fish to face that direction
        Vector2 direction = _targetPosition - GlobalPosition;
        float targetAngle = Mathf.Atan2(direction.Y, direction.X);
        Rotation = Mathf.LerpAngle(Rotation, targetAngle, _rotationSpeed * (float)delta);
    }
}