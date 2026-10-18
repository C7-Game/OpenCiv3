using Godot;

public class AdvisorHead {
	public enum Mood {
		None,
		Happy,
		Angry,
		Sad,
		Surprised
	};

	public enum Advisor {
		None,
		Domestic,
		Trade,
		Military,
		Foreign,
		Culture,
		Science
	};

	// This is a named struct to make the advisor_heads.lua file easier to use.
	public record struct AdvisorGraphicsDetails(
		Advisor advisor,
		Mood mood,
		int eraIndex
	);

	public static ImageTexture GetPopupImage(Advisor advisor, Mood mood, int eraIndex) {
		return TextureLoader.Load("advisor_heads", new AdvisorGraphicsDetails() {
			advisor = advisor,
			mood = mood,
			eraIndex = eraIndex,
		});
	}
}
