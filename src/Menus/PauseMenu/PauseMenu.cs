using Godot;

public partial class PauseMenu : CanvasLayer
{
	private Button ResumeButton;

	public override void _Ready() {
		Visible = false;
		ResumeButton = GetNode<VBoxContainer>("ButtonContainer").GetNode<Button>("ResumeButton");
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (@event.IsActionPressed("escape")) {
			TogglePause();
		} 
	}

	private void OnResumeButtonPressed() {
		TogglePause();
	}

	private void OnQuitButtonPressed() {
		GetTree().Paused = false;
		GetTree().Quit();
	}

	private void TogglePause() {
		bool NewPausedState = !GetTree().Paused;
		GetTree().Paused = NewPausedState;
		Visible = NewPausedState;
		if (NewPausedState) {
			Input.MouseMode = Input.MouseModeEnum.Visible;
			ResumeButton.GrabFocus();
		} else {
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}
}
