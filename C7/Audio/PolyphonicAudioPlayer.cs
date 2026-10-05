using Godot;

public class PolyphonicAudioPlayer {
	private readonly AudioStreamPlayer player;
	private AudioStreamPlaybackPolyphonic playback;

	public PolyphonicAudioPlayer(AudioStreamPlayer player, int polyphony = 32) {
		this.player = player;

		player.Stream = new AudioStreamPolyphonic {
			Polyphony = polyphony
		};

		player.Play();

		playback = player.GetStreamPlayback()
			as AudioStreamPlaybackPolyphonic;
	}

	public long Play(AudioStream stream) {
		return playback.PlayStream(stream);
	}

	public void Stop(long id) {
		playback.StopStream(id);
	}

	public void StopAll() {
		player.Stop();
		player.Play();

		playback = player.GetStreamPlayback()
			as AudioStreamPlaybackPolyphonic;
	}
}
