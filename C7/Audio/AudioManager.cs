
using System;
using System.Collections.Generic;
using C7Engine;
using Godot;
using Serilog;

[GlobalClass]
public partial class AudioManager : Node {
	private ILogger log;

	[Export] AudioStreamPlayer musicPlayer;
	[Export] AudioStreamPlayer sfxAudioPlayer;
	[Export] AudioStreamPlayer uiAudioPlayer;
	[Export] AudioStreamPlayer ambienceAudioPlayer;

	private PolyphonicAudioPlayer _polyMusicPlayer;
	private PolyphonicAudioPlayer _polySfxAudioPlayer;
	private PolyphonicAudioPlayer _polyUiAudioPlayer;
	private PolyphonicAudioPlayer _polyAmbienceAudioPlayer;

	private static string AudioSettingsSection = "audio";

	public static string MusicBus = "Music";
	public static string SfxAudioBus = "Sfx";
	public static string UIAudioBus = "UI";
	public static string AmbienceAudioBus = "Ambience";

	private bool musicEnabled = true;
	private bool sfxAudioEnabled = true;
	private bool uiAudioEnabled = true;
	private bool ambienceAudioEnabled = true;
	// Maps a bus to the settings key holding its volume. Public so settings UI
	// can drive a bus without repeating the magic strings.
	public static readonly Dictionary<string, string> BusVolumeKeys = new() {
		{ MusicBus, "musicVolume" },
		{ SfxAudioBus, "sfxAudioVolume" },
		{ UIAudioBus, "uiAudioVolume" },
		{ AmbienceAudioBus, "ambienceAudioVolume" },
	};



	public override void _Ready() {
		log = LogManager.ForContext<AudioManager>();

		musicEnabled = ConfigureVolume("musicVolume", MusicBus);
		_polyMusicPlayer = new PolyphonicAudioPlayer(musicPlayer, 2);

		sfxAudioEnabled = ConfigureVolume("sfxAudioVolume", SfxAudioBus);
		_polySfxAudioPlayer = new PolyphonicAudioPlayer(sfxAudioPlayer, 32);

		uiAudioEnabled = ConfigureVolume("uiAudioVolume", UIAudioBus);
		_polyUiAudioPlayer = new PolyphonicAudioPlayer(uiAudioPlayer, 8);

		ambienceAudioEnabled = ConfigureVolume("ambienceAudioVolume", AmbienceAudioBus);
		_polyAmbienceAudioPlayer = new PolyphonicAudioPlayer(ambienceAudioPlayer, 8);
	}

	private bool ConfigureVolume(string volumeKey, string audioBus) {
		try {
			string volume = C7Settings.GetSettingValue(AudioSettingsSection, volumeKey);
			float volumeDb = LogicalVolumeAsDecibel(int.Parse(volume));

			if (volumeDb == float.MinValue) {
				return false;
			}

			log.Debug("setting {volumeKey} to {volume}, which is {offset} decibel (offset)",
				volumeKey, volume, volumeDb);

			int busIndex = AudioServer.GetBusIndex(audioBus);
			AudioServer.SetBusVolumeDb(busIndex, volumeDb);

			return true;
		} catch (ApplicationException ex) {
			log.Error(ex, "could not configure {volumeKey}", volumeKey);
			return false;
		}
	}

	/**
	 * Godot uses a decibel offset volume system, described at https://docs.godotengine.org/en/stable/tutorials/audio/audio_buses.html
	 * This is what audio professionals would use, but is not intuitive to end users.
	 * In this system, a 6db difference halves or doubles the volume.
	 * Our users are probably more used to a 0% to 100% system.
	 * So this method converts between them.
	 */
		public static float LogicalVolumeAsDecibel(int volume) {
		if (volume <= 0) {
			return float.MinValue;
		}
		if (volume >= 100) {
			return 0;
		}
		return 20.0f * (float)(Math.Log10(volume / 100.0f));
	}

	// TODO: playlists, mixing, transitions
	// See: https://www.youtube.com/watch?app=desktop&v=07Kyqqg31FI&t=346s

	// TODO: polyphony via AudioStreamPolyphonic + AudioStreamPlaybackPolyphonic
	// https://docs.godotengine.org/en/4.0/classes/class_audiostreampolyphonic.html

	private long currentMusicId = AudioStreamPlaybackPolyphonic.InvalidId;

	public void PlayMusic(string configKey) {
		AudioStream stream = AudioLoader.Load(configKey);

		if (stream == null)
			return;

		if (currentMusicId != AudioStreamPlaybackPolyphonic.InvalidId) {
			_polyMusicPlayer.Stop(currentMusicId);
			currentMusicId = AudioStreamPlaybackPolyphonic.InvalidId;
		}

		currentMusicId = _polyMusicPlayer.Play(stream);
	}


	public void StopMusic() {
		if (currentMusicId != AudioStreamPlaybackPolyphonic.InvalidId) {
			_polyMusicPlayer.Stop(currentMusicId);
			currentMusicId = AudioStreamPlaybackPolyphonic.InvalidId;
		}
	}


	/// <summary>
	/// The user's volume for a bus, as a 0-100 percentage, or -1 if the bus is
	/// unknown.
	/// </summary>
	public int GetBusVolume(string bus) {
		if (!BusVolumeKeys.TryGetValue(bus, out string key)) {
			log.Warning("No volume setting known for bus {bus}", bus);
			return -1;
		}
		string stored = C7Settings.GetSettingsValueOrDefault(AudioSettingsSection, key, "100");
		return int.TryParse(stored, out int volume) ? volume : 100;
	}

	public void SetBusVolume(string bus, int volume) {
		if (!BusVolumeKeys.TryGetValue(bus, out string key)) {
			log.Warning("No volume setting known for bus {bus}", bus);
			return;
		}
		volume = Mathf.Clamp(volume, 0, 100);
		C7Settings.SetValue(AudioSettingsSection, key, volume.ToString());
		C7Settings.SaveSettings();
		SetBusVolumeDb(bus, LogicalVolumeAsDecibel(volume));
	}

	public void SetBusVolumePercent(string bus, int volume) {
		SetBusVolumeDb(bus, LogicalVolumeAsDecibel(Mathf.Clamp(volume, 0, 100)));
	}

	private void SetBusVolumeDb(string bus, float volumeDb) {
		int busIndex = AudioServer.GetBusIndex(bus);
		if (busIndex < 0) {
			log.Warning("Audio bus {bus} not found", bus);
			return;
		}
		AudioServer.SetBusVolumeDb(busIndex, volumeDb);
		AudioServer.SetBusMute(busIndex, volumeDb == float.MinValue);
	}
	public void PlaySfxAudio(string configKey) {
		var stream = AudioLoader.Load(configKey);
		if (stream != null)
			_polySfxAudioPlayer.Play(stream);
	}

	public void PlayUIAudio(string configKey) {
		var stream = AudioLoader.Load(configKey);
		if (stream != null)
			_polyUiAudioPlayer.Play(stream);
	}

	public void PlayAmbienceAudio(string configKey) {
		var stream = AudioLoader.Load(configKey);
		if (stream != null)
			_polyAmbienceAudioPlayer.Play(stream);
	}
}
