using Godot;
using System; 

public partial class Fish : Area2D
{
    [Export]
    private float _speed = 50f;
    private const float MinValidSpeed =0f;
    private const float TimerExpired = 0f;
    [Export]
    private float _rotationSpeed = 5f;
    [Export]
    private float _minLifespan = 20f;
    [Export]
    private float _maxLifespan = 60f; //It might be worth giving rare fish a lower lifespan?
    [Export]
    private float _fadeDuration = 1.0f;
    private const float TransparentAlpha = 0f;
    private bool _isLeaving = false;
    private float _currentLifeSpan;
   //pause time variables are intended to give 'bursty' movement to the fish.
    [Export]
    private float _minPauseTime = 0.5f;
    [Export]
    private float _maxPauseTime = 4.0f;
    [Export]
    private float _burstMultiplier = 1.8f;
    [Export]
    private float _coastMultiplier = .4f;
    [Export]
    private float _deceleration = 60f;
    private float _currentSpeed;
    private bool _restingFish = false;
    private float _pauseTimer = 0f;
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

    // The level this fish was spawned on, so the bite and reel code can read its reaction window and pull strength
    public FishingLevel LevelSettings { get; private set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        _targetPosition = GlobalPosition;
        _currentLifeSpan = (float)GD.RandRange(_minLifespan, _maxLifespan);
    }

    public void SetTarget(Vector2 newTarget)
    {
        _restingFish = false;
        _currentSpeed = _speed * _burstMultiplier;
        _targetPosition = newTarget;
    }

    private void StartDespawnFade()
    {
        _isLeaving = true;
        //Makes it so fading fish can't be caught
        Monitoring = false;
        Monitorable = false;
        Tween fadeTween = CreateTween();
        fadeTween.TweenProperty(this, "modulate:a", TransparentAlpha, _fadeDuration);
        fadeTween.Finished += Despawn;
    }
    public override void _Process(double delta)
    {
        _timeAlive += (float)delta;
        UpdateAnimation((float)delta);

        if(_isLeaving)
        {
            return;
        }

        if (_timeAlive > _currentLifeSpan)
        {
            StartDespawnFade();
            return;
        }
        if(_restingFish)
        {
            _pauseTimer -= (float)delta; 
            if(_pauseTimer <= TimerExpired)
            {
                _restingFish = false;
                OnReachedWaypoint?.Invoke(this);
            }
            return;
        }
        if (GlobalPosition.DistanceSquaredTo(_targetPosition) < ArrivalDistanceSquared)
        {
            _restingFish = true;
            _pauseTimer = (float)GD.RandRange(_minPauseTime, _maxPauseTime);
            return;
        }
        //Helps with smoothing out burst speed to a slow coasting.
        float coastSpeed = _speed * _coastMultiplier;
        _currentSpeed = Mathf.MoveToward(_currentSpeed, coastSpeed, _deceleration * (float)delta);

        GlobalPosition = GlobalPosition.MoveToward(_targetPosition, _currentSpeed * (float)delta);
        
        // Calculate the angle to the target and smoothly rotate the fish to face that direction
        Vector2 direction = _targetPosition - GlobalPosition;
        float targetAngle = Mathf.Atan2(direction.Y, direction.X);
        Rotation = Mathf.LerpAngle(Rotation, targetAngle, _rotationSpeed * (float)delta);
    }

	// This method takes the FishData resource rolled by the spawner and applies its stats
    // The level's settings (if any) make the fish swim faster on harder levels
    public void ApplyData(FishData data, FishingLevel level = null)
    {
        if (data == null)
        {
            GD.PushError("Fish received null FishData!");
            return;
        }

        // Overwrite the default speed with the speed defined in the Godot Inspector for this specific fish
        if(data.Speed > MinValidSpeed)
        {
            _speed = data.Speed;
        }

        if (level != null)
        {
            LevelSettings = level;
            _speed *= level.SwimSpeedMultiplier;
        }

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