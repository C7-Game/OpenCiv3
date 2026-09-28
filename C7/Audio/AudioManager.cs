
using System;
using C7Engine;
using Godot;
using Serilog;

[GlobalClass]
public partial class AudioManager : Node {
	private ILogger log;

	[Export] AudioStreamPlayer musicPlayer;
	[Export] AudioStreamPlayer sfxPlayer;
	[Export] AudioStreamPlayer uiPlayer;
	[Export] AudioStreamPlayer ambiencePlayer;

	public static string MusicBus = "Music";
	public static string SfxBus = "Sfx";
	public static string UIBus = "UI";
	public static string AmbienceBus = "Ambience";

	private bool musicEnabled = true;
	private bool soundEnabled = true;
	private bool uiEnabled = true;
	private bool ambienceEnabled = true;

	public override void _Ready() {
		log = LogManager.ForContext<AudioManager>();
		ConfigureMusic();
		ConfigureSound();
		ConfigureUI();
		ConfigureAmbience();
	}

	private void ConfigureMusic() {
		try {
			string volume = C7Settings.GetSettingValue("audio", "musicVolume");
			float volumeDb = LogicalVolumeAsDecibel(volume);

			if (volumeDb == float.MinValue) {
				musicEnabled = false;
			}

			if (musicEnabled) {
				log.Debug("setting music volume to {volume}, which is {offset} decibel (offset)", volume, volumeDb);
				int busIndex = AudioServer.GetBusIndex(MusicBus);
				AudioServer.SetBusVolumeDb(busIndex, volumeDb);
			}
		} catch (ApplicationException ex) {
			log.Error(ex, "could not configure music");
		}
	}

	private void ConfigureSound() {
		try {
			string volume = C7Settings.GetSettingValue("audio", "soundVolume");
			float volumeDb = LogicalVolumeAsDecibel(volume);

			if (volumeDb == float.MinValue) {
				soundEnabled = false;
			}

			if (soundEnabled) {
				log.Debug("setting sound volume to {volume}, which is {offset} decibel (offset)", volume, volumeDb);
				int busIndex = AudioServer.GetBusIndex(SfxBus);
				AudioServer.SetBusVolumeDb(busIndex, volumeDb);
			}
		} catch (ApplicationException ex) {
			log.Error(ex, "could not configure sound");
		}
	}

	private void ConfigureUI() {
		try {
			string volume = C7Settings.GetSettingValue("audio", "uiVolume");
			float volumeDb = LogicalVolumeAsDecibel(volume);

			if (volumeDb == float.MinValue) {
				uiEnabled = false;
			}

			if (uiEnabled) {
				log.Debug("setting UI volume to {volume}, which is {offset} decibel (offset)", volume, volumeDb);
				int busIndex = AudioServer.GetBusIndex(UIBus);
				AudioServer.SetBusVolumeDb(busIndex, volumeDb);
			}
		} catch (ApplicationException ex) {
			log.Error(ex, "could not configure UI audio");
		}
	}

	private void ConfigureAmbience() {
		try {
			string volume = C7Settings.GetSettingValue("audio", "ambienceVolume");
			float volumeDb = LogicalVolumeAsDecibel(volume);

			if (volumeDb == float.MinValue) {
				ambienceEnabled = false;
			}

			if (ambienceEnabled) {
				log.Debug("setting ambience volume to {volume}, which is {offset} decibel (offset)", volume, volumeDb);
				int busIndex = AudioServer.GetBusIndex(AmbienceBus);
				AudioServer.SetBusVolumeDb(busIndex, volumeDb);
			}
		} catch (ApplicationException ex) {
			log.Error(ex, "could not configure ambience audio");
		}
	}

	/**
	 * Godot uses a decibel offset volume system, described at https://docs.godotengine.org/en/stable/tutorials/audio/audio_buses.html
	 * This is what audio professionals would use, but is not intuitive to end users.
	 * In this system, a 6db difference halves or doubles the volume.
	 * Our users are probably more used to a 0% to 100% system.
	 * So this method converts between them.
	 */
	private float LogicalVolumeAsDecibel(string volume) {
		if (volume == null) {
			//First run.  Save the setting.
			C7Settings.SetValue("audio", "musicVolume", "100");
			C7Settings.SaveSettings();
			return 0;
		}
		int userVolumeSetting = int.Parse(volume);
		if (userVolumeSetting == 100) {
			return 0;
		} else if (userVolumeSetting == 0) {
			return float.MinValue;
		} else {
			//Conversion math based on https://stackoverflow.com/a/37810295/3534605
			return 20.0f * (float)(Math.Log10(userVolumeSetting / 100.0f));
		}
	}

	// TODO: playlists, mixing, transitions
	// See: https://www.youtube.com/watch?app=desktop&v=07Kyqqg31FI&t=346s

	// TODO: polyphony via AudioStreamPolyphonic + AudioStreamPlaybackPolyphonic
	// https://docs.godotengine.org/en/4.0/classes/class_audiostreampolyphonic.html

	public void PlayMusic(string configKey) {
		AudioStream stream = AudioLoader.Load(configKey);

		if (stream == null)
			return;

		musicPlayer.Stream = stream;
		musicPlayer.Play();
	}

	public void StopMusic() {
		musicPlayer.Stop();
	}

	public void PlaySound(string configKey) {
		AudioStream stream = AudioLoader.Load(configKey);

		if (stream == null)
			return;

		sfxPlayer.Stream = stream;
		sfxPlayer.Play();
	}

	public void PlayUI(string configKey) {
		AudioStream stream = AudioLoader.Load(configKey);

		if (stream == null)
			return;

		uiPlayer.Stream = stream;
		uiPlayer.Play();
	}

	public void PlayAmbience(string configKey) {
		AudioStream stream = AudioLoader.Load(configKey);

		if (stream == null)
			return;

		ambiencePlayer.Stream = stream;
		ambiencePlayer.Play();
	}
}
