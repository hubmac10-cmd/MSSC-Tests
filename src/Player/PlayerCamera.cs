using Godot;

public partial class PlayerCamera : Node3D
{
	[Export] public float Sensitivity = 0.2f;
	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion) {
			GetParent<Node3D>().RotateY(Mathf.DegToRad(-mouseMotion.Relative.X * Sensitivity));
			RotateX(Mathf.DegToRad(-mouseMotion.Relative.Y * Sensitivity));
			Vector3 rot = Rotation;
			rot.X = Mathf.Clamp(rot.X, Mathf.DegToRad(-90), Mathf.DegToRad(90));
			Rotation = rot;
		}
		if (@event.IsActionPressed("ui_cancel")) {
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}
}
