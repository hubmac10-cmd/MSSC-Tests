using Godot;

public partial class PlayerMain : CharacterBody3D
{
	public const float Speed = 5.0f;
	public const float JumpVelocity = 4.5f;
	[Export] public float MouseSensitivity = 0.003f;
	[Export] public float StickSensitivity = 2.5f;

	private Node3D _head;

	public override void _Ready() {
		_head = GetNode<Node3D>("Head");
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	private void Look(float yaw, float pitch)
	{
		RotateY(yaw);
		_head.RotateX(pitch);
		var rot = _head.Rotation;
		rot.X = Mathf.Clamp(rot.X, Mathf.DegToRad(-89f), Mathf.DegToRad(89f));
		_head.Rotation = rot;
	}

	public override void _Input(InputEvent @event) {
		if (@event is InputEventMouseMotion mouseMotion) {
			Look(-mouseMotion.Relative.X * MouseSensitivity, -mouseMotion.Relative.Y * MouseSensitivity);
		}
		if (@event.IsActionPressed("ui_cancel")) {
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
		if (@event.IsActionPressed("mouse1")) {
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}
	public override void _PhysicsProcess(double delta) {
		Vector3 velocity = Velocity;
		float d = (float)delta;

		if (!IsOnFloor()) {
			velocity += GetGravity() * d;
		}

		if (Input.IsActionJustPressed("jump") && IsOnFloor()) {
			velocity.Y = JumpVelocity;
		}

		Vector2 inputDir = Input.GetVector("left", "right", "forward", "backward");
		Vector3 direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();
		if (direction != Vector3.Zero) {
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
		} 
		else {
			velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
			velocity.Z = Mathf.MoveToward(Velocity.Z, 0, Speed);
		}

		Vector2 look = Input.GetVector("look_left", "look_right", "look_up", "look_down");
		if (look != Vector2.Zero) {
			Look(-look.X * StickSensitivity * d, -look.Y * StickSensitivity * d);
		}


		Velocity = velocity;
		MoveAndSlide();
	}
}
