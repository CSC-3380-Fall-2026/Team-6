using Godot;
using System.Collections.Generic;

// Plays all of the game's sound effects and music. Sound effects go through the SFX bus and music through
// the Music bus (set up in default_bus_layout.tres), so each can have its own volume.
// The sounds are picked in the Inspector on Scenes/AudioManager.tscn, and the files go in Assets/Audio.
// Every slot starts empty and an empty slot just plays nothing, so the game works before any sounds are chosen.
// Each scene starts its own music by calling AudioManager.Instance.PlayMusic(...) in its _Ready.
// The music crossfades when the track changes, and pauses while the game is paused.
public partial class AudioManager : Node
{
	// Lets any script call AudioManager.Instance.PlaySFX(AudioManager.Catch)
	public static AudioManager Instance { get; private set; }

	// Sound effect names for PlaySFX
	public const string MenuHover = "menu_hover";
	public const string MenuClick = "menu_click";
	public const string Cast = "cast";
	public const string BobberSplash = "bobber_splash";
	public const string Catch = "catch";
	public const string FishEscape = "fish_escape";
	public const string Slice = "slice";
	public const string LifeLost = "life_lost";
	public const string GameOver = "game_over";

	// Every sound effect name, comma separated, for the Inspector. Clickable uses it with PropertyHint.EnumSuggestion
	// to show these names as a dropdown on its hover and click sounds, while still allowing the field to be left blank for no sound.
	public const string SfxNames = MenuHover + "," + MenuClick + "," + Cast + "," + BobberSplash + "," + Catch + ","
		+ FishEscape + "," + Slice + "," + LifeLost + "," + GameOver;

	// Music track names for PlayMusic
	public const string MenuMusic = "menu";
	public const string GoneFishingMusic = "gone_fishing";
	public const string FishNinjaMusic = "fish_ninja";

	private const string SfxBus = "SFX";
	private const string MusicBus = "Music";
	// How many copies of the same sound effect can play over each other, like several slices in a row
	private const int VoicesPerSound = 4;

	// Assigned in the Inspector. Leave a slot empty for no sound.
	[ExportGroup("Sound Effects")]
	[Export] private AudioStream menuHoverSound;
	[Export] private AudioStream menuClickSound;
	[Export] private AudioStream castSound;
	[Export] private AudioStream bobberSplashSound;
	[Export] private AudioStream catchSound;
	[Export] private AudioStream fishEscapeSound;
	[Export] private AudioStream sliceSound;
	[Export] private AudioStream lifeLostSound;
	[Export] private AudioStream gameOverSound;

	// Turn on Loop in each music file's Import settings so it loops without a gap
	[ExportGroup("Music")]
	[Export] private AudioStream menuTrack;
	[Export] private AudioStream goneFishingTrack;
	[Export] private AudioStream fishNinjaTrack;
	[Export] private float crossfadeSeconds = 0.75f;

	private readonly Dictionary<string, AudioStreamPlayer> sfxPlayers = new Dictionary<string, AudioStreamPlayer>();
	private readonly Dictionary<string, AudioStream> musicTracks = new Dictionary<string, AudioStream>();
	// Two music players, so one track can fade out while the next one fades in
	private AudioStreamPlayer musicA;
	private AudioStreamPlayer musicB;
	private AudioStreamPlayer currentMusic;
	private string currentTrack = "";
	private Tween crossfade;

	public override void _Ready()
	{
		if (Instance != null) {
			QueueFree();
			return;
		}
		Instance = this;

		if (AudioServer.GetBusIndex(SfxBus) == -1 || AudioServer.GetBusIndex(MusicBus) == -1) {
			GD.PushWarning("AudioManager: the SFX and Music buses are missing from default_bus_layout.tres, so everything plays on Master");
		}

		AddSfx(MenuHover, menuHoverSound);
		AddSfx(MenuClick, menuClickSound);
		AddSfx(Cast, castSound);
		AddSfx(BobberSplash, bobberSplashSound);
		AddSfx(Catch, catchSound);
		AddSfx(FishEscape, fishEscapeSound);
		AddSfx(Slice, sliceSound);
		AddSfx(LifeLost, lifeLostSound);
		AddSfx(GameOver, gameOverSound);

		musicTracks[MenuMusic] = menuTrack;
		musicTracks[GoneFishingMusic] = goneFishingTrack;
		musicTracks[FishNinjaMusic] = fishNinjaTrack;
		musicA = AddMusicPlayer("MusicA");
		musicB = AddMusicPlayer("MusicB");
		currentMusic = musicA;
	}

	// Plays one of the sound effect names above. Does nothing if that sound hasn't been picked yet.
	public void PlaySFX(string name)
	{
		if (string.IsNullOrEmpty(name)) {
			return;
		}
		if (!sfxPlayers.TryGetValue(name, out AudioStreamPlayer player)) {
			GD.PushError($"AudioManager: there's no sound effect called '{name}'");
			return;
		}
		if (player.Stream != null) {
			player.Play();
		}
	}

	// Crossfades from the current music to one of the track names above. Call it from a scene's _Ready.
	// Asking for the track that's already playing keeps it going, and an empty slot just fades the music out.
	public void PlayMusic(string track)
	{
		if (!musicTracks.TryGetValue(track, out AudioStream stream)) {
			GD.PushError($"AudioManager: there's no music track called '{track}'");
			return;
		}
		if (track == currentTrack) {
			return;
		}
		currentTrack = track;

		AudioStreamPlayer fadingOut = currentMusic;
		currentMusic = currentMusic == musicA ? musicB : musicA;

		crossfade?.Kill();
		// Stop makes the fade wait along with the music while the game is paused
		crossfade = CreateTween().SetParallel().SetPauseMode(Tween.TweenPauseMode.Stop);
		crossfade.TweenProperty(fadingOut, "volume_linear", 0f, crossfadeSeconds);
		if (stream != null) {
			currentMusic.Stream = stream;
			currentMusic.VolumeLinear = 0f;
			currentMusic.Play();
			crossfade.TweenProperty(currentMusic, "volume_linear", 1f, crossfadeSeconds);
		}
		crossfade.Chain().TweenCallback(Callable.From(fadingOut.Stop));
	}

	private void AddSfx(string name, AudioStream stream)
	{
		AudioStreamPlayer player = new AudioStreamPlayer {
			Name = name,
			Stream = stream,
			Bus = SfxBus,
			MaxPolyphony = VoicesPerSound,
		};
		AddChild(player);
		sfxPlayers[name] = player;
	}

	private AudioStreamPlayer AddMusicPlayer(string name)
	{
		AudioStreamPlayer player = new AudioStreamPlayer {
			Name = name,
			Bus = MusicBus,
			// This manager keeps running while paused so menu sounds still play, but the music pauses
			ProcessMode = ProcessModeEnum.Pausable,
		};
		// Keeps the track looping even if Loop wasn't turned on when it was imported
		player.Finished += () => {
			if (player == currentMusic) {
				player.Play();
			}
		};
		AddChild(player);
		return player;
	}
}
