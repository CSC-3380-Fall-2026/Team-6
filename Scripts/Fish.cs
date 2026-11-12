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
    private const float ArrivalDistanceSquared = 100.0f;


    // Controls how many animation frames play per second, exposed in the Godot Inspector
    [Export]
    private float _animationFps = 8f;

    // Prevents division-by-zero or invalid 0-frame slicing on non-square textures
    private const int MinFrames = 1;

    // Accumulates delta time between sprite frame advances
    private float _animationTimer = 0f;

    [Export]
    private Sprite2D _sprite;
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
        UpdateAnimation((float)delta);
        if (_timeAlive > _lifespan)
        {
            Despawn();
            return;
        }

        // Check if we are close enough to our target waypoint
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

	// This method takes the FishData resource rolled by the spawner and applies its stats
    public void ApplyData(FishData data)
    {
        if (data == null)
        {
            GD.PushError("Fish received null FishData!");
            return;
        }

        _speed = data.Speed;

        if(_sprite != null && data.GoneFishingSprite != null)
        {
            _sprite.Texture = data.GoneFishingSprite;

            // Calculate how many square frames fit horizontally across the strip. We're cutting up the animations here.
            int safeHeight = Mathf.Max(MinFrames, data.GoneFishingSprite.GetHeight());
            int frameCount = data.GoneFishingSprite.GetWidth() / safeHeight;
            _sprite.Hframes = Mathf.Max(MinFrames, frameCount);

            //loop the animation
            _sprite.Frame = 0;
        }

        // Overwrite the default speed with the speed defined in the Godot Inspector for this specific fish
        _speed = data.Speed;
    }
    // Steps through the horizontal sprite sheet frames at the configured FPS rate
    private void UpdateAnimation(float delta)
    {
        // Skip animation if there is no proper animation in play.
        if (_sprite == null || _sprite.Hframes <= MinFrames || _animationFps <= 0f)
        {
            return;
        }

        // Accumulate elapsed time since the last frame swap
        _animationTimer += delta;

        // Calculate how many seconds each single frame should stay on screen
        float frameDuration = 1f / _animationFps;

        // Once enough time has passed, advance to the next frame and wrap around with modulo
        if (_animationTimer >= frameDuration)
        {
            _animationTimer -= frameDuration;
            _sprite.Frame = (_sprite.Frame + 1) % _sprite.Hframes;
        }
    }
    public void Despawn()
    {
        OnDespawn?.Invoke(this);
        QueueFree();
    }
}