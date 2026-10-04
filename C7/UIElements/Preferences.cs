using C7.UIElements;
using Godot;

/**
 * User preferences: settings that stay stable across games and scenarios, so
 * they are stored in C7.ini rather than in the save file.
 *
 * Deliberately not a PopupOverlay child. Preferences is a main menu style view,
 * not a lightweight in-game modal, so it is a full scene shown and hidden in
 * place, like the advisors.
 */
[GlobalClass]
public partial class Preferences : Control {
	[Export] Civ3Checkbox promptForResearch;
	[Export] Civ3HSlider musicVolume;
	[Export] Civ3HSlider sfxVolume;
	[Export] Civ3HSlider uiVolume;
	[Export] Civ3HSlider ambienceVolume;
	[Export] Label musicVolumeLabel;
	[Export] Label sfxVolumeLabel;
	[Export] Label uiVolumeLabel;
	[Export] Label ambienceVolumeLabel;
	[Export] Control musicVolumeRow;
	[Export] Control sfxVolumeRow;
	[Export] Control uiVolumeRow;
	[Export] Control ambienceVolumeRow;
	[Export] Civ3TextureButton close;

	private AudioManager audioManager;

	public override void _Ready() {
		audioManager = GetNode<AudioManager>("/root/GlobalAudioManager");

		promptForResearch.ButtonPressed = PreferencesSettings.GetPromptForResearch();
		promptForResearch.Toggled += enabled => {
			PreferencesSettings.SetPromptForResearch(enabled);
		};

		ConnectVolumeSlider(musicVolumeRow, musicVolume, musicVolumeLabel, AudioManager.MusicBus);
		ConnectVolumeSlider(sfxVolumeRow, sfxVolume, sfxVolumeLabel, AudioManager.SfxAudioBus);
		ConnectVolumeSlider(uiVolumeRow, uiVolume, uiVolumeLabel, AudioManager.UIAudioBus);
		ConnectVolumeSlider(ambienceVolumeRow, ambienceVolume, ambienceVolumeLabel, AudioManager.AmbienceAudioBus);

		TextureLoader.SetButtonTextures(close, "ui.exit");
		close.Pressed += HidePreferences;
	}

	private void ConnectVolumeSlider(Control row, Civ3HSlider slider, Label valueLabel, string bus) {
		int volume = audioManager.GetBusVolume(bus);
		if (volume < 0) {
			row.Visible = false;
			return;
		}

		slider.Value = volume;
		UpdateVolumeLabel(valueLabel, volume);

		slider.ValueChanged += value => {
			UpdateVolumeLabel(valueLabel, (int)value);
			audioManager.SetBusVolumePercent(bus, (int)value);
		};
		slider.DragEnded += changed => {
			audioManager.SetBusVolume(bus, (int)slider.Value);
		};
	}

	private static void UpdateVolumeLabel(Label label, int volume) {
		label.Text = volume == 0 ? "Off" : $"{volume}%";
	}

	public void ShowPreferences() {
		Show();
	}

	public void HidePreferences() {
		Hide();
	}
}
